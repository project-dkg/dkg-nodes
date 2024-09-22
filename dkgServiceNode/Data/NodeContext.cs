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

using Microsoft.EntityFrameworkCore;
using dkgServiceNode.Models;
using dkgServiceNode.Services.Cache;
using dkgCommon.Constants;

namespace dkgServiceNode.Data
{
    public class NodeContext : DbContext
    {
        private readonly ILogger _logger;
        private readonly NodesCache _nodesCache;
        private readonly NodesRoundHistoryCache _nodesRoundHistoryCache;

        public NodeContext(
            DbContextOptions<NodeContext> options,
            NodesCache nc,
            NodesRoundHistoryCache nrhc,
            ILogger<NodeContext> lggr) : base(options) 
        {
            _logger = lggr;
            _nodesCache = nc;
            _nodesRoundHistoryCache = nrhc;
        }
        public DbSet<Node> Nodes { get; set; }
        public async Task<bool> ExistsAsync(string address)
        {
            return await Nodes.AnyAsync(e => e.Address == address);
        }

        public async Task<bool> DeleteAsync(string address)
        {
            return await Nodes.AnyAsync(e => e.Address == address);
        }

        public async Task DeleteAsync(Node node)
        {
            try
            {
                _nodesRoundHistoryCache.UpdateNodeCounts(node.RoundId, node.Status, null, NStatus.NotRegistered);
                Nodes.Remove(node);
                await SaveChangesAsync();
                _nodesCache.DeleteNodeFromCache(node);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error deleting node: {msg}", ex.Message);
            }
        }

    }
}
