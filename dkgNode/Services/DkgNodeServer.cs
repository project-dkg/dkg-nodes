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

using Google.Protobuf;
using Grpc.Core;

using dkg.group;
using dkg.poly;
using dkg.share;


using dkgCommon;
using dkgNode.Models;
using dkgCommon.Constants;

using static dkgCommon.DkgNode;
using static dkgCommon.Constants.NStatus;


namespace dkgNode.Services
{
    // gRPC сервер
    // Здесь "сложены" параметры узла, которые нужны и клиенту и серверу
    class DkgNodeServer : DkgNodeBase
    {
        private IGroup G { get; }
        internal string Name { get; }
        internal IScalar PrivateKey { get; }  // Приватный ключ этого узла  
        internal IPoint PublicKey { get; }    // Публичный ключ этого узла
        internal PriShare? SecretShare { get; set; } = null;
        internal DkgNodeConfig[] Configs { get; set; } = [];

        // Distributed Key Generator 
        public DistKeyGenerator? Dkg { get; set; } = null;
        // Защищает Dkg от параллельной обработки наскольких запросов
        private readonly object dkgLock = new() { };

        // Node status
        private NStatus Status { get; set; } = NotRegistered;
        private int? Round { get; set; } = null;
        private IPoint? DistributedPublicKey = null; // Distributed public key
        private readonly object stsLock = new() { };

        public void SetStatus(NStatus status)
        {
            lock (stsLock)
            {
                Status = status;
            }
        }
        public void SetStatusAndRound(NStatus status, int round)
        {
            lock (stsLock)
            {
                Status = status;
                Round = round;
            }
        }
        public void SetStatusClearRound(NStatus status)
        {
            lock (stsLock)
            {
                Status = status;
                Round = null;
            }
        }
        public NStatus GetStatus()
        {
            lock (stsLock)
            {
                return Status;
            }
        }

        public int? GetRound()
        {
            lock (stsLock)
            {
                return Round;
            }
        }

        public void SetDistributedPublicKey(IPoint? dpk)
        {
            lock (stsLock)
            {
                DistributedPublicKey = dpk;
            }
        }

        public IPoint? GetDistributedPublicKey()
        {
            IPoint? dpk = null;
            lock (stsLock)
            {
                dpk = DistributedPublicKey;
            }
            return dpk;
        }

        // Cipher
        internal IPoint C1 { get; set; }
        internal IPoint C2 { get; set; }

        private readonly ILogger _logger;
        public DkgNodeServer(ILogger logger, string name, IGroup group)
        {
            _logger = logger;
            G = group;
            C1 = G.Point();
            C2 = G.Point();
            Name = name;
            PrivateKey = G.Scalar();
            PublicKey = G.Base().Mul(PrivateKey);
        }

        // gRPC сервер реализует 4 метода
        //
        // Выдача публичного ключа
        // ProcessDeal
        // ProcessResponse
        // Прием сообщения
        // Частичная расшифровка
        public override Task<PublicKeyReply> GetPublicKey(PublicKeyRequest _, ServerCallContext context)
        {
            PublicKeyReply resp = new() { Data = ByteString.CopyFrom(PublicKey.GetBytes()) };
            return Task.FromResult(resp);
        }

