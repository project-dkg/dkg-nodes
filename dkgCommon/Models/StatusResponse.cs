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

namespace dkgCommon.Models
{
    public class StatusResponse
    {
        public int RoundId { get; set; }
        public RStatus RoundStatus { get; set; }
        public int LastRoundId { get; set; }
        public RStatus LastRoundStatus { get; set; }
        public int? LastRoundResult { get; set; } = null;
        public NStatus Status { get; set; }
        public int? NodeRandom { get; set; } = null;
        public NStatus LastStatus { get; set; }
        public int? LastNodeRandom { get; set; } = null;
        public string[] Data { get; set; }
  
        public StatusResponse()
        {
            RoundId = 0;
            LastRoundId = 0;
            Status = NStatus.Unknown;
            LastStatus = NStatus.Unknown;
            RoundStatus = RStatus.Unknown;
            LastRoundStatus = RStatus.Unknown;
            Data = [];
        }
        public StatusResponse(int roundId, NStatus status)
            {
            RoundId = roundId;
            LastRoundId = 0;
            Status = status;
            LastStatus = NStatus.Unknown;
            RoundStatus = RStatus.Unknown;
            LastRoundStatus = RStatus.Unknown;
            Data = [];

        }
        public StatusResponse(int roundId, RStatus roundStatus, 
                              int lastRoundId, RStatus lastRoundStatus, int? lastRoundResult,
                              NStatus status, int? nodeRandom, NStatus lastStatus, int? lastNodeRandom)
        {
            RoundId = roundId;
            RoundStatus = roundStatus;
            LastRoundId = lastRoundId;
            LastRoundStatus = lastRoundStatus;
            LastRoundResult = lastRoundResult;
            Status = status;
            NodeRandom = nodeRandom;
            LastStatus = lastStatus;
            LastNodeRandom = lastNodeRandom;
            Data = [];        
        }
        public StatusResponse(int roundId, RStatus roundStatus,
                              int lastRoundId, RStatus lastRoundStatus, int? lastRoundResult,
                              NStatus status, int? nodeRandom, NStatus lastStatus, int? lastNodeRandom,
                              string[] data)
        {
            RoundId = roundId;
            RoundStatus = roundStatus;
            LastRoundId = lastRoundId;
            LastRoundStatus = lastRoundStatus;
            LastRoundResult = lastRoundResult;
            Status = status;
            NodeRandom = nodeRandom;
            LastStatus = lastStatus;
            LastNodeRandom = lastNodeRandom;
            Data = data;
        }
        public override string ToString()
        {
            return $"StatusResponse [RoundId: {RoundId}, Status: {(NodeStatus)Status}, Data: [{string.Join(", ", Data)}]]";
        }
    }

}
