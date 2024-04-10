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

namespace dkgServiceNode.Constants
{
    public enum RStatus
    {
        NotStarted = 0,
        Started = 10,
        Processing = 20,
        Finished = 30,
        Unknown = 255
    }
    public static class RoundStatusConstants
    {
        public static readonly RoundStatus Unknown = new()
        {
            RoundStatusId = RStatus.Unknown,
            Name = "Unknown"
        };

        public static readonly RoundStatus NotStarted = new()
        {
            RoundStatusId = RStatus.NotStarted,
            Name = "Not started"
        };

        public static readonly RoundStatus Started = new()
        {
            RoundStatusId = RStatus.Started,
            Name = "Started (collecting nodes)"
        };

        public static readonly RoundStatus Processing = new()
        {
            RoundStatusId = RStatus.Processing,
            Name = "Processing (running dkg algorithm)"
        };

        public static readonly RoundStatus Finished = new()
        {
            RoundStatusId = RStatus.Finished,
            Name = "Finished (random number collected)"
        };

        public static readonly RoundStatus[] RoundStatusArray = [
            NotStarted,
            Started,
            Processing,
            Finished
        ];
        public static RoundStatus GetRoundStatusById(short id)
        {
            RoundStatus? ret = RoundStatusArray.FirstOrDefault(x => (short)x.RoundStatusId == id);
            if (ret == null) ret = Unknown;
            return ret;
        }
    }
}
