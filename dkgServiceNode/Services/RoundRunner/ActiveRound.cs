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
using dkg.group;
using dkg.poly;
using dkg.vss;

using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("dkgNodesTests")]


namespace dkgServiceNode.Services.RoundRunner
{
    public class ActiveRound
    {
        internal ActiveNode[]? Nodes { get; set; } = null;
        internal Round Round { get; set; }
        public int Id { get { return Round.Id; } }
        internal readonly ILogger<ActiveRound> Logger;
        internal bool ForceProcessDeals { get; set; } = false;
        internal bool ForceProcessResponses { get; set; } = false;
        internal string[] StepOneData { get; set; } = [];
        internal string[] StepThreeData { get; set; } = [];
        internal DateTime Step2StartWaitingTime { get; set; } = DateTime.MinValue;
        internal DateTime Step3StartWaitingTime { get; set; } = DateTime.MinValue;
        internal DateTime ResultStartWaitingTime { get; set; } = DateTime.MinValue;

        public ActiveRound(Round rnd, ILogger<ActiveRound> lgr)
        {
            Logger = lgr;
            Round = rnd;
            Logger.LogDebug("ActiveRound [{Id}]: Create", Id);
        }
        public void Clear()
        {
            Logger.LogDebug("ActiveRound [{Id}]: Clear", Id);
            Nodes = null;
            ForceProcessDeals = false;
            ForceProcessResponses = false;
            StepOneData = [];
            StepThreeData = [];
            Step2StartWaitingTime = DateTime.MinValue;
            Step3StartWaitingTime = DateTime.MinValue;
            ResultStartWaitingTime = DateTime.MinValue;
        }
        public int? GetResult()
        {
            int? result = null;
            if (Nodes != null)
            {
                try
                {
                    List<PriShare> shares = [];
                    foreach (var node in Nodes)
                    {
                        if (node?.SecretShare != null)
                        {
                            shares.Add(node.SecretShare);
                        }
                    }
                    IScalar secretKey = PriPoly.RecoverSecret(new Secp256k1Group(), [.. shares], VssTools.MinimumT(Nodes.Length));
                    result = BitConverter.ToInt32(secretKey.GetBytes(), 0);
                }
                catch (Exception ex)
                {
                    Logger.LogInformation("ActiveRound [{Id}]: GetResult failed\n{message}", Id, ex.Message);
                }
            }

            Logger.LogDebug("ActiveRound [{Id}]: GetResult returning {result}", Id, result);
            return result;
        }
        public string[] GetStepOneData()
        {
            Logger.LogDebug("ActiveRound [{Id}]: GetStepOneData", Id);
            return StepOneData;
        }
        public string[] GetStepTwoData(Node node)
        {
            string[] result = [];
            Logger.LogDebug("ActiveRound [{Id}]: GetStepTwoData for node [{node}]", Id, node);
            if (Nodes != null)
            {
                int activeNodeIndex = FindNodeIndex(node);
                if (activeNodeIndex != -1)
                {
                    result = new string[Nodes.Length];
                    for (int i = 0; i < Nodes.Length; i++)
                    {
                        if (Nodes[i].Deals != null)
                        {
                            result[i] = Nodes[i].Deals![activeNodeIndex];
                        }
                        else
                        {
                            result[i] = string.Empty;
                        }
                    }
                }
            }
            return result;
        }
        public string[] GetStepThreeData(Node node)
        {
            Logger.LogDebug("ActiveRound [{Id}]: GetStepThreeData for node [{node}]", Id, node);
            if (Nodes != null)
            {
                if (StepThreeData.Length == 0)
                {
                    StepThreeData = new string[Nodes.Length * Nodes.Length];
                    int index = 0;
                    for (int i = 0; i < Nodes!.Length; i++)
                    {
                        if (Nodes[i].Responses != null)
                        {
                            for (int j = 0; j < Nodes[i].Responses!.Length; j++)
                            {
                                StepThreeData[index] = Nodes[i].Responses![j];
                                index++;
                            }
                        }
                        else
                        {
                            StepThreeData[index] = string.Empty;
                            index++;
                        }
                    }
                }
            }
            return StepThreeData;
        }

