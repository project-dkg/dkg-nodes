﻿// Copyright (C) 2024 Maxim [maxirmx] Samsonov (www.sw.consulting)
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
        private  readonly Dictionary<string, List<NodesRoundHistory>> _cacheNodesRoundHistory = [];
        private  readonly object _cacheNodesRoundHistoryLock = new();
        private  readonly object _cacheCountsRoundHistoryLock = new();

        private readonly Dictionary<(int RoundId, NStatus Status), int> _nodeCounts = [];

        public void SetHistoryCounterNoLock(int roundId, NStatus status, int count)
        {
           _nodeCounts[(roundId, status)] = count;
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

        public void SaveNodesRoundHistoryToCacheNoLock(NodesRoundHistory history)
        {
            if (!_cacheNodesRoundHistory.ContainsKey(history.NodeAddress))
            {
                _cacheNodesRoundHistory[history.NodeAddress] = [];
            }
            _cacheNodesRoundHistory[history.NodeAddress].Add(new NodesRoundHistory(history));
        }

        public  void SaveNodesRoundHistoryToCache(NodesRoundHistory history)
        {
            lock (_cacheNodesRoundHistoryLock)
            {
                if (!_cacheNodesRoundHistory.TryGetValue(history.NodeAddress, out List<NodesRoundHistory>? histories))
                {
                    _cacheNodesRoundHistory[history.NodeAddress] = [new NodesRoundHistory(history)];
                }
                else
                {
                    // Find the index of the existing record in the list
                    int index = histories.FindIndex(nrh => nrh.RoundId == history.RoundId);

                    // If the existing record is found, replace it with the new history
                    if (index != -1)
                    {
                        _cacheNodesRoundHistory[history.NodeAddress][index] = new NodesRoundHistory(history);
                    }
                    else
                    {
                        // Add the new history to the list if it doesn't exist
                        _cacheNodesRoundHistory[history.NodeAddress].Add(new NodesRoundHistory(history));

                        // Remove excess items if the list has more than 10 items
                        if (_cacheNodesRoundHistory[history.NodeAddress].Count > 10)
                        {
                            // Find the two items with the largest RoundId
                            var largestRoundIds = _cacheNodesRoundHistory[history.NodeAddress]
                                .OrderByDescending(nrh => nrh.RoundId)
                                .Take(2)
                                .Select(nrh => nrh.RoundId)
                                .ToList();

                            // Remove all items except the two with the largest RoundId
                            _cacheNodesRoundHistory[history.NodeAddress] = _cacheNodesRoundHistory[history.NodeAddress]
                                .Where(nrh => largestRoundIds.Contains(nrh.RoundId))
                                .ToList();
                        }
                    }
                }
            }
        }

        public  NodesRoundHistory? GetLastNodeRoundHistory(string nodeAddress, int RoundId)
        {
            NodesRoundHistory? res = null;
            lock (_cacheNodesRoundHistoryLock)
            {
                if (_cacheNodesRoundHistory.TryGetValue(nodeAddress, out List<NodesRoundHistory>? histories))
                {
                    var history = histories.Where(nrh => nrh.RoundId != RoundId)
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
        public  bool CheckNodeQualification(string nodeAddress, int previousRoundId)
        {
            return previousRoundId > 0 && GetNodeRandomForRound(nodeAddress, previousRoundId) != null;
        }
        public  int? GetNodeRandomForRound(string nodeAddress, int roundId)
        {
            int? res = null;
            lock (_cacheNodesRoundHistoryLock)
            {
                if (_cacheNodesRoundHistory.TryGetValue(nodeAddress, out List<NodesRoundHistory>? histories))
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