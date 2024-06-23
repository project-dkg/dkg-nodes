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

using dkgServiceNode.Models;
using Microsoft.EntityFrameworkCore;

namespace dkgServiceNode.Data
{
    public class DkgContext : DbContext
    {
        public DkgContext(DbContextOptions<DkgContext> options) : base(options) { }
        public DbSet<Node> Nodes { get; set; }
        public DbSet<Round> Rounds { get; set; }
        public DbSet<NodesRoundHistory> NodesRoundHistory { get; set; }
        public async Task<bool> NodeExistsAsync(int id)
        {
            return await Nodes.AnyAsync(e => e.Id == id);
        }
        public async Task<bool> RoundExistsAsync(int id)
        {
            return await Rounds.AnyAsync(e => e.Id == id);
        }

        public async Task<int?> LastRoundResult()
        {
            Round? lastRR = await Rounds
                         .Where(r => r.Result != null)
                         .OrderByDescending(r => r.Id)
                         .FirstOrDefaultAsync();
            return lastRR?.Result;
        }

        public async Task<Node?> FindNodeByPublicKeyAsync(string publicKey)
        {
            return await Nodes.FirstOrDefaultAsync(node => node.PublicKey == publicKey);
        }
        public async Task<Node?> FindNodeByAddressAsync(string address)
        {
            return await Nodes.FirstOrDefaultAsync(node => node.Address == address);
        }
        public async Task<NodesRoundHistory?> FindNodeRoundHistoryAsync(int nodeId, int roundId)
        {
            return await NodesRoundHistory.FirstOrDefaultAsync(nrh => nrh.NodeId == nodeId && nrh.RoundId == roundId);
        }
    }
}
