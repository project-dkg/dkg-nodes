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

using dkg.group;
using dkg.poly;
using dkgServiceNode.Models;

namespace dkgServiceNode.Services.RoundRunner
{
    public class ActiveNode
    {
        private Node Node { get; set; }
        public string Key => Node.PublicKey;
        public string[]? Deals { get; set; } = null;
        public string[]? Responses { get; set; } = null;
        public IPoint? DistributedPublicKey { get; set; } = null;
        public PriShare? SecretShare { get; set; } = null;
        public bool Failed { get; set; } = false;
        public bool Finished { get; set; } = false;

        private readonly ILogger Logger;
        private readonly int RoundId;

        public ActiveNode(int roundId, Node node, ILogger logger)
        {
            Logger = logger;
            RoundId = roundId;
            Node = node;
        }
        static public string GetKey(Node node)
        {
            return node.PublicKey;
        }
        public void SetResult(string[] data)
        {
            Finished = true;
            Failed = false;
            try
            {
                byte[] pkd = Convert.FromBase64String(data[0]);
                DistributedPublicKey = new Secp256k1Point();
                DistributedPublicKey.SetBytes(pkd);

                pkd = Convert.FromBase64String(data[1]);
                SecretShare = new PriShare();
                SecretShare.SetBytes(pkd);
            }
            catch (Exception ex)
            {
                Logger.LogError("ActiveRound [{Id}]: SetResult exception for node [{node}]\n{message}", RoundId, Key, ex.Message);
            }

        }
        public void SetNoResult()
        {
            Finished = false;
            Failed = true;
        }
        public static bool operator ==(ActiveNode a, Node b) => a.Key == GetKey(b);
        public static bool operator !=(ActiveNode a, Node b) => a.Key != GetKey(b);

        public bool Equals(ActiveNode? other)
        {
            if (other is null) return false;
            return Key.Equals(other.Key);
        }
        public override bool Equals(object? obj)
        {
            return Equals(obj as ActiveNode);
        }
        public override int GetHashCode()
        {
            return Key.GetHashCode();
        }
        public override string ToString()
        {
            return Node.Gd.ToString();
        }
    }
}
