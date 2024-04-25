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
using dkgServiceNode.Models;

namespace dkgNodesTests
{

    [TestFixture]
    public class NodeCountResultTests
    {
        [Test]
        public void TestGetCountReturnsCorrectCount()
        {
            var counts = new List<NodeCountResult>
            {
                new NodeCountResult { RoundId = 1, Status = NStatus.RunningStepOne, Count = 5 },
                new NodeCountResult { RoundId = 2, Status = NStatus.RunningStepOne, Count = 3 },
                new NodeCountResult { RoundId = 2, Status = NStatus.WaitingStepTwo, Count = 4 }
            };

            int count = NodeCountResult.GetCount(counts, 1, NStatus.RunningStepOne);
            Assert.That(count, Is.EqualTo(5));
        }

        [Test]
        public void TestGetCountReturnsZeroForNonexistentRound()
        {
            var counts = new List<NodeCountResult>
            { 
                new NodeCountResult { RoundId = 1, Status = NStatus.RunningStepOne, Count = 5 },
                new NodeCountResult { RoundId = 1, Status = NStatus.RunningStepTwo, Count = 3 },
                new NodeCountResult { RoundId = 2, Status = NStatus.RunningStepOne, Count = 4 }
            };

            int count = NodeCountResult.GetCount(counts, 3, NStatus.RunningStepOne);
            Assert.That(count, Is.EqualTo(0));
        }

        [Test]
        public void TestGetCountReturnsZeroForNonexistentStatus()
        {
            var counts = new List<NodeCountResult>
            {
                new NodeCountResult { RoundId = 1, Status = NStatus.RunningStepOne, Count = 5 },
                new NodeCountResult { RoundId = 1, Status = NStatus.RunningStepTwo, Count = 3 },
                new NodeCountResult { RoundId = 2, Status = NStatus.RunningStepOne, Count = 4 }
            };

            int count = NodeCountResult.GetCount(counts, 1, NStatus.Finished);
            Assert.That(count, Is.EqualTo(0));
        }

        [Test]
        public void TestGetCountReturnsTotalCountForNullStatus()
        {
            var counts = new List<NodeCountResult>
            {
                new NodeCountResult { RoundId = 1, Status = NStatus.RunningStepOne, Count = 5 },
                new NodeCountResult { RoundId = 1, Status = NStatus.RunningStepTwo, Count = 3 },
                new NodeCountResult { RoundId = 2, Status = NStatus.RunningStepOne, Count = 4 }
            };

            int count = NodeCountResult.GetCount(counts, 1, null);
            Assert.That(count, Is.EqualTo(8));
        }
    }
}