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

namespace dkgServiceNode.Constants
{
    public enum RStatus
    {
        NotStarted = 0,
        Registration = 10,
        CreatingDeals = 20,
        ProcessingDeals = 21,
        ProcessingResponses = 22,
        Finished = 30,
        Cancelled = 40,
        Failed = 41,
        Unknown = 255
    }
    public sealed class RoundStatus
    {
        private readonly Dictionary<RStatus, RStatus> roundStatusRoute = new()
        {
            { RStatus.NotStarted, RStatus.Registration },
            { RStatus.Registration, RStatus.CreatingDeals },
            { RStatus.CreatingDeals, RStatus.ProcessingDeals },
            { RStatus.ProcessingDeals, RStatus.ProcessingResponses },
            { RStatus.ProcessingResponses, RStatus.Finished }
        };
        public RStatus RoundStatusId { get; set; } = RStatus.Unknown;
        public string Name { get; set; } = "Unknown";
        public string ActionName { get; set; } = "--";
        public string ActionIcon { get; set; } = "fa-question";
        public bool IsVersatile()
        {
            return RoundStatusId < RStatus.Finished;
        }

        public RStatus NextStatusId()
        {
            return roundStatusRoute.TryGetValue(RoundStatusId, out RStatus value) ? value : RStatus.Unknown;
        }
        public RoundStatus NextStatus()
        {
            return RoundStatusConstants.GetRoundStatusById((short)NextStatusId());
        }
        public RoundStatus CancelStatus()
        {
            return RoundStatusConstants.GetRoundStatusById((short)RStatus.Cancelled);
        }

        public static implicit operator RStatus(RoundStatus st) => st.RoundStatusId;
        public static implicit operator RoundStatus(RStatus st) => RoundStatusConstants.GetRoundStatusById(st);
        public static implicit operator short(RoundStatus st) => (short)st.RoundStatusId;
        public static implicit operator RoundStatus(short st) => RoundStatusConstants.GetRoundStatusById(st);
        public override string ToString() => Name;

        public static bool operator ==(RoundStatus a, RoundStatus b) => a.RoundStatusId == b.RoundStatusId;
        public static bool operator !=(RoundStatus a, RoundStatus b) => a.RoundStatusId != b.RoundStatusId;
        public override bool Equals(object? obj) => obj is RoundStatus st && st.RoundStatusId == RoundStatusId;
        public override int GetHashCode() => RoundStatusId.GetHashCode();
        public bool IsRunning()
        {
            return RoundStatusId == RStatus.CreatingDeals ||
                   RoundStatusId == RStatus.ProcessingDeals ||
                   RoundStatusId == RStatus.ProcessingResponses;
        }
        public static bool IsRunning(RStatus st) => ((RoundStatus)st).IsRunning();
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
            RoundStatusId = RStatus.Registration,
            Name = "Collecting applications",
            ActionName = "Open Registration",
            ActionIcon = "fa-play"
        };

        public static readonly RoundStatus CreatingDeals = new()
        {
            RoundStatusId = RStatus.CreatingDeals,
            Name = "Creating dkg deals",
            ActionName = "Run dkg algorithm",
            ActionIcon = "fa-forward"
        };

        public static readonly RoundStatus ProcessingDeals = new()
        {
            RoundStatusId = RStatus.ProcessingDeals,
            Name = "Processing dkg deals",
            ActionName = "Process dkg deals",
            ActionIcon = "fa-forward"
        };

        public static readonly RoundStatus ProcessingResponses = new()
        {
            RoundStatusId = RStatus.ProcessingResponses,
            Name = "Processing dkg responses",
            ActionName = "Process dkg responses",
            ActionIcon = "fa-forward"
        };

        public static readonly RoundStatus Finished = new()
        {
            RoundStatusId = RStatus.Finished,
            Name = "Finished [got round result]",
            ActionName = "Finish round",
            ActionIcon = "fa-forward-fast"
        };

        public static readonly RoundStatus Cancelled = new()
        {
            RoundStatusId = RStatus.Cancelled,
            Name = "Cancelled [no round result]",
            ActionName = "Cancel round",
            ActionIcon = "fa-xmark"
        };

        public static readonly RoundStatus Failed = new()
        {
            RoundStatusId = RStatus.Failed,
            Name = "Failed [no round result]"
        };

        public static readonly RoundStatus[] RoundStatusArray = [
            NotStarted,
            Started,
            CreatingDeals,
            ProcessingDeals,
            ProcessingResponses,
            Finished,
            Cancelled,
            Failed
        ];
        public static RoundStatus GetRoundStatusById(short id)
        {
            RoundStatus ret = RoundStatusArray.FirstOrDefault(x => (short)x.RoundStatusId == id) ?? Unknown;
            return ret;
        }
        public static RoundStatus GetRoundStatusById(RStatus st)
        {
            RoundStatus ret = RoundStatusArray.FirstOrDefault(x => x.RoundStatusId == st) ?? Unknown;
            return ret;
        }
    }

}
