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

using System.Text;
using Solnet.Wallet;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
[assembly: InternalsVisibleTo("dkgNodesTests")]

namespace dkgNode.Models
{
    // Конфигурация узла (он же "node", "участник")
    public class DkgNodeConfig
    {
        [JsonIgnore]
        public string? NiceName { get; set; }

        [JsonIgnore]
        public int PollingInterval { get; set; }

        [JsonIgnore]
        public string ServiceNodeUrl { get; set; }

        public string Address { get; set; }
        public string? PublicKey { get; set; }
        public void EncodePublicKey(byte[] value)
        {
            PublicKey = Convert.ToBase64String(value);
        }

        public string? Signature { get; set; }
        public void SelfSign()
        {
            if (SolanaAccount is null)
            {
                throw new Exception("Solana account is not initialized");
            }

            string msg = $"{Address}{PublicKey}{Name}";
            byte[] msgBytes = Encoding.UTF8.GetBytes(msg);
            byte[] SignatureBytes = SolanaAccount.Sign(msgBytes);
            Signature = Convert.ToBase64String(SignatureBytes);
        }

        [JsonPropertyName("Name")]
        public string Name
        {
            get { return NiceName ?? Address; }
        }
        [JsonIgnore]
        public Account SolanaAccount { get; set; }
        public DkgNodeConfig()
        {
            NiceName = null;
            PublicKey = null;
            Address = string.Empty;
            PollingInterval = 3000;
            ServiceNodeUrl = "https://localhost:8081";
            SolanaAccount = new();
        }
        public DkgNodeConfig(DkgNodeConfig other)
        {
            NiceName = other.NiceName;
            PublicKey = other.PublicKey;
            Address = other.Address;
            PollingInterval = other.PollingInterval;
            ServiceNodeUrl = other.ServiceNodeUrl;
            SolanaAccount = other.SolanaAccount;
        }
    }
}

