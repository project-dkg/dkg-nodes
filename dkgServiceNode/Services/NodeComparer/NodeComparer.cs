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

namespace dkgServiceNode.Services.NodeComparer
{
    public class NodeComparer : IComparer<Node>
    {
        private readonly int ZPoint;
        private readonly int RoundId;
        private readonly NodesRoundHistoryCache nodesRoundHistoryCache;
        public NodeComparer(int zPoint, int roundId, NodesRoundHistoryCache nrhc)
        {
            ZPoint = zPoint;
            RoundId = roundId;
            nodesRoundHistoryCache = nrhc;
        }

        private int? GetInt(Node? x)
        {
            return x != null ? nodesRoundHistoryCache.GetNodeRandomForRound(x.Id, RoundId) : null;
        }
        public int Compare(Node? x, Node? y)
        {
            int? a = GetInt(x);
            int? b = GetInt(y);

            if (a == null)
            {
                return b == null ? 0 : 1; // If a is null and b is null, they're equal. If a is null and b is not null, b is smaller. 
            }
            else
            {
                // a is not null...
                if (b == null)
                {
                    // ...and b is null, a is smaller.
                    return -1;
                }
                else
                {
                    int AValue = Math.Abs((int)a - ZPoint);
                    int BValue = Math.Abs((int)b - ZPoint);
                    return AValue.CompareTo(BValue);
                }
            }
        }
    }
}
