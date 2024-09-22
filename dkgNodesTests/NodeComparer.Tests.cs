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

using dkgServiceNode.Models;
using dkgServiceNode.Services.Cache;
using dkgServiceNode.Services.NodeComparer;

namespace dkgNodesTests
{
    [TestFixture]
    public class NodeComparerTests
    {
        private readonly NodesRoundHistoryCache nodesRoundHistoryCache = new();

        [Test]
        public void Compare_BothNodesNull_ReturnsZero()
        {
            var comparer = new NodeComparer(0, 1, nodesRoundHistoryCache);
            var result = comparer.Compare(null, null);
            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public void Compare_FirstNodeNull_ReturnsOne()
        {

            var comparer = new NodeComparer(0, 1, nodesRoundHistoryCache); 
            var node = new Node() { Address = "100181" };

            var history1 = new NodesRoundHistory { NodeAddress = "100181", RoundId = 1, NodeRandom = 5 };
            var history2 = new NodesRoundHistory { NodeAddress = "100182", RoundId = 1, NodeRandom = 3 };
            nodesRoundHistoryCache.SaveNodesRoundHistoryToCache(history1);
            nodesRoundHistoryCache.SaveNodesRoundHistoryToCache(history2);

            var result = comparer.Compare(null, node);
            Assert.That(result, Is.EqualTo(1));
        }
        [Test]
        public void Compare_SecondNodeNull_ReturnsMinusOne()
        {
            var comparer = new NodeComparer(0, 1, nodesRoundHistoryCache);
            var node = new Node { Address = "1001810" };
            var result = comparer.Compare(node, null);
            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public void Compare_BothNodesHaveSameNodeRandom_ReturnsZero()
        {
            var comparer = new NodeComparer(0, 1,nodesRoundHistoryCache);
            var node1 = new Node { Address = "1001811" };
            var node2 = new Node { Address = "1001812" };

            var history1 = new NodesRoundHistory { NodeAddress = "1001811", RoundId = 1, NodeRandom = 5 };
            var history2 = new NodesRoundHistory { NodeAddress = "1001812", RoundId = 1, NodeRandom = 5 };

            nodesRoundHistoryCache.SaveNodesRoundHistoryToCache(history1);
            nodesRoundHistoryCache.SaveNodesRoundHistoryToCache(history2);

            var result = comparer.Compare(node1, node2);
            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public void Compare_FirstNodeHasSmallerNodeRandom_ReturnsMinusOne()
        {
            var comparer = new NodeComparer(0, 1, nodesRoundHistoryCache);
            var node1 = new Node { Address = "1001821" };
            var node2 = new Node { Address = "1001822" };

            var history1 = new NodesRoundHistory { NodeAddress = "1001821", RoundId = 1, NodeRandom = 3 };
            var history2 = new NodesRoundHistory { NodeAddress = "1001822", RoundId = 1, NodeRandom = 5 };

            nodesRoundHistoryCache.SaveNodesRoundHistoryToCache(history1);
            nodesRoundHistoryCache.SaveNodesRoundHistoryToCache(history2);

            var result = comparer.Compare(node1, node2);
            Assert.That(result, Is.EqualTo(-1));
        }

        [Test]
        public void Compare_SecondNodeHasSmallerNodeRandom_ReturnsOne()
        {
            var comparer = new NodeComparer(0, 1, nodesRoundHistoryCache);
            var node1 = new Node { Address = "1001831" };
            var node2 = new Node { Address = "1001832" };

            var history1 = new NodesRoundHistory { NodeAddress = "1001831", RoundId = 1, NodeRandom = 5 };
            var history2 = new NodesRoundHistory { NodeAddress = "1001832", RoundId = 1, NodeRandom = 3 };

            nodesRoundHistoryCache.SaveNodesRoundHistoryToCache(history1);
            nodesRoundHistoryCache.SaveNodesRoundHistoryToCache(history2);

            var result = comparer.Compare(node1, node2);
            Assert.That(result, Is.EqualTo(1));
        }
    }

}