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

using dkgCommon.Constants;
using dkgServiceNode.Models;
using dkgServiceNode.Services.Cache;
using dkgServiceNode.Services.RequestProcessors;

namespace dkgServiceNode.Data
{
    public class NodeCompositeContext
    {
        private readonly NodesCache nodesCache;
        private readonly NodesRoundHistoryCache nodesRoundHistoryCache;
        private readonly NodeAddProcessor nodeRequestProcessor;
        private readonly NrhAddProcessor nrhRequestProcessor;

        private readonly ILogger logger;
        public NodeCompositeContext(
                          NodesCache nc,
                          NodesRoundHistoryCache nrhc,
                          NodeAddProcessor nodep,
                          NrhAddProcessor nrhp,
                          ILogger<RoundContext> lggr)
        {
            nodesCache = nc;
            nodesRoundHistoryCache = nrhc;
            nodeRequestProcessor = nodep;
            nrhRequestProcessor = nrhp;

            logger = lggr;
        }
        public Node? GetNodeByAddress(string address) => nodesCache.GetNodeByAddress(address);
        public List<Node> GetAllNodes() => nodesCache.GetAllNodes();
        public int GetNodeCount() => nodesCache.GetNodeCount();
        public List<Node> GetAllNodesSortedById() => nodesCache.GetAllNodesSortedById();
        public List<Node> GetFilteredNodes(string search = "") => nodesCache.GetFilteredNodes(search);
        public NodesRoundHistory? GetLastNodeRoundHistory(string nodeAddress, int RoundId) => nodesRoundHistoryCache.GetLastNodeRoundHistory(nodeAddress, RoundId);
        public bool CheckNodeQualification(string nodeAddress, int previousRoundId) => nodesRoundHistoryCache.CheckNodeQualification(nodeAddress, previousRoundId);
        public int? GetNodeRandomForRound(string nodeAddress, int roundId) => nodesRoundHistoryCache.GetNodeRandomForRound(nodeAddress, roundId);
        public void RegisterNode(Node node)
        {
            try
            { 
                nodeRequestProcessor.EnqueueRequest(new Node(node));
                nodesCache.SaveNodeToCache(node);
                SaveNodesRoundHistory(node);
                nodesRoundHistoryCache.UpdateNodeCounts(null, NStatus.NotRegistered, node.RoundId, node.Status);
            }
            catch (Exception ex)
            {
                logger.LogError("Error adding node: {msg}", ex.Message);
            }
        }
        public void UpdateNode(Node node, bool fullUpdate = false, bool regCompletion = false)
        {
            try
            {
                NodesRoundHistory? nrh = GetLastNodeRoundHistory(node.Address, -1);
                if (regCompletion)
                {
                    nodesRoundHistoryCache.UpdateNodeCounts(
                        node.RoundId,
                        NStatus.WaitingRegistration,
                        node.RoundId,
                        node.Status);

                }
                else
                {
                    nodesRoundHistoryCache.UpdateNodeCounts(
                        nrh?.RoundId,
                        nrh != null ? nrh.NodeFinalStatus : NStatus.NotRegistered,
                        node.RoundId,
                        node.Status);
                }

                SaveNodesRoundHistory(node);
                if (fullUpdate)
                {
                    nodesCache.UpdateNodeInCache(node);
                }
                else
                {
                    nodesCache.UpdateNodeInCache(node.Address, node.Status, node.RoundId);
                }
            }
            catch (Exception ex)
            {
                logger.LogError("Error updating node: {msg}", ex.Message);
            }
        }

        private void SaveNodesRoundHistory(Node node)
        {
            if (node.RoundId != null && 
                node.Random != null &&
                node.Status != NStatus.NotRegistered &&
                node.Status != NStatus.WaitingRegistration)
            {
                nodesRoundHistoryCache.SaveNodesRoundHistoryToCache(new NodesRoundHistory(node));
                nrhRequestProcessor.EnqueueRequest(new Node(node));
            }
        }

        public void FinalizeRegistration(Node node)
        {
            node.Status = NStatus.WaitingRoundStart;
            UpdateNode(node, false, true);
        }
    }
}