        public override Task<ProcessDealReply> ProcessDeal(ProcessDealRequest deal, ServerCallContext context)
        {
            ProcessDealReply resp = new ProcessDealReply(); ;

            bool proceed = false;
            bool proceed2 = false;
            int attempt = 0;
            const int maxAttempts = 5;
            const int timeout = 1000;

            lock (stsLock)
            {
                if (deal.RoundId == Round)
                {
                    proceed = true;
                }
            }

            if (proceed)
            {
                DistDeal distDeal = new();
                distDeal.SetBytes(deal.Data.ToByteArray());

                while (!proceed2)
                {
                    lock (dkgLock)
                    {
                        ByteString data = ByteString.CopyFrom([]);
                        if (Dkg != null)
                        {
                            try
                            {
                                data = ByteString.CopyFrom(Dkg.ProcessDeal(distDeal).GetBytes());
                                resp = new ProcessDealReply { Data = data };
                                proceed2 = true;
                                _logger.LogDebug("'{Name}': ProcessDeal request [Round: {RoundId}]", Name, Round);
                            }
                            catch (Exception ex)
                            {
                                // Ошибки на данном этапе не являются фатальными
                                // Если response'а нет, это просто значит, что в дальнейшую обработку ничего не уйдёт. 
                                _logger.LogDebug("'{Name}': ProcessDeal request failed [Round: {RoundId}],\n {Message}", Name, Round, ex.Message);
                            }
                        }
                        else
                        {
                            _logger.LogDebug("'{Name}': DistKeyGenerator is null [Round: {RoundId}], timeout {attempt} of {N}", Name, Round, attempt + 1, maxAttempts);
                        }
                    }
                    if (attempt++ > maxAttempts)
                    {
                        proceed2 = true;
                        _logger.LogDebug("'{Name}': DistKeyGenerator was not created after {N} timeouts [Round: {RoundId}]", Name, Round, maxAttempts);
                    }
                    else
                    {
                        Thread.Sleep(timeout);
                    }
                }
            }
            else
            {
                _logger.LogError("'{Name}': ProcessDeal request Id mismatch [Node round: {Round}, Request round: {RoundId}]",
                    Name, Round, deal.RoundId);
            }

            return Task.FromResult(resp);
        }
        public override Task<ProcessResponseReply> ProcessResponse(ProcessResponseRequest response, ServerCallContext context)
        {
            bool proceed = false;
            bool res = false;  
            lock (stsLock)
            {
                if (response.RoundId == Round)
                {
                    proceed = true;
                }
            }

            if (proceed)
            {
                DistResponse distResponse = new();
                distResponse.SetBytes(response.Data.ToByteArray());

                    lock (dkgLock)
                    {
                        ByteString data = ByteString.CopyFrom([]);
                        if (Dkg != null)
                        {
                            try
                            {
                                DistJustification? distJust = Dkg.ProcessResponse(distResponse);
                                string anno = "no justification";
                                if (distJust != null)
                                {
                                    anno = "with jsutification";
                                }
                                //    data = ByteString.CopyFrom(distJust.GetBytes());
                                _logger.LogDebug("'{Name}': ProcessResponse request [Round: {RoundId}, {anno}]", Name, Round, anno);
                                res = true;
                            }
                            catch (Exception ex)
                            {
                                // Ошибки на данном этапе не являются фатальными
                                // Если response не удалось обработать, это значит, что он не учитывается. Как будто и не было.
                                _logger.LogDebug("'{Name}': ProcessResponse request failed [Round: {RoundId}],\n{Message}", Name, Round, ex.Message);
                            }
                        }
                }
            }
            else
            {
                _logger.LogError("'{Name}': ProcessResponse request Id mismatch [Node round: {Round}, Request round: {RoundId}]", 
                    Name, Round, response.RoundId);
            }
            return Task.FromResult(new ProcessResponseReply { Res = res });
        }

        public override Task<RunRoundReply> RunRound(RunRoundRequest request, ServerCallContext context)
        {
            bool res = false;
            DkgNodeConfig[] configs = request.DkgNodeRefs.Select(nodeRef => new DkgNodeConfig
            {
                Port = nodeRef.Port,
                Host = nodeRef.Host,
                PublicKey = nodeRef.PublicKey,
            }).ToArray();

            lock (stsLock)
            {
                if (request.RoundId == Round)
                {
                    Status = Running;
                    Configs = configs;
                    res = true;
                }
            }

            if (!res)
            {
                _logger.LogError("'{Name}': RunRound request Id mismatch [Node round: {Round}, Request round: {RoundId}]", Name, Round, request.RoundId);
            }
            else
            {
                _logger.LogInformation("'{Name}': RunRound request [Round: {request.RoundId}]", Name, request.RoundId);
            }
            return Task.FromResult(new RunRoundReply() { Res = res });
        }

        public override Task<EndRoundReply> EndRound(EndRoundRequest request, ServerCallContext context)
        {
            bool res = false;
            lock (stsLock)
            {
                if (request.RoundId == Round)
                {
                    Status = NotRegistered;
                    Round = null;
                    DistributedPublicKey = null;
                    res = true;
                }
            }

            EndRoundReply endRoundReply = new EndRoundReply() { Res = res };

            if (!res)
            {
                _logger.LogError("'{Name}': EndRound request Id mismatch [Node round: {Round}, Request round: {RoundId}]", Name, Round, request.RoundId);
            }
            else
            {
                _logger.LogInformation("'{Name}': EndRound request [Round: {request.RoundId}]", Name, request.RoundId);
            }
            return Task.FromResult(endRoundReply);
        }

        public override Task<RoundResultReply> RoundResult(RoundResultRequest request, ServerCallContext context)
        {
            bool res = false;
            IPoint? dpk = null;
            lock (stsLock)
            {
                if (request.RoundId == Round)
                {
                    dpk = DistributedPublicKey;
                    res = true;
                }
            }

            RoundResultReply roundResultReply = new RoundResultReply() { Res = (res && dpk != null) };

            if (!res)
            {
                _logger.LogError("'{Name}': RoundResult request Id mismatch [Node round: {Round}, Request round: {RoundId}]", Name, Round, request.RoundId);
            }
            else
            {
                string anno = "no result";
                if (dpk != null)
                {
                    roundResultReply.DistributedPublicKey = ByteString.CopyFrom(dpk.GetBytes());
                    anno = "result";
                }
                _logger.LogDebug("'{Name}' RoundResult request [Round: {RoundId}] returning {anno}", Name, request.RoundId, anno);
            }
            return Task.FromResult(roundResultReply);
        }
    }
}
