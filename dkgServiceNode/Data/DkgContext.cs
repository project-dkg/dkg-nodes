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
using Microsoft.EntityFrameworkCore;

namespace dkgServiceNode.Data
{
    public class DkgContext : DbContext
    {
        private DbSet<Node> Nodes { get; set; }
        private DbSet<Round> Rounds { get; set; }
        private DbSet<NodesRoundHistory> NodesRoundHistory { get; set; }

        private readonly NodesCache nodesCache;
        private readonly RoundsCache roundsCache;
        private readonly NodesRoundHistoryCache nodesRoundHistoryCache;

        private readonly ILogger logger;
        public DkgContext(DbContextOptions<DkgContext> options,
                          NodesCache nc,
                          RoundsCache rc,  
                          NodesRoundHistoryCache nrhc,
                          ILogger<DkgContext> lggr) : base(options)
        {
            nodesCache = nc;
            roundsCache = rc;
            nodesRoundHistoryCache = nrhc;

            logger = lggr;

            try
            {
                if (Nodes is not null)
                {
                    nodesCache.LoadNodesToCache(Nodes);
                }
                if (Rounds is not null)
                {
                    roundsCache.LoadRoundsToCache(Rounds);
                }
                if (NodesRoundHistory is not null)
                {
                    nodesRoundHistoryCache.LoadNodesRoundHistoriesToCache(NodesRoundHistory);
                }
            }
            catch (Exception ex)
            {
                logger.LogError("Error loading caches: {msg}", ex.Message);
            }
        }
        public Node? GetNodeById(int id) => nodesCache.GetNodeById(id);
        public Node? GetNodeByPublicKey(string publicKey) => nodesCache.GetNodeByPublicKey(publicKey);
        public Node? GetNodeByAddress(string address) => nodesCache.GetNodeByAddress(address);
        public async Task AddNodeAsync(Node node)
        {
            try
            { 
                Nodes.Add(node);
                await SaveChangesAsync();
                nodesCache.LoadNodeToCache(node);
                await LoadNodesRoundHistory(node);
                nodesRoundHistoryCache.UpdateNodeCounts(null, NStatus.NotRegistered, node.RoundId, node.Status);
            }
            catch (Exception ex)
            {
                logger.LogError("Error adding node: {msg}", ex.Message);
            }
        }

        private async Task LoadNodesRoundHistory(Node node)
        {
            if (node.RoundId != null && node.Random != null)
            {
                nodesRoundHistoryCache.LoadNodesRoundHistoryToCache(new NodesRoundHistory(node));

                await Database.ExecuteSqlRawAsync(
                  "CALL upsert_node_round_history({0}, {1}, {2}, {3})",
                  node.Id, node.RoundId, node.StatusValue, node.Random);
            }
        }
        public List<Node> GetAllNodes() => nodesCache.GetAllNodes();

        public int GetNodeCount() => nodesCache.GetNodeCount();
        public List<Node> GetAllNodesSortedById() => nodesCache.GetAllNodesSortedById();
        public List<Node> GetFilteredNodes(string search = "") => nodesCache.GetFilteredNodes(search);
        public async Task UpdateNodeAsync(Node node)
        {
            try
            {
                NodesRoundHistory? nrh = GetLastNodeRoundHistory(node.Id, -1);
                nodesRoundHistoryCache.UpdateNodeCounts(nrh?.RoundId, nrh != null ? nrh.NodeFinalStatus : NStatus.NotRegistered,
                                                        node.RoundId, node.Status);

                Entry(node).State = EntityState.Modified;
                await SaveChangesAsync();
                await LoadNodesRoundHistory(node);
                nodesCache.UpdateNodeInCache(node);
            }
            catch (Exception ex)
            {
                logger.LogError("Error updating node: {msg}", ex.Message);
            }
        }

        public async Task DeleteNodeAsync(Node node)
        {
            try
            {
                nodesRoundHistoryCache.UpdateNodeCounts(node.RoundId, node.Status, null, NStatus.NotRegistered);
                Nodes.Remove(node);
                await SaveChangesAsync();
                nodesCache.DeleteNodeFromCache(node);
            }
            catch (Exception ex)
            {
                logger.LogError("Error deleting node: {msg}", ex.Message);
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
                roundsCache.AddRoundToCache(round);
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
        public NodesRoundHistory? GetLastNodeRoundHistory(int nodeId, int currentRoundId) => nodesRoundHistoryCache.GetLastNodeRoundHistory(nodeId, currentRoundId);
        public bool CheckNodeQualification(int nodeId, int previousRoundId) => nodesRoundHistoryCache.CheckNodeQualification(nodeId, previousRoundId);
        public int? GetNodeRandomForRound(int nodeId, int roundId) => nodesRoundHistoryCache.GetNodeRandomForRound(nodeId, roundId);

    }
}
