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

namespace dkgServiceNode.Services.Cache
{
    public  class NodesRoundHistoryCache
    {
        private  readonly Dictionary<int, List<NodesRoundHistory>> _cacheNodesRoundHistory = [];
        private  bool _isCacheNodesRoundHistoryLoaded = false;
        private  bool _isCacheCountsRoundHistoryLoaded = false;
        private readonly object _cacheNodesRoundHistoryLock = new();
        private  readonly object _cacheCountsRoundHistoryLock = new();

        private Dictionary<(int RoundId, NStatus Status), int> _nodeCounts = [];

        public void LoadNodesRoundHistoriesToCache(IEnumerable<NodesRoundHistory> histories)
        {
            lock (_cacheNodesRoundHistoryLock)
            {
                if (!_isCacheNodesRoundHistoryLoaded)
                {
                    var maxRoundNodeHistories = histories
                        .GroupBy(nrh => nrh.NodeId)
                        .Select(group => new { NodeId = group.Key, MaxRoundId = group.Max(nrh => nrh.RoundId) })
                        .Select(maxRound => histories
                            .Where(nrh => nrh.NodeId == maxRound.NodeId && nrh.RoundId == maxRound.MaxRoundId)
                            .FirstOrDefault())
                        .ToList();

                    foreach (var history in maxRoundNodeHistories)
                    {
                        if (history != null)
                        {
                            LoadNodesRoundHistoryToCacheNoLock(history);
                        }
                    }
                    _isCacheNodesRoundHistoryLoaded = true;
                }
                lock (_cacheCountsRoundHistoryLock)
                {
                    if (!_isCacheCountsRoundHistoryLoaded)
                    {
                        _nodeCounts = histories
                            .GroupBy(n => new { n.RoundId, n.NodeFinalStatusValue })
                            .ToDictionary(g => (g.Key.RoundId, (NStatus)g.Key.NodeFinalStatusValue), g => g.Count());
                        _isCacheCountsRoundHistoryLoaded = true;
                    }
                }
            }
        }

        public void UpdateNodeCounts(int? oldRoundId, NStatus oldStatus, int? newRoundId, NStatus newStatus)
        {
            if (oldRoundId != newRoundId || oldStatus != newStatus)
            {
                lock (_cacheCountsRoundHistoryLock)
                {
                    if (oldRoundId == newRoundId && oldRoundId != null)
                    {
                        if (_nodeCounts.TryGetValue(((int)oldRoundId, oldStatus), out int oldCount))
                        {
                            _nodeCounts[((int)oldRoundId, oldStatus)] = oldCount - 1;
                        }
                    }
                    if (newRoundId != null)
                    {
                        if (_nodeCounts.TryGetValue(((int)newRoundId, newStatus), out int newCount))
                        {
                            _nodeCounts[((int)newRoundId, newStatus)] = newCount + 1;
                        }
                        else
                        {
                            _nodeCounts[((int)newRoundId, newStatus)] = 1;
                        }
                    }
                }
            }
        }

        public int GetNodeCountForRound(int roundId, NStatus status)
        {
            int res = 0;
            lock (_cacheCountsRoundHistoryLock)
            {
                if (_nodeCounts.TryGetValue((roundId, status), out int count))
                {
                    res = count;
                }
            }
            return res;
        }

        private  void LoadNodesRoundHistoryToCacheNoLock(NodesRoundHistory history)
        {
            if (!_cacheNodesRoundHistory.ContainsKey(history.NodeId))
            {
                _cacheNodesRoundHistory[history.NodeId] = new List<NodesRoundHistory>();
            }
            _cacheNodesRoundHistory[history.NodeId].Add(history);
        }

        public  void LoadNodesRoundHistoryToCache(NodesRoundHistory history)
        {
            lock (_cacheNodesRoundHistoryLock)
            {
                if (!_cacheNodesRoundHistory.TryGetValue(history.NodeId, out List<NodesRoundHistory>? histories))
                {
                    _cacheNodesRoundHistory[history.NodeId] = new List<NodesRoundHistory> { history };
                }
                else
                {
                    // Find the index of the existing record in the list
                    int index = histories.FindIndex(nrh => nrh.RoundId == history.RoundId);

                    // If the existing record is found, replace it with the new history
                    if (index != -1)
                    {
                        _cacheNodesRoundHistory[history.NodeId][index] = history;
                    }
                    else
                    {
                        // Add the new history to the list if it doesn't exist
                        _cacheNodesRoundHistory[history.NodeId].Add(history);

                        // Remove excess items if the list has more than 10 items
                        if (_cacheNodesRoundHistory[history.NodeId].Count > 10)
                        {
                            // Find the two items with the largest RoundId
                            var largestRoundIds = _cacheNodesRoundHistory[history.NodeId]
                                .OrderByDescending(nrh => nrh.RoundId)
                                .Take(2)
                                .Select(nrh => nrh.RoundId)
                                .ToList();

                            // Remove all items except the two with the largest RoundId
                            _cacheNodesRoundHistory[history.NodeId] = _cacheNodesRoundHistory[history.NodeId]
                                .Where(nrh => largestRoundIds.Contains(nrh.RoundId))
                                .ToList();
                        }
                    }
                }
            }
        }

        public  NodesRoundHistory? GetLastNodeRoundHistory(int nodeId, int currentRoundId)
        {
            NodesRoundHistory? res = null;
            lock (_cacheNodesRoundHistoryLock)
            {
                if (_cacheNodesRoundHistory.TryGetValue(nodeId, out List<NodesRoundHistory>? histories))
                {
                    var history = histories.Where(nrh => nrh.RoundId != currentRoundId)
                                           .OrderByDescending(nrh => nrh.RoundId)
                                           .FirstOrDefault();
                    if (history != null)
                    {
                        res = new NodesRoundHistory(history);
                    }
                }
            }
            return res;
        }
        public  bool CheckNodeQualification(int nodeId, int previousRoundId)
        {
            return GetNodeRandomForRound(nodeId, previousRoundId) != null;
        }
        public  int? GetNodeRandomForRound(int nodeId, int roundId)
        {
            int? res = null;
            lock (_cacheNodesRoundHistoryLock)
            {
                if (_cacheNodesRoundHistory.TryGetValue(nodeId, out List<NodesRoundHistory>? histories))
                {
                    var history = histories.Where(nrh => nrh.RoundId == roundId)
                                           .FirstOrDefault();
                    if (history != null)
                    {
                        res = history.NodeRandom;
                    }
                }
            }
            return res;
        }
    }
}