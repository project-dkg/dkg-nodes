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

namespace dkgServiceNode.Services.Cache
{
    public class RoundsCache
    {
        private  readonly Dictionary<int, Round> _cacheRounds = new();
        private  readonly object _cacheRoundsLock = new();
        public void SaveRoundToCache(Round round)
        {
            _cacheRounds[round.Id] = new Round(round);
        }

        public  Round? GetRoundById(int id)
        {
            _cacheRounds.TryGetValue(id, out var round);
            return round;
        }

        public  List<Round> GetAllRounds()
        {
            return new List<Round>(_cacheRounds.Values);
        }

        public  List<Round> GetAllRoundsSortedByIdDescending()
        {
            return new List<Round>(_cacheRounds.Values.OrderByDescending(r => r.Id));
        }

        public  void AddRoundToCache(Round round)
        {
            lock (_cacheRoundsLock)
            {
                SaveRoundToCache(round);
            }
        }

        public  void UpdateRoundInCache(Round round)
        {
            lock (_cacheRoundsLock)
            {
                _cacheRounds[round.Id] = round;
            }
        }

        public  void DeleteRoundFromCache(int id)
        {
            lock (_cacheRoundsLock)
            {
                _cacheRounds.Remove(id);
            }
        }

        public  bool RoundExists(int id)
        {
            return _cacheRounds.ContainsKey(id);
        }

        public  int? LastRoundResult()
        {
            return _cacheRounds.Values.OrderByDescending(r => r.Id).FirstOrDefault()?.Result;
        }
    }
}
