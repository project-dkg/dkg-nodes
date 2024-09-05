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
        public DkgContext(DbContextOptions<DkgContext> options,
                          NodesCache nc,
                          RoundsCache rc,  
                          NodesRoundHistoryCache nrhc) : base(options)
        {
            nodesCache = nc;
            roundsCache = rc;
            nodesRoundHistoryCache = nrhc;
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
        public Node? GetNodeById(int id) => nodesCache.GetNodeById(id);
        public Node? GetNodeByPublicKey(string publicKey) => nodesCache.GetNodeByPublicKey(publicKey);
        public Node? GetNodeByAddress(string address) => nodesCache.GetNodeByAddress(address);
        public async Task AddNodeAsync(Node node)
        {
            Nodes.Add(node);
            await SaveChangesAsync();
            nodesCache.LoadNodeToCache(node);
            nodesRoundHistoryCache.LoadNodesRoundHistoryToCache(new NodesRoundHistory(node));
            nodesRoundHistoryCache.UpdateNodeCounts(null, NStatus.NotRegistered, node.RoundId, node.Status);
        }
        public List<Node> GetAllNodes() => nodesCache.GetAllNodes();
        public int GetNodeCount() => nodesCache.GetNodeCount();
        public List<Node> GetAllNodesSortedById() => nodesCache.GetAllNodesSortedById();
        public List<Node> GetFilteredNodes(string search = "") => nodesCache.GetFilteredNodes(search);
        public async Task UpdateNodeAsync(Node node)
        {
            NodesRoundHistory? nrh = GetLastNodeRoundHistory(node.Id, -1);
            nodesRoundHistoryCache.UpdateNodeCounts(nrh?.RoundId, nrh != null ? nrh.NodeFinalStatus : NStatus.NotRegistered, 
                                                    node.RoundId, node.Status);

            Entry(node).State = EntityState.Modified;
            await SaveChangesAsync();

            nodesCache.UpdateNodeInCache(node);
            // possible race condition (:)

            nodesRoundHistoryCache.LoadNodesRoundHistoryToCache(new NodesRoundHistory(node));
        }

        public async Task DeleteNodeAsync(Node node)
        {
            nodesRoundHistoryCache.UpdateNodeCounts(node.RoundId, node.Status, null, NStatus.NotRegistered);
            Nodes.Remove(node);
            await SaveChangesAsync();
            nodesCache.DeleteNodeFromCache(node);
        }

        public Round? GetRoundById(int id) => roundsCache.GetRoundById(id);
        public List<Round> GetAllRounds() => roundsCache.GetAllRounds();
        public List<Round> GetAllRoundsSortedByIdDescending() => roundsCache.GetAllRoundsSortedByIdDescending();
        public async Task AddRoundAsync(Round round)
        {
            Rounds.Add(round);
            await SaveChangesAsync();
            roundsCache.AddRoundToCache(round);
        }
        public async Task UpdateRoundAsync(Round round)
        {
            Rounds.Update(round);
            await SaveChangesAsync();
            roundsCache.UpdateRoundInCache(round);
        }
        public async Task DeleteRoundAsync(Round round)
        {
            Rounds.Remove(round);
            await SaveChangesAsync();
            roundsCache.DeleteRoundFromCache(round.Id);
        }
        public bool RoundExists(int id) => roundsCache.RoundExists(id);
        public int? LastRoundResult() => roundsCache.LastRoundResult();
        public NodesRoundHistory? GetLastNodeRoundHistory(int nodeId, int currentRoundId) => nodesRoundHistoryCache.GetLastNodeRoundHistory(nodeId, currentRoundId);
        public bool CheckNodeQualification(int nodeId, int previousRoundId) => nodesRoundHistoryCache.CheckNodeQualification(nodeId, previousRoundId);
        public int? GetNodeRandomForRound(int nodeId, int roundId) => nodesRoundHistoryCache.GetNodeRandomForRound(nodeId, roundId);

    }
}
