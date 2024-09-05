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
using System.Collections.Concurrent;

namespace dkgServiceNode.Services.Cache
{
    public  class NodesCache
    {
        private  readonly Dictionary<int, Node> _cacheNodes = new();
        private  readonly ConcurrentDictionary<string, int> _publicKeyToId = new();
        private  readonly ConcurrentDictionary<string, int> _addressToId = new();
        private  bool _isCacheNodesLoaded = false;
        private  readonly object _cacheNodesLock = new();

        public  void LoadNodesToCache(IEnumerable<Node> nodes)
        {
            lock (_cacheNodesLock)
            {
                if (!_isCacheNodesLoaded)
                {
                    foreach (var node in nodes)
                    {
                        LoadNodeToCacheNoLock(node);
                    }
                    _isCacheNodesLoaded = true;
                }
            }
        }

        private  void LoadNodeToCacheNoLock(Node node)
        {
            _cacheNodes[node.Id] = new Node(node);
            _publicKeyToId[node.PublicKey] = node.Id;
            _addressToId[node.Address] = node.Id;
        }

        public  void LoadNodeToCache(Node node)
        {
            lock (_cacheNodesLock)
            {
                _cacheNodes[node.Id] = new Node(node);
                _publicKeyToId[node.PublicKey] = node.Id;
                _addressToId[node.Address] = node.Id;
            }
        }

        public  Node? GetNodeById(int id)
        {
            Node? res = null;
            lock (_cacheNodesLock)
            {
                if (_cacheNodes.TryGetValue(id, out Node? node))
                {
                    res = new Node(node);
                }
            }
            return res;
        }

        public  Node? GetNodeByPublicKey(string publicKey)
        {
            if (_publicKeyToId.TryGetValue(publicKey, out var id))
            {
                return GetNodeById(id);
            }
            return null;
        }

        public  Node? GetNodeByAddress(string address)
        {
            if (_addressToId.TryGetValue(address, out var id))
            {
                return GetNodeById(id);
            }
            return null;
        }

        public  List<Node> GetAllNodes()
        {
            List<Node> copiedNodes;
            lock (_cacheNodesLock)
            {
                copiedNodes = _cacheNodes.Values.Select(node => new Node(node)).ToList();
            }
            return copiedNodes;
        }

        public  int GetNodeCount()
        {
            int res = 0;
            lock (_cacheNodesLock)
            {
                res = _cacheNodes.Count;
            }
            return res;
        }

        public  List<Node> GetAllNodesSortedById()
        {
            List<Node> copiedNodes;
            lock (_cacheNodesLock)
            {
                copiedNodes = _cacheNodes.OrderBy(kvp => kvp.Key)
                                        .Select(kvp => new Node(kvp.Value))
                                        .ToList();
            }
            return copiedNodes;
        }

        public  List<Node> GetFilteredNodes(string search = "")
        {
            return string.IsNullOrWhiteSpace(search) ? GetAllNodes() : GetFilteredNodesInternal(search);
        }

        private  List<Node> GetFilteredNodesInternal(string search)
        {
            List<Node> filteredNodes;
            lock (_cacheNodesLock)
            {
                filteredNodes = _cacheNodes
                 .Where(kvp =>
                     kvp.Value.Name.Contains(search) ||
                     kvp.Value.Id.ToString().Contains(search) ||
                     kvp.Value.Address.Contains(search) ||
                     (kvp.Value.RoundId != null && kvp.Value.RoundId.ToString()!.Contains(search)) ||
                     (kvp.Value.RoundId == null && ("null".Contains(search) || "--".Contains(search))) ||
                     NodeStatusConstants.GetNodeStatusById(kvp.Value.StatusValue).ToString().Contains(search))
                 .Select(kvp => new Node(kvp.Value)) // Copy the filtered nodes
                 .ToList();
            }
            return filteredNodes;
        }

        private  void RemoveToIdEntries(ConcurrentDictionary<string, int> dictionary, int nodeId)
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

        public  void UpdateNodeInCache(Node node)
        {
            // Update node in the cache
            RemoveToIdEntries(_addressToId, node.Id);
            RemoveToIdEntries(_publicKeyToId, node.Id);
            LoadNodeToCache(node);
        }

        public  void DeleteNodeFromCache(Node node)
        {
            // Remove node from the cache
            RemoveToIdEntries(_addressToId, node.Id);
            RemoveToIdEntries(_publicKeyToId, node.Id);
            lock (_cacheNodesLock)
            {
                _cacheNodes.Remove(node.Id);
            }
        }
    }
}
