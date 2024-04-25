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
using dkgServiceNode.Services.NodeComparer;

namespace dkgNodesTests
{
    [TestFixture]
    public class NodeComparerTests
    {
        [Test]
        public void TestCompareReturnsZeroForEqualNodes()
        {
            var comparer = new NodeComparer(0);
            var x = new Node { PublicKey = Convert.ToBase64String(BitConverter.GetBytes(1)) };
            var y = new Node { PublicKey = Convert.ToBase64String(BitConverter.GetBytes(1)) };
            Assert.That(comparer.Compare(x, y), Is.EqualTo(0));
        }

        [Test]
        public void TestCompareReturnsNegativeForXLessThanY()
        {
            var comparer = new NodeComparer(0);
            var x = new Node { PublicKey = Convert.ToBase64String(BitConverter.GetBytes(1)) };
            var y = new Node { PublicKey = Convert.ToBase64String(BitConverter.GetBytes(2)) };
            Assert.That(comparer.Compare(x, y), Is.EqualTo(-1));
        }

        [Test]
        public void TestCompareReturnsPositiveForXGreaterThanY()
        {
            var comparer = new NodeComparer(0);
            var x = new Node { PublicKey = Convert.ToBase64String(BitConverter.GetBytes(2)) };
            var y = new Node { PublicKey = Convert.ToBase64String(BitConverter.GetBytes(1)) };
            Assert.That(comparer.Compare(x, y), Is.EqualTo(1));
        }

        [Test]
        public void TestCompareReturnsCorrectValueForDifferentZeroPoints()
        {
            var comparer = new NodeComparer(2);
            var x = new Node { PublicKey = Convert.ToBase64String(BitConverter.GetBytes(3)) };
            var y = new Node { PublicKey = Convert.ToBase64String(BitConverter.GetBytes(1)) };
            Assert.That(comparer.Compare(x, y), Is.EqualTo(0));
        }
    }
}
