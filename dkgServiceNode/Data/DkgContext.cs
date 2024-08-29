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
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace dkgServiceNode.Data
{
    public class DkgContext : DbContext
    {
        private static readonly ConcurrentDictionary<int, Node> _cacheNodes = [];
        private static readonly ConcurrentDictionary<string, int> _publicKeyToId = [];
        private static readonly ConcurrentDictionary<string, int> _addressToId = [];
        private static bool _isCacheNodesLoaded = false;
        private static readonly object _cacheNodesLock = new object();

        private DbSet<Node> Nodes { get; set; }

        public DkgContext(DbContextOptions<DkgContext> options) : base(options)
        {
            // Ensure cache is loaded only once
            if (!_isCacheNodesLoaded)
            {
                lock (_cacheNodesLock)
                {
                    if (!_isCacheNodesLoaded)
                    {
                        LoadNodesToCache();
                        _isCacheNodesLoaded = true;
                    }
                }
            }
            if (!_isCacheRoundsLoaded)
            {
                lock (_cacheRoundsLock)
                {
                    if (!_isCacheRoundsLoaded)
                    {
                        LoadRoundsToCache();
                        _isCacheRoundsLoaded = true;
                    }
                }
            }
        }
        private static void LoadNodeToCache(Node node)
        {
            _cacheNodes[node.Id] = node;
            _publicKeyToId[node.PublicKey] = node.Id;
            _addressToId[node.Address] = node.Id;
        }
        private void LoadNodesToCache()
        {
            foreach (var node in Nodes)
            {
                LoadNodeToCache(node);
            }
        }
        public Node? GetNodeById(int id)
        {
            _cacheNodes.TryGetValue(id, out Node? node);
            return node;
        }

        public Node? GetNodeByPublicKey(string publicKey)
        {
            if (_publicKeyToId.TryGetValue(publicKey, out var id))
            {
                return GetNodeById(id);
            }
            return null;
        }

        public Node? GetNodeByAddress(string address)
        {
            if (_addressToId.TryGetValue(address, out var id))
            {
                return GetNodeById(id);
            }
            return null;
        }
        public async Task AddNodeAsync(Node node)
        {
            Nodes.Add(node);
            await SaveChangesAsync();
            LoadNodeToCache(node);
        }

        public List<Node> GetAllNodes()
        {
            return _cacheNodes.Values.ToList();
        }

        public int GetNodeCount()
        {
            return _cacheNodes.Count;
        }
        public List<Node> GetAllNodesSortedById()
        {
            var sortedNodes = _cacheNodes.OrderBy(kvp => kvp.Key)
                                    .Select(kvp => kvp.Value)
                                    .ToList();
            return sortedNodes;
        }

        public List<Node> GetFilteredNodes(string search = "")
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                return GetAllNodes();
            }

            var filteredNodes = _cacheNodes
                .Where(kvp =>
                    kvp.Value.Name.Contains(search) ||
                    kvp.Value.Id.ToString().Contains(search) ||
                    kvp.Value.Address.Contains(search) ||
                    (kvp.Value.RoundId != null && kvp.Value.RoundId.ToString()!.Contains(search)) ||
                    (kvp.Value.RoundId == null && ("null".Contains(search) || "--".Contains(search))) ||
                    NodeStatusConstants.GetNodeStatusById(kvp.Value.StatusValue).ToString().Contains(search))
                .Select(kvp => kvp.Value)
                .ToList();

            return filteredNodes;
        }

        private static void RemoveToIdEntries(ConcurrentDictionary<string, int> dictionary, int nodeId)
        {
            var keysToRemove = dictionary
                .Where(kvp => kvp.Value == nodeId)
                .Select(kvp => kvp.Key)
                .ToList(); 

            foreach (var key in keysToRemove)
            {
                dictionary.TryRemove(key, out _);
            }
        }
        public async Task UpdateNodeAsync(Node node)
        {
            // Update node in the database
            Entry(node).State = EntityState.Modified; 
            await SaveChangesAsync();

            // Update node in the cache
            RemoveToIdEntries(_addressToId, node.Id);
            RemoveToIdEntries(_publicKeyToId, node.Id);
            LoadNodeToCache(node);
        }

        public async Task DeleteNodeAsync(Node node)
        {
            // Remove node from the database
            Nodes.Remove(node);
            await SaveChangesAsync();

            // Remove node from the cache
            RemoveToIdEntries(_addressToId, node.Id);
            RemoveToIdEntries(_publicKeyToId, node.Id);
            _cacheNodes.TryRemove(node.Id, out _);
        }

        private DbSet<Round> Rounds { get; set; }
        private static readonly ConcurrentDictionary<int, Round> _cacheRounds = [];
        private static bool _isCacheRoundsLoaded = false;
        private static readonly object _cacheRoundsLock = new object();

        private static void LoadRoundToCache(Round round)
        {
            _cacheRounds[round.Id] = round;
        }
        private void LoadRoundsToCache()
        {
            foreach (var round in Rounds)
            {
                LoadRoundToCache(round);
            }
        }
        public Round? GetRoundById(int id)
        {
            _cacheRounds.TryGetValue(id, out Round? round);
            return round;
        }
        public List<Round> GetAllRounds()
        {
            return _cacheRounds.Values.ToList();
        }
        public List<Round> GetAllRoundsSortedByIdDescending()
        {
            var sortedRounds = _cacheRounds.OrderByDescending(kvp => kvp.Key)
                                            .Select(kvp => kvp.Value)
                                            .ToList();
            return sortedRounds;
        }
        public async Task AddRoundAsync(Round round)
        {
            Rounds.Add(round);
            await SaveChangesAsync();
            LoadRoundToCache(round);
        }
        public async Task UpdateRoundAsync(Round round)
        {
            Entry(round).State = EntityState.Modified;
            await SaveChangesAsync();

            LoadRoundToCache(round);
        }
        public async Task DeleteRoundAsync(Round round)
        {
            Rounds.Remove(round);
            await SaveChangesAsync();

            _cacheNodes.TryRemove(round.Id, out _);
        }

        public bool RoundExists(int id)
        {
            return _cacheRounds.ContainsKey(id);
        }
        public int? LastRoundResult()
        {
            Round? lastRR = _cacheRounds.Values
                .Where(r => r.Result != null)
                .OrderByDescending(r => r.Id)
                .FirstOrDefault();
            return lastRR?.Result;
        }

        public DbSet<NodesRoundHistory> NodesRoundHistory { get; set; }

        public async Task<NodesRoundHistory?> FindNodeRoundHistoryAsync(int nodeId, int roundId)
        {
            return await NodesRoundHistory.FirstOrDefaultAsync(nrh => nrh.NodeId == nodeId && nrh.RoundId == roundId);
        }

        public async Task<NodesRoundHistory?> GetLastNodeRoundHistory(int nodeId, int currentRoundId)
        {
            return await NodesRoundHistory
                .Where(nrh => nrh.NodeId == nodeId && nrh.RoundId != currentRoundId)
                .OrderByDescending(nrh => nrh.RoundId)
                .FirstOrDefaultAsync();
        }

    }
}
