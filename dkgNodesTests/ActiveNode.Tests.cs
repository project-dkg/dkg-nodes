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

using dkgServiceNode.Services.RoundRunner;
using dkgServiceNode.Models;
using Microsoft.Extensions.Logging;
using Moq;


namespace dkgNodesTests
{
    [TestFixture]
    public class ActiveNodeTests
    {
        private Mock<ILogger> _mockLogger;

        [SetUp]
        public void SetUp()
        {
            _mockLogger = new Mock<ILogger>();
        }

        [Test]
        public void TestConstructorSetsKey()
        {
            Node node = new Node { PublicKey = "publicKey" };
            ActiveNode activeNode = new ActiveNode(1, node, _mockLogger.Object);
            Assert.That(activeNode.Key, Is.EqualTo("publicKey"));
        }

        [Test]
        public void TestSetResultSetsDistributedPublicKeyAndSecretShare()
        {
            Node node = new Node { PublicKey = "publicKey" };
            ActiveNode activeNode = new ActiveNode(1, node, _mockLogger.Object);

            string[] data = { "AtDGHAvdzEBXkF9nrlWVyupD6AeTF2zHc+5EGExa13TB", "AQAAAGMygfx9vJSf4XEPUYIByz8rRU7cehXHxylasMN/1486" };
            activeNode.SetResult(data);
            Assert.Multiple(() =>
            {
                Assert.That(activeNode.DistributedPublicKey, Is.Not.Null);
                Assert.That(activeNode.SecretShare, Is.Not.Null);
            });
        }

        [Test]
        public void TestSetNoResultSetsFinalizedToTrue()
        {
            Node node = new Node { PublicKey = "publicKey" };
            ActiveNode activeNode = new ActiveNode(1, node, _mockLogger.Object);
            activeNode.SetNoResult();
            Assert.That(activeNode.Failed, Is.True);
        }

        [Test]
        public void TestEqualsReturnsTrueForSameKey()
        {
            Node node = new Node { PublicKey = "publicKey" };
            ActiveNode activeNode1 = new ActiveNode(1, node, _mockLogger.Object);
            ActiveNode activeNode2 = new ActiveNode(2, node, _mockLogger.Object);
            Assert.That(activeNode1.Equals(activeNode2), Is.True);
        }

        [Test]
        public void TestEqualsReturnsFalseForDifferentKey()
        {
            Node node1 = new Node { PublicKey = "publicKey1" };
            Node node2 = new Node { PublicKey = "publicKey2" };
            ActiveNode activeNode1 = new ActiveNode(1, node1, _mockLogger.Object);
            ActiveNode activeNode2 = new ActiveNode(2, node2, _mockLogger.Object);
            Assert.That(activeNode1.Equals(activeNode2), Is.False);
        }

        [Test]
        public void TestFailedIsFalseByDefault()
        {
            Node node = new Node { PublicKey = "publicKey" };
            ActiveNode activeNode = new ActiveNode(1, node, _mockLogger.Object);
            Assert.That(activeNode.Failed, Is.False);
        }

        [Test]
        public void TestSetNoResultSetsFailedToTrue()
        {
            Node node = new Node { PublicKey = "publicKey" };
            ActiveNode activeNode = new ActiveNode(1, node, _mockLogger.Object);
            activeNode.SetNoResult();
            Assert.That(activeNode.Failed, Is.True);
        }

        [Test]
        public void TestSetResultSetsFailedToFalse()
        {
            Node node = new Node { PublicKey = "publicKey" };
            ActiveNode activeNode = new ActiveNode(1, node, _mockLogger.Object);
            string[] data = { "AtDGHAvdzEBXkF9nrlWVyupD6AeTF2zHc+5EGExa13TB", "AQAAAGMygfx9vJSf4XEPUYIByz8rRU7cehXHxylasMN/1486" };
            activeNode.SetResult(data);
            Assert.That(activeNode.Failed, Is.False);
        }

        [Test]
        public void TestSetTimedOutSetsFailedToFalse()
        {
            Node node = new Node { PublicKey = "publicKey" };
            ActiveNode activeNode = new ActiveNode(1, node, _mockLogger.Object);
            activeNode.SetTimedOut();
            Assert.That(activeNode.Failed, Is.False);
        }

        [Test]
        public void TestTimedOutIsFalseByDefault()
        {
            Node node = new Node { PublicKey = "publicKey" };
            ActiveNode activeNode = new ActiveNode(1, node, _mockLogger.Object);
            Assert.That(activeNode.TimedOut, Is.False);
        }

        [Test]
        public void TestSetTimedOutSetsTimedOutToTrue()
        {
            Node node = new Node { PublicKey = "publicKey" };
            ActiveNode activeNode = new ActiveNode(1, node, _mockLogger.Object);
            activeNode.SetTimedOut();
            Assert.That(activeNode.TimedOut, Is.True);
        }

        [Test]
        public void TestSetResultSetsTimedOutToFalse()
        {
            Node node = new Node { PublicKey = "publicKey" };
            ActiveNode activeNode = new ActiveNode(1, node, _mockLogger.Object);
            string[] data = { "AtDGHAvdzEBXkF9nrlWVyupD6AeTF2zHc+5EGExa13TB", "AQAAAGMygfx9vJSf4XEPUYIByz8rRU7cehXHxylasMN/1486" };
            activeNode.SetResult(data);
            Assert.That(activeNode.TimedOut, Is.False);
        }

        [Test]
        public void TestSetNoResultSetsTimedOutToFalse()
        {
            Node node = new Node { PublicKey = "publicKey" };
            ActiveNode activeNode = new ActiveNode(1, node, _mockLogger.Object);
            activeNode.SetNoResult();
            Assert.That(activeNode.TimedOut, Is.False);
        }

    }

}
