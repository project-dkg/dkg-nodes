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
using dkgServiceNode.Services.CRandom;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace dkgServiceNode.Models
{
    [Table("nodes")]
    public class Node
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = "--";

        [Column("address")]
        // Solana Wallet Address
        public string Address { get; set; } = string.Empty;

        [NotMapped]
        // Dkg Algorithm Public Key
        public string PublicKey { get; set; } = string.Empty;

        [Column("round_id")]
        // This is required to make Entity Framework happy
        // Otherwise EF creates shadow foreign key and starts to use
        // his placeholder is null in the database and is never changed
        public int? RoundId { get; set; } = null;

        [NotMapped]
        public short StatusValue { get; set; } = 0;

        [NotMapped]
        public int? Random { get; set; } = null;

        [NotMapped]
        public string Signature { get; set; } = string.Empty;

        [NotMapped]
        public NodeStatus Status
        {
            get { return NodeStatusConstants.GetNodeStatusById(StatusValue); }
            set { StatusValue = (short)value.NodeStatusId; }
        }

        public override string ToString() => Name;

        public void CalculateRandom()
        {
            Random = CR.Calculate(PublicKey);
        }

        public Node()
        {
            
        }
        public Node(Node other)
        {
            Id = other.Id;
            Name = other.Name;
            PublicKey = other.PublicKey;
            RoundId = other.RoundId;
            StatusValue = other.StatusValue;
            Address = other.Address;
            Random = other.Random;
            Signature = other.Signature;
            Status = other.Status;
        }
    }
}
