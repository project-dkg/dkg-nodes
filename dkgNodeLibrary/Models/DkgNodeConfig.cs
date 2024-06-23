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

using Microsoft.Extensions.Logging;
using Solnet.Wallet.Bip39;
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

        [JsonPropertyName("PublicKey")]
        public string? SerializedPublicKey
        {
            get { return PublicKey; }
        }
        [JsonIgnore]
        internal string? PublicKey { get; set; }

        public string? GetPublicKey() => PublicKey;
        public void EncodePublicKey(byte[] value)
        {
            PublicKey = Convert.ToBase64String(value);
        }

        [JsonPropertyName("Name")]
        public string Name
        {
            get { return NiceName ?? Address; }
        }
        public DkgNodeConfig()
        {
            NiceName = null;
            PublicKey = null;
            Address = string.Empty;
            PollingInterval = 3000;
            ServiceNodeUrl = "https://localhost:8081";
        }
        public DkgNodeConfig(DkgNodeConfig other)
        {
            NiceName = other.NiceName;
            PublicKey = other.PublicKey;
            Address = other.Address;
            PollingInterval = other.PollingInterval;
            ServiceNodeUrl = other.ServiceNodeUrl;
        }

        public static (string, string) GenerateNewAddress()
        {
            var mnemonic = new Mnemonic(WordList.English, WordCount.Twelve);
            var wallet = new Wallet(mnemonic);
            Account account = wallet.Account;

            // The public key of the account is the Solana address
            string solanaAddress = account.PublicKey.Key;

            return (solanaAddress, mnemonic.ToString());
        }

    }
}

