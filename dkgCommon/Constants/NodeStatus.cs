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


namespace dkgCommon.Constants
{
    public enum NStatus
    {
        NotRegistered = 0,
        WaitingRoundStart = 10,
        RunningStepOne = 21,
        WaitingStepTwo = 22,
        RunningStepTwo = 23,
        WaitingStepThree = 24,
        RunningStepThree = 25,
        RunningStepFour = 27,
        Finished = 30,
        Failed = 40,
        Unknown = 255
    }
    public sealed class NodeStatus
    {
        public NStatus NodeStatusId { get; set; } = NStatus.Unknown;
        public string Name { get; set; } = "Unknown";

        public static implicit operator NStatus(NodeStatus st) => st.NodeStatusId;
        public static implicit operator NodeStatus(NStatus st) => NodeStatusConstants.GetNodeStatusById(st);
        public static implicit operator short(NodeStatus st) => (short)st.NodeStatusId;
        public static implicit operator NodeStatus(short st) => NodeStatusConstants.GetNodeStatusById(st);
        public override string ToString() => Name;

        public static bool operator ==(NodeStatus a, NodeStatus b) => a.NodeStatusId == b.NodeStatusId;
        public static bool operator !=(NodeStatus a, NodeStatus b) => a.NodeStatusId != b.NodeStatusId;
        public override bool Equals(object? obj) => obj is NodeStatus st && st.NodeStatusId == NodeStatusId;
        public override int GetHashCode() => NodeStatusId.GetHashCode();
        public bool IsRunning() => NodeStatusId >= NStatus.RunningStepOne && NodeStatusId < NStatus.Finished;

        public static bool IsRunning(NStatus st) => st >= NStatus.RunningStepOne && st < NStatus.Finished;
    }
    public static class NodeStatusConstants
    {
        public static readonly NodeStatus Unknown = new()
        {
            NodeStatusId = NStatus.Unknown,
            Name = "Unknown"
        };

        public static readonly NodeStatus NotRegistered = new()
        {
            NodeStatusId = NStatus.NotRegistered,
            Name = "Not registered"
        };

        public static readonly NodeStatus WaitingRoundStart = new()
        {
            NodeStatusId = NStatus.WaitingRoundStart,
            Name = "Waiting for round start"
        };

        public static readonly NodeStatus RunningStepOne = new()
        {
            NodeStatusId = NStatus.RunningStepOne,
            Name = "Running step one of dkg algorithm"
        };

        public static readonly NodeStatus WaitingStepTwo = new()
        {
            NodeStatusId = NStatus.WaitingStepTwo,
            Name = "Waiting for step two of dkg algorithm"
        };

        public static readonly NodeStatus RunningStepTwo = new()
        {
            NodeStatusId = NStatus.RunningStepTwo,
            Name = "Running step two of dkg algorithm"
        };

        public static readonly NodeStatus WaitingStepThree = new()
        {
            NodeStatusId = NStatus.WaitingStepThree,
            Name = "Waiting for step three of dkg algorithm"
        };

        public static readonly NodeStatus RunningStepThree = new()
        {
            NodeStatusId = NStatus.RunningStepThree,
            Name = "Running step three of dkg algorithm"
        };

        public static readonly NodeStatus Finished = new()
        {
            NodeStatusId = NStatus.Finished,
            Name = "Finished [got round result]"
        };

        public static readonly NodeStatus Failed = new()
        {
            NodeStatusId = NStatus.Failed,
            Name = "Failed [no round result]",
        };

        public static readonly NodeStatus[] NodeStatusArray = [
            NotRegistered,
            WaitingRoundStart,
            RunningStepOne,
            WaitingStepTwo,
            RunningStepTwo,
            WaitingStepThree,
            RunningStepThree,
            Finished,
            Failed
        ];
        public static NodeStatus GetNodeStatusById(short id)
        {
            NodeStatus ret = NodeStatusArray.FirstOrDefault(x => (short)x.NodeStatusId == id) ?? Unknown;
            return ret;
        }

        public static NodeStatus GetNodeStatusById(NStatus st)
        {
            NodeStatus ret = NodeStatusArray.FirstOrDefault(x => x.NodeStatusId == st) ?? Unknown;
            return ret;
        }
    }
}
