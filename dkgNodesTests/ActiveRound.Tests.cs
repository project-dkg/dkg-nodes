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

using Moq;
using Microsoft.Extensions.Logging;
using dkgServiceNode.Services.RoundRunner;
using dkgServiceNode.Models;

namespace dkgNodesTests
{

    [TestFixture]
    public class ActiveRoundTests
    {
        private Mock<ILogger<ActiveRound>> _loggerMock;
        private string _publicKey = "_public_key_";
        private string _name = "name";

        [SetUp]
        public void SetUp()
        {
            _loggerMock = new Mock<ILogger<ActiveRound>>();
            
        }

        [Test]
        public void Constructor_Sets_Id()
        {
            var round = new Round { Id = 5 };
            var activeRound = new ActiveRound(round, _loggerMock.Object);
            Assert.That(activeRound.Id, Is.EqualTo(round.Id));
        }

        [Test]
        public void Run_WithEmptyNodes_DoesNotThrow()
        {
            var round = new Round { Id = 5 };
            var activeRound = new ActiveRound(round, _loggerMock.Object);
            var nodes = new List<Node>();
            Assert.DoesNotThrow(() => activeRound.Run(nodes));
        }

        [Test]
        public void Run_WithNodes_CallsRunRound()
        {
            var round = new Round { Id = 5 };
            var activeRound = new ActiveRound(round, _loggerMock.Object);
            var nodes = new List<Node> { new() { PublicKey = _publicKey, Name = _name } };

            activeRound.Run(nodes);

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Run")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            Assert.That(activeRound.GetStepOneData, Is.Not.Null);

        }

        [Test]
        public void GetResult_WithNodes_ReturnsResult()
        {
            var round = new Round { Id = 5 };
            var activeRound = new ActiveRound(round, _loggerMock.Object);
            var nodes = new List<Node> { new() { PublicKey = _publicKey, Name = _name } };

            activeRound.Run(nodes);
            var result = activeRound.GetResult();

            Assert.That(result, Is.Null);
        }
        [Test]
        public void TestClearSetsNodesToNull()
        {
            var round = new Round { Id = 5 };
            var activeRound = new ActiveRound(round, _loggerMock.Object);
            var nodes = new List<Node> { new() { PublicKey = _publicKey, Name = _name } };

            activeRound.Run(nodes);
            activeRound.Clear();

            Assert.That(activeRound.GetStepOneData().Length, Is.EqualTo(0));
        }

        [Test]
        public void TestIsResultReadyReturnsFalseWhenNotAllNodesAreFinalized()
        {
            var round = new Round { Id = 5 };
            var activeRound = new ActiveRound(round, _loggerMock.Object);
            var nodes = new List<Node> { new() { PublicKey = _publicKey, Name = _name } };

            activeRound.Run(nodes);

            Assert.That(activeRound.IsResultReady(), Is.False);
        }

        [Test]
        public void TestIsResultReadyReturnsTrueWhenAllNodesAreFinalized()
        {
            var round = new Round { Id = 5 };
            var activeRound = new ActiveRound(round, _loggerMock.Object);
            var nodes = new List<Node> { new() { PublicKey = _publicKey, Name = _name } };

            activeRound.Run(nodes);
            activeRound.SetNoResult(nodes[0]);

            Assert.That(activeRound.IsResultReady(), Is.True);
        }

        [Test]
        public void TestIsStepTwoDataReadyReturnsFalseWhenNotAllNodesHaveDeals()
        {
            var round = new Round { Id = 5 };
            var activeRound = new ActiveRound(round, _loggerMock.Object);
            var nodes = new List<Node> { new() { PublicKey = _publicKey, Name = _name } };

            activeRound.Run(nodes);

            Assert.That(activeRound.IsStepTwoDataReady(), Is.False);
        }

        [Test]
        public void TestIsStepThreeDataReadyReturnsFalseWhenNotAllNodesHaveResponses()
        {
            var round = new Round { Id = 5 };
            var activeRound = new ActiveRound(round, _loggerMock.Object);
            var nodes = new List<Node> { new() { PublicKey = _publicKey, Name = _name } };

            activeRound.Run(nodes);

            Assert.That(activeRound.IsStepThreeDataReady(), Is.False);
        }

        [Test]
        public void TestSetStepTwoDataSetsDealsForNode()
        {
            var round = new Round { Id = 5 };
            var activeRound = new ActiveRound(round, _loggerMock.Object);
            var nodes = new List<Node> { new() { PublicKey = _publicKey, Name = _name } };

            activeRound.Run(nodes);
            activeRound.SetStepTwoData(nodes[0], ["deal1", "deal2"]);

            Assert.That(activeRound.IsStepTwoDataReady(), Is.True);
        }

        [Test]
        public void TestSetStepThreeDataSetsResponsesForNode()
        {
            var round = new Round { Id = 5 };
            var activeRound = new ActiveRound(round, _loggerMock.Object);
            var nodes = new List<Node> { new() { PublicKey = _publicKey, Name = _name } };

            activeRound.Run(nodes);
            activeRound.SetStepThreeData(nodes[0], ["response1", "response2"]);

            Assert.That(activeRound.IsStepThreeDataReady(), Is.True);
        }

    }
}