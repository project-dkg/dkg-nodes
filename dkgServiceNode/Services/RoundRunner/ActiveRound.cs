// Copyright (C) 2024 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of dkg service node
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions
// are met:
// 1. Redistributions of source code must retain the above copyright
// notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
// TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
// PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDERS OR CONTRIBUTORS
// BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.

using Grpc.Net.Client;

using dkgCommon;
using dkgServiceNode.Models;
using static dkgCommon.DkgNode;


namespace dkgServiceNode.Services.RoundRunner
{
    public class ActiveRound
    {
        private GrpcChannel[]? _channels { get; set; } = null;
        private DkgNodeClient[]? _dkgNodes { get; set; } = null;
        private Round _round { get; set; }
        private readonly ILogger<ActiveRound> _logger;
        private static readonly object lockObject = new();
        public ActiveRound(Round round, ILogger<ActiveRound> logger)
        {
            _logger = logger;
            _round = round;

            _logger.LogDebug("Round [{Id}]: Created", Id);
        }
        public int Id
        {
            get { return _round.Id; }
        }
        private void InitInternal(List<Node> nodes)
        {
            _logger.LogDebug("Round [{Id}]: InitInternal", Id);
            _channels = new GrpcChannel[nodes.Count];
            _dkgNodes = new DkgNodeClient[nodes.Count];
            int j = 0;
            foreach (Node node in nodes)
            {
                string dest = $"http://{node.Host}:{node.Port}";
                _channels[j] = GrpcChannel.ForAddress(dest);
                _dkgNodes[j] = new DkgNodeClient(_channels[j]);  // ChannelCredentials.Insecure ???
                _logger.LogDebug("Round [{Id}]: Created channel to {dest}", Id, dest);
                j++;
            }
            _logger.LogDebug("Round [{Id}]: InitInternal completed", Id);
        }

        private void ClearInternal()
        {
            _logger.LogDebug("Round [{Id}]: ClearInternal", Id);

            List<Task> shutdownTasks = [];
            try
            {
                for (int j = 0; j < _dkgNodes?.Length; j++)
                {
                    shutdownTasks.Add(_dkgNodes[j].EndRoundAsync(new EndRoundRequest { RoundId = Id }).ResponseAsync);
                }
                Task.WaitAll([.. shutdownTasks]);
            } 
            catch (Exception ex) 
            {
                _logger.LogError("Round [{Id}]: ClearInternal exception at EndRound\n{message}", Id, ex.Message);
            }

            shutdownTasks.Clear();
            try
            {
                for (int i = 0; i < _channels?.Length; i++)
                {
                    shutdownTasks.Add(_channels[i].ShutdownAsync());
                }
                Task.WaitAll([.. shutdownTasks]);
            }
            catch (Exception ex)
            {
                _logger.LogError("Round [{Id}]: ClearInternal exception at Shutdown\n{message}", Id, ex.Message);
            }
            _logger.LogDebug("Round [{Id}]: ClearInternal completed", Id);
        }

        public void Run(List<Node> nodes)
        {
            _logger.LogDebug("Round [{Id}]: Run for {count} nodes", Id, _dkgNodes?.Length);
            lock (lockObject)
            {
                InitInternal(nodes);

                try
                {
                    var runRoundRequest = new RunRoundRequest
                    {
                        RoundId = Id
                    };

                    foreach (var node in nodes)
                    {
                        runRoundRequest.DkgNodeRefs.Add(new DkgNodeRef
                        {
                            Port = node.Port,
                            Host = node.Host,
                            PublicKey = node.PublicKey,
                        });
                    }


                    List<Task> startRounsTasks = [];
                    for (int j = 0; j < _dkgNodes?.Length; j++)
                    {
                        startRounsTasks.Add(_dkgNodes[j].RunRoundAsync(runRoundRequest).ResponseAsync);
                    }

                    Task.WaitAll([.. startRounsTasks]);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Round [{Id}]: Run exception at RunRound\n{message}", Id, ex.Message);
                }
                _logger.LogDebug("Round [{Id}]: Run completed", Id);
            }
        }

        public int? GetResult()
        {
            int? result = null;

            _logger.LogDebug("Round [{Id}]: GetResult", Id);
            lock (lockObject)
            {
                if (_dkgNodes != null)
                {
                    try
                    {
                        foreach (var dkgNode in _dkgNodes)
                        {
                            var roundResultRequest = new RoundResultRequest
                            {
                                RoundId = _round.Id
                            };

                            RoundResultReply roundResultReply = dkgNode.RoundResult(roundResultRequest);

                            if (roundResultReply.Res)
                            {
                                result = BitConverter.ToInt32(roundResultReply.DistributedPublicKey.ToByteArray(), 0);
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Round [{Id}]: GetResult terminating with exception\n{message}", Id, ex.Message);
                    }
                }
            }
            _logger.LogDebug("Round [{Id}]: GetResult returning {result}", Id, result);

            return result;
        }

        public void Clear(List<Node>? nodes)
        {
            _logger.LogDebug("Round [{Id}]: Clear", Id);
            lock (lockObject)
            {
                if (_dkgNodes == null && nodes != null)
                {
                    InitInternal(nodes);
                }
                ClearInternal();
            }
            _logger.LogDebug("Round [{Id}]: Clear completed", Id);
        }

    }
}
