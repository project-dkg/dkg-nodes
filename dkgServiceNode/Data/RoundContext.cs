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
using dkgServiceNode.Services.Cache;
using Microsoft.EntityFrameworkCore;

namespace dkgServiceNode.Data
{
    public class RoundContext : DbContext
    {
        private DbSet<Round> Rounds { get; set; }

        private readonly RoundsCache roundsCache;

        private readonly ILogger logger;
        public RoundContext(
            DbContextOptions<RoundContext> options,
            RoundsCache rc,  
            ILogger<RoundContext> lggr) : base(options)
        {
            roundsCache = rc;
            logger = lggr;
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

    }
}
