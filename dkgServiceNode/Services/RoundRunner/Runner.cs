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
    public class Runner
    {
        private readonly ILogger<ActiveRound> _logger;
        private List<ActiveRound> ActiveRounds { get; set; } = [];
        private readonly object lockObject = new();

        public Runner(ILogger<ActiveRound> logger)
        {
            _logger = logger;
        }
        public void StartRound(Round round)
        {
            lock (lockObject)
            {
                ActiveRounds.Add(new ActiveRound(round, _logger));
            }
        }

        public void RunRound(Round round, List<Node>? nodes)
        {
            ActiveRound? roundToRun = null;
            lock (lockObject)
            {
                roundToRun = ActiveRounds.First(r => r.Id == round.Id);
                if (roundToRun != null && nodes != null)
                {
                    roundToRun.Run(nodes);
                }
            }
        }

        public int? GetRoundResult(Round round)
        {
            ActiveRound? roundToRun = null;
            lock (lockObject)
            {
                roundToRun = ActiveRounds.First(r => r.Id == round.Id);
            }
            if (roundToRun != null)
            {
                return roundToRun.GetResult();
            }
            else
            {
                return null;
            }
        }

        public int? FinishRound(Round round, List<Node>? nodes)
        {
            int? result = GetRoundResult(round);
            RemoveRound(round, nodes);
            return result;
        }

        public void CancelRound(Round round, List<Node>? nodes)
        {
            RemoveRound(round, nodes);
        }
        internal void RemoveRound(Round round, List<Node>? nodes)
        {
            ActiveRound? roundToRemove = null;
            lock (lockObject)
            {
                roundToRemove = ActiveRounds.First(r => r.Id == round.Id);
                if (roundToRemove != null)
                {
                    roundToRemove.Clear(nodes);
                    ActiveRounds.Remove(roundToRemove);
                }
            }
        }
    }
}