        public bool IsResultReady()
        {
            bool result = true;

            if (Nodes != null)
            {
                int failed = Nodes.Count(node => node.Failed);
                int finished = Nodes.Count(node => node.Finished);
                int timedOut = Nodes.Count(node => node.TimedOut);

                if ((ResultStartWaitingTime == DateTime.MinValue || DateTime.Now - ResultStartWaitingTime < TimeSpan.FromSeconds(Round.TimeoutR)) &&
                    VssTools.MinimumT(Nodes.Length) > finished &&
                    finished + failed + timedOut < Nodes.Length)
                {
                    result = false;
                }
            }
            return result;
        }
        public bool IsStepTwoDataReady()
        {
            bool result = true;
            bool force = ForceProcessDeals || 
                (Step2StartWaitingTime != DateTime.MinValue && DateTime.Now - Step2StartWaitingTime >= TimeSpan.FromSeconds(Round.Timeout2));

            if (Nodes != null)
            {
                foreach (var node in Nodes)
                {
                    if (node.Deals == null && !node.Failed)
                    {
                        if (!force)
                        {
                            result = false;
                            break;
                        }
                        else
                        {
                            node.SetTimedOut();
                        }
                    }
                }
            }
            return result;
        }
        public bool IsStepThreeDataReady()
        {
            bool result = true;
            bool force = ForceProcessResponses ||
                    (Step3StartWaitingTime != DateTime.MinValue && DateTime.Now - Step3StartWaitingTime >= TimeSpan.FromSeconds(Round.Timeout3));

            if (Nodes != null)
            {
                foreach (var node in Nodes)
                {
                    if (node.Responses == null && !node.Failed && !node.TimedOut)
                    {
                        if (!force)
                        {
                            result = false;
                            break;
                        }
                        else
                        {
                            node.SetTimedOut();
                        }
                    }
                }
            }
            return result;
        }

        public void ProcessDeals() => ForceProcessDeals = true;
        public void ProcessResponses() => ForceProcessResponses = true;

        public void Run(List<Node> nodes)
        {
            Logger.LogDebug("ActiveRound [{Id}]: Run for {count} nodes", Id, nodes.Count);
            try
            {
                Nodes = new ActiveNode[nodes.Count];
                int j = 0;
                foreach (Node node in nodes)
                {
                    Nodes[j] = new ActiveNode(Id, node, Logger);
                    j++;
                }
                SetStepOneData();
            }
            catch (Exception ex)
            {
                Logger.LogError("ActiveRound [{Id}]: Run exception at RunRound\n{message}", Id, ex.Message);
                Clear();
            }
        }
        public void SetNoResult(Node node)
        {
            ActiveNode? activeNode = FindNode(node);
            activeNode?.SetNoResult();
        }
        public void SetResult(Node node, string[] data)
        {
            ActiveNode? activeNode = FindNode(node);
            activeNode?.SetResult(data);
        }
        public void SetResultWaitingTime()
        {
            if (ResultStartWaitingTime == DateTime.MinValue)
            {
                ResultStartWaitingTime = DateTime.Now;
            }
        }
        public void SetStepOneData()
        {
           Logger.LogDebug("ActiveRound [{Id}]: SetStepOneData", Id);
            if (Nodes != null)
            {
                StepOneData = new string[Nodes.Length];
                for (int i = 0; i <Nodes.Length; i++)
                {
                    StepOneData[i] = Nodes[i]?.Key ?? "";
                }
            }
        }
        public void SetStepTwoData(Node node, string[] data) => SetStepData(node, data, (node, data) => { node.Deals = data; }, "SetStepTwoData");
        public void SetStepTwoWaitingTime()
        {
            if (Step2StartWaitingTime == DateTime.MinValue)
            {
                Step2StartWaitingTime = DateTime.Now;
            }
        }
        public void SetStepThreeData(Node node, string[] data) => SetStepData(node, data, (node, data) => { node.Responses = data; }, "SetStepThreeData");

        public void SetStepThreeWaitingTime()
        {
            if (Step3StartWaitingTime == DateTime.MinValue)
            {
                Step3StartWaitingTime = DateTime.Now;
            }
        }
        private ActiveNode? FindNode(Node node)
        {
            ActiveNode? result = null;
            if (Nodes != null)
            {
                foreach (var activeNode in Nodes)
                {
                    if (activeNode == node)
                    {
                        result = activeNode;
                        break;
                    }
                }
            }
            return result;
        }
        private int FindNodeIndex(Node node)
        {
            int result = -1;

            if (Nodes != null)
            {
                for (int i = 0; i < Nodes.Length; i++)
                {
                    if (Nodes[i] == node)
                    {
                        result = i;
                        break;
                    }
                }
            }
            return result;
        }
        private void SetStepData(Node node, string[] data, Action<ActiveNode, string[]> setDataAction, string name)
        {
            Logger.LogDebug("ActiveRound [{Id}]: {name} for node [{node}]", Id, name, node);

            ActiveNode? activeNode = FindNode(node);
            if (activeNode is not null)
            {
                setDataAction(activeNode, data);
            }
        }
    }
}
