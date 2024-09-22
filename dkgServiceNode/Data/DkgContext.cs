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
using Microsoft.EntityFrameworkCore;

namespace dkgServiceNode.Data
{
    public class DkgContext : DbContext
    {
        //private DbSet<Node> Nodes { get; set; }
        private DbSet<Round> Rounds { get; set; }

        private readonly NodesCache nodesCache;
        private readonly RoundsCache roundsCache;
        private readonly NodesRoundHistoryCache nodesRoundHistoryCache;
        private readonly NodeAddProcessor nodeRequestProcessor;
        private readonly NrhAddProcessor nrhRequestProcessor;

        private readonly ILogger logger;
        public DkgContext(DbContextOptions<DkgContext> options,
                          NodesCache nc,
                          RoundsCache rc,  
                          NodesRoundHistoryCache nrhc,
                          NodeAddProcessor nodep,
                          NrhAddProcessor nrhp,
                          ILogger<DkgContext> lggr) : base(options)
        {
            nodesCache = nc;
            roundsCache = rc;
            nodesRoundHistoryCache = nrhc;
            nodeRequestProcessor = nodep;
            nrhRequestProcessor = nrhp;

            logger = lggr;
        }
        public Node? GetNodeById(int id) => nodesCache.GetNodeById(id);
        public Node? GetNodeByAddress(string address) => nodesCache.GetNodeByAddress(address);
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

        private void SaveNodesRoundHistory(Node node)
        {
            if (node.RoundId != null && node.Random != null)
            {
                nodesRoundHistoryCache.SaveNodesRoundHistoryToCache(new NodesRoundHistory(node));
                nrhRequestProcessor.EnqueueRequest(new Node(node));
            }
        }
        public List<Node> GetAllNodes() => nodesCache.GetAllNodes();
        public int GetNodeCount() => nodesCache.GetNodeCount();
        public List<Node> GetAllNodesSortedById() => nodesCache.GetAllNodesSortedById();
        public List<Node> GetFilteredNodes(string search = "") => nodesCache.GetFilteredNodes(search);
        public void UpdateNode(Node node)
        {
            try
            {
                NodesRoundHistory? nrh = GetLastNodeRoundHistory(node.Id, -1);
                nodesRoundHistoryCache.UpdateNodeCounts(
                    nrh?.RoundId, 
                    nrh != null ? nrh.NodeFinalStatus : NStatus.NotRegistered,
                    node.RoundId, 
                    node.Status);

                SaveNodesRoundHistory(node);
                nodesCache.UpdateNodeInCache(node);
            }
            catch (Exception ex)
            {
                logger.LogError("Error updating node: {msg}", ex.Message);
            }
        }

        public Round? GetRoundById(int id) => roundsCache.GetRoundById(id);
        public List<Round> GetAllRounds() => roundsCache.GetAllRounds();
        public List<Round> GetAllRoundsSortedByIdDescending() => roundsCache.GetAllRoundsSortedByIdDescending();
        public async Task AddRoundAsync(Round round)
        {
            try
            {
                Rounds.Add(round);
                await SaveChangesAsync();
                roundsCache.SaveRoundToCache(round);
            }
            catch (Exception ex)
            {
                logger.LogError("Error adding round: {msg}", ex.Message);
            }
        }
        public async Task UpdateRoundAsync(Round round)
        {
            try
            {
                Rounds.Update(round);
                await SaveChangesAsync();
                roundsCache.UpdateRoundInCache(round);
            }
            catch (Exception ex)
            {
                logger.LogError("Error updating round: {msg}", ex.Message);
            }
        }
        public async Task DeleteRoundAsync(Round round)
        {
            try
            {
                Rounds.Remove(round);
                await SaveChangesAsync();
                roundsCache.DeleteRoundFromCache(round.Id);
            }
            catch (Exception ex)
            {
                logger.LogError("Error deleting round: {msg}", ex.Message);
            }
        }
        public bool RoundExists(int id) => roundsCache.RoundExists(id);
        public int? LastRoundResult() => roundsCache.LastRoundResult();
        public NodesRoundHistory? GetLastNodeRoundHistory(int nodeId, int RoundId) => nodesRoundHistoryCache.GetLastNodeRoundHistory(nodeId, RoundId);
        public bool CheckNodeQualification(int nodeId, int previousRoundId) => nodesRoundHistoryCache.CheckNodeQualification(nodeId, previousRoundId);
        public int? GetNodeRandomForRound(int nodeId, int roundId) => nodesRoundHistoryCache.GetNodeRandomForRound(nodeId, roundId);

    }
}
