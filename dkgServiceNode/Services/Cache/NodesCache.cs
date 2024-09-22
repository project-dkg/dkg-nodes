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
    public class NodesCache
    {
        private  readonly Dictionary<string, Node> _cacheNodes = new();
        private  readonly object _cacheNodesLock = new();
        public void SaveNodeToCacheNoLock(Node node)
        {
            _cacheNodes[node.Address] = new Node(node);
        }

        public void SaveNodeToCache(Node node)
        {
            lock (_cacheNodesLock)
            {
                _cacheNodes[node.Address] = new Node(node);
            }
        }

        public Node? GetNodeByAddress(string address)
        {
            Node? res = null;
            lock (_cacheNodesLock)
            {
                if (_cacheNodes.TryGetValue(address, out Node? node))
                {
                    res = new Node(node);
                }
            }
            return res;
        }

        public List<Node> GetAllNodes()
        {
            List<Node> copiedNodes;
            lock (_cacheNodesLock)
            {
                copiedNodes = _cacheNodes.Values.Select(node => new Node(node)).ToList();
            }
            return copiedNodes;
        }

        public int GetNodeCount()
        {
            int res = 0;
            lock (_cacheNodesLock)
            {
                res = _cacheNodes.Count;
            }
            return res;
        }

        public List<Node> GetAllNodesSortedById()
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

        public List<Node> GetFilteredNodes(string search = "")
        {
            return string.IsNullOrWhiteSpace(search) ? GetAllNodes() : GetFilteredNodesInternal(search);
        }

        private List<Node> GetFilteredNodesInternal(string search)
        {
            List<Node> filteredNodes;
            lock (_cacheNodesLock)
            {
                filteredNodes = _cacheNodes
                 .Where(kvp =>
                     kvp.Value.Name.Contains(search) ||
                     kvp.Value.Address.Contains(search) ||
                     (kvp.Value.RoundId != null && kvp.Value.RoundId.ToString()!.Contains(search)) ||
                     (kvp.Value.RoundId == null && ("null".Contains(search) || "--".Contains(search))) ||
                     NodeStatusConstants.GetNodeStatusById(kvp.Value.StatusValue).ToString().Contains(search))
                 .Select(kvp => new Node(kvp.Value))
                 .ToList();
            }
            return filteredNodes;
        }

        public void UpdateNodeInCache(Node node)
        {
            lock (_cacheNodesLock)
            {
                _cacheNodes[node.Address] = new Node(node);
            }
        }
        public void UpdateNodeInCache(string address, NodeStatus status)
        {
            lock (_cacheNodesLock)
            {
                if (_cacheNodes.TryGetValue(address, out Node? node))
                {
                    node.Status = status;
                }
            }
        }
        public void UpdateNodeInCache(string address, NodeStatus status, int? roundId)
        {
            lock (_cacheNodesLock)
            {
                if (_cacheNodes.TryGetValue(address, out Node? node))
                {
                    node.Status = status;
                    node.RoundId = roundId;
                }
            }
        }

        public void DeleteNodeFromCache(Node node)
        {
            lock (_cacheNodesLock)
            {
                _cacheNodes.Remove(node.Address);
            }
        }
    }
}
