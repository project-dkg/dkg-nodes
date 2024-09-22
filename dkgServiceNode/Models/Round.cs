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
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace dkgServiceNode.Models
{
    [Table("rounds")]
    public class Round
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public int Id { get; set; }

        [Column("status")]
        public short StatusValue { get; set; } = 0;

        [Column("max_nodes")]
        public int MaxNodes { get; set; } = 256;

        [Column("timeout2")]
        public int Timeout2 { get; set; } = 30;

        [Column("timeout3")]
        public int Timeout3 { get; set; } = 30;

        [Column("timeoutr")]
        public int TimeoutR { get; set; } = 120;

        [NotMapped]
        public int NodeCount { get; set; } = 0;

        [NotMapped]
        public int NodeCountWRegistration { get; set; } = 0;

        [NotMapped]
        public int NodeCountWRoundStart { get; set; } = 0;

        [NotMapped]
        public int NodeCountStepOne { get; set; } = 0;

        [NotMapped]
        public int NodeCountWStepTwo { get; set; } = 0;
        [NotMapped]
        public int NodeCountStepTwo { get; set; } = 0;

        [NotMapped]
        public int NodeCountWStepThree { get; set; } = 0;
        [NotMapped]
        public int NodeCountStepThree { get; set; } = 0;
        [NotMapped]
        public int NodeCountStepFour { get; set; } = 0;

        [NotMapped]
        public int NodeCountFinished { get; set; } = 0;

        [NotMapped]
        public int NodeCountFailed { get; set; } = 0;

        [NotMapped]
        public int NodeCountTimedOut { get; set; } = 0;

        [Column("result")]
        public int? Result { get; set; } = null;

        [Column("created")]
        public DateTime CreatedOn { get; set; } = DateTime.Now.ToUniversalTime();

        [Column("modified")]
        public DateTime ModifiedOn { get; set; } = DateTime.Now.ToUniversalTime();

        [NotMapped]
        public RoundStatus Status
        {
            get { return RoundStatusConstants.GetRoundStatusById(StatusValue); }
            set { StatusValue = (short)value.RoundStatusId; }
        }

        [NotMapped]
        public bool IsVersatile
        {
            get { return Status.IsVersatile(); }
        }

        [NotMapped]
        public RoundStatus NextStatus
        {
            get { return Status.NextStatus(); }
        }
        [NotMapped]
        public RoundStatus CancelStatus
        {
            get { return RoundStatus.CancelStatus(); }
        }

        [JsonIgnore]
        public ICollection<Node> Nodes { get; set; } = [];

        public override string ToString() => $"Round {Id}";

        public Round()
        {
        }

        public Round(Round other)
        {
            Id = other.Id;
            StatusValue = other.StatusValue;
            MaxNodes = other.MaxNodes;
            Timeout2 = other.Timeout2;
            Timeout3 = other.Timeout3;
            TimeoutR = other.TimeoutR;
            NodeCount = other.NodeCount;
            NodeCountStepOne = other.NodeCountStepOne;
            NodeCountWStepTwo = other.NodeCountWStepTwo;
            NodeCountStepTwo = other.NodeCountStepTwo;
            NodeCountWStepThree = other.NodeCountWStepThree;
            NodeCountStepThree = other.NodeCountStepThree;
            NodeCountStepFour = other.NodeCountStepFour;
            NodeCountFinished = other.NodeCountFinished;
            NodeCountFailed = other.NodeCountFailed;
            NodeCountTimedOut = other.NodeCountTimedOut;
            Result = other.Result;
            CreatedOn = other.CreatedOn;
            ModifiedOn = other.ModifiedOn;
            Nodes = other.Nodes;
        }
    }
}
