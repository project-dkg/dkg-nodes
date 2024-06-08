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

using dkgServiceNode.Models;

using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("dkgNodesTests")]

namespace dkgServiceNode.Services.RoundRunner
{
    public class Runner
    {
        internal readonly ILogger<ActiveRound> Logger;
        internal List<ActiveRound> ActiveRounds { get; set; } = [];
        private readonly object lockObject = new();

        public Runner(ILogger<ActiveRound> logger)
        {
            Logger = logger;
        }
        public bool CheckNode(Round round, Node node) => CheckNodeCondition(round, node, (roundToRun, node) => roundToRun.CheckNode(node));
        public bool CheckTimedOutNode(Round round, Node node) => CheckNodeCondition(round, node, (roundToRun, node) => roundToRun.CheckTimedOutNode(node));
        public void StartRound(Round round)
        {
            lock (lockObject)
            {
                ActiveRounds.Add(new ActiveRound(round, Logger));
            }
        }
        public void RunRound(Round round, List<Node>? nodes)
        {
            ActiveRound? roundToRun = null;
            lock (lockObject)
            {
                roundToRun = ActiveRounds.FirstOrDefault(r => r.Id == round.Id);
                if (roundToRun != null && nodes != null)
                {
                    roundToRun.Run(nodes);
                }
            }
        }
        public int? GetRoundResult(Round round)
        {
            int? res = null;
            lock (lockObject)
            {
                ActiveRound? roundToRun = ActiveRounds.FirstOrDefault(r => r.Id == round.Id);
                if (roundToRun != null)
                {
                    res = roundToRun.GetResult();
                }
            }
            return res;
        }

        public int? FinishRound(Round round)
        {
            int? result = GetRoundResult(round);
            RemoveRound(round);
            return result;
        }

        internal void Process(Round round, Action<ActiveRound> processAction)
        {
            ActiveRound? roundToRun = null;
            lock (lockObject)
            {
                roundToRun = ActiveRounds.FirstOrDefault(r => r.Id == round.Id);
                if (roundToRun != null)
                {
                    processAction(roundToRun);
                }
            }
        }   

        public void ProcessDeals(Round round) => Process(round, roundToRun => roundToRun.ProcessDeals());
        public void ProcessResponses(Round round) => Process(round, roundToRun => roundToRun.ProcessResponses());

        public void CancelRound(Round round) => RemoveRound(round);
        public void SetNoResult(Round round, Node node)
        {
            lock (lockObject)
            {
                ActiveRound? roundToRun = ActiveRounds.FirstOrDefault(r => r.Id == round.Id);
                roundToRun?.SetNoResult(node);
            }
        }
        public void SetResult(Round round, Node node, string[] data) =>
            SetStepData(round, node, data, (roundToRun, node, data) => roundToRun.SetResult(node, data));
        public void SetResultWaitingTime(Round round) =>
            SetStepDataWaitingTime(round, (roundToRun) => roundToRun.SetResultWaitingTime());
        public void SetStepTwoData(Round round, Node node, string[] data) =>
            SetStepData(round, node, data, (roundToRun, node, data) => roundToRun.SetStepTwoData(node, data));
        public void SetStepTwoWaitingTime(Round round) =>
            SetStepDataWaitingTime(round, (roundToRun) => roundToRun.SetStepTwoWaitingTime());
        public void SetStepThreeData(Round round, Node node, string[] data) =>
            SetStepData(round, node, data, (roundToRun, node, data) => roundToRun.SetStepThreeData(node, data));
        public void SetStepThreeWaitingTime(Round round) =>
            SetStepDataWaitingTime(round, (roundToRun) => roundToRun.SetStepThreeWaitingTime());
        public bool IsResultReady(Round round) =>
            IsStepDataReady(round, roundToRun => roundToRun.IsResultReady());
        public bool IsStepTwoDataReady(Round round) => 
            IsStepDataReady(round, roundToRun => roundToRun.IsStepTwoDataReady());
        public bool IsStepThreeDataReady(Round round) => 
            IsStepDataReady(round, roundToRun => roundToRun.IsStepThreeDataReady());
        public string[] GetStepOneData(Round round) =>
            GetStepData(round, roundToRun => roundToRun.GetStepOneData());
        public string[] GetStepTwoData(Round round, Node node) => 
            GetStepData(round, roundToRun => roundToRun.GetStepTwoData(node));
        public string[] GetStepThreeData(Round round, Node node) => 
            GetStepData(round, roundToRun => roundToRun.GetStepThreeData(node));


        // Private methods
        internal bool CheckNodeCondition(Round round, Node node, Func<ActiveRound, Node, bool> condition)
        {
            bool res = false;
            lock (lockObject)
            {
                ActiveRound? roundToRun = ActiveRounds.FirstOrDefault(r => r.Id == round.Id);
                if (roundToRun != null)
                {
                    res = condition(roundToRun, node);
                }
            }
            return res;
        }

        internal void SetStepData(Round round, Node node, string[] data, Action<ActiveRound, Node, string[]> setDataAction)
        {
            lock (lockObject)
            {
                ActiveRound? roundToRun = ActiveRounds.FirstOrDefault(r => r.Id == round.Id);
                if (roundToRun != null)
                {
                    setDataAction(roundToRun, node, data);
                }
            }
        }
        internal bool IsStepDataReady(Round round, Func<ActiveRound, bool> isDataReadyFunc)
        {
            bool res = false;
            lock (lockObject)
            {
                ActiveRound? roundToRun = ActiveRounds.FirstOrDefault(r => r.Id == round.Id);
                if (roundToRun != null && roundToRun.Nodes != null)
                {
                    res = isDataReadyFunc(roundToRun);
                }
            }
            return res;
        }

        internal string[] GetStepData(Round round, Func<ActiveRound, string[]> getDataFunc)
        {
            string[] res = [];
            lock (lockObject)
            {
                ActiveRound? roundToRun = ActiveRounds.FirstOrDefault(r => r.Id == round.Id);
                if (roundToRun != null)
                {
                    res = getDataFunc(roundToRun);
                }
            }
            return res;
        }

        internal void SetStepDataWaitingTime(Round round, Action<ActiveRound> setStepDataWaitingimeFunc)
        {
            lock (lockObject)
            {
                ActiveRound? roundToRun = ActiveRounds.FirstOrDefault(r => r.Id == round.Id);
                if (roundToRun != null)
                {
                    setStepDataWaitingimeFunc(roundToRun);
                }
            }
        }

        internal void RemoveRound(Round round)
        {
            ActiveRound? roundToRemove = null;
            lock (lockObject)
            {
                roundToRemove = ActiveRounds.FirstOrDefault(r => r.Id == round.Id);
                if (roundToRemove != null)
                {
                    roundToRemove.Clear();
                    ActiveRounds.Remove(roundToRemove);
                }
            }
        }

    }
}
