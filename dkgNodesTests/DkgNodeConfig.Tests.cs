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

using dkgNode.Models;
using System.Text;

namespace dkgNodesTests
{
    [TestFixture]
    public class DkgNodeConfigTests
    {
        [Test]
        public void TestNameReturnsNiceNameIfSet()
        {
            DkgNodeConfig config = new() { NiceName = "Test Node" };
            Assert.That(config.Name, Is.EqualTo("Test Node"));
        }
        [Test]
        public void TestDefaultConstructorSetsDefaultValues()
        {
            DkgNodeConfig config = new();
            Assert.Multiple(() =>
            {
                Assert.That(config.NiceName, Is.Null);
                Assert.That(config.PublicKey, Is.Null);
                Assert.That(config.ServiceNodeUrl, Is.EqualTo("https://localhost:8081"));
            });
        }

        [Test]
        public void TestNameReturnsAddressIfNiceNameNotSet()
        {
            DkgNodeConfig config = new();
            Assert.That(config.Name, Is.EqualTo(config.Address));
        }

        [Test]
        public void TestGetPublicKeyReturnsPublicKey()
        {
            DkgNodeConfig config = new();
            byte[] publicKey = Encoding.ASCII.GetBytes("1234567890123456"); // 16 bytes
            config.EncodePublicKey(publicKey);
            Assert.That(config.PublicKey, Is.EqualTo(Convert.ToBase64String(publicKey)));
        }

        [Test]
        public void TestCopyConstructorCopiesValues()
        {
            DkgNodeConfig original = new()
            {
                NiceName = "Test Node",
                PublicKey = "publicKey",
                ServiceNodeUrl = "https://example.com",
                PollingInterval = 5000
            };

            DkgNodeConfig copy = new(original);

            Assert.Multiple(() =>
            {
                Assert.That(copy.NiceName, Is.EqualTo(original.NiceName));
                Assert.That(copy.PublicKey, Is.EqualTo(original.PublicKey));
                Assert.That(copy.ServiceNodeUrl, Is.EqualTo(original.ServiceNodeUrl));
                Assert.That(copy.PollingInterval, Is.EqualTo(original.PollingInterval));
                Assert.That(copy.Address, Is.EqualTo(original.Address));
            });
        }

        [Test]
        public void TestDefaultValues()
        {
            DkgNodeConfig config = new();
            Assert.Multiple(() =>
            {
                Assert.That(config.PollingInterval, Is.EqualTo(3000));
                Assert.That(config.ServiceNodeUrl, Is.EqualTo("https://localhost:8081"));
            });
        }
    }
}