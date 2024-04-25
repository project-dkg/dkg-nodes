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
using dkg.group;
using dkg.poly;

namespace dkgNodesTests
{

    [TestFixture]
    public class RunnerTests
    {
        private Mock<ILogger<ActiveRound>> _mockLogger;
        private Runner _runner;

        [SetUp]
        public void SetUp()
        {
            _mockLogger = new Mock<ILogger<ActiveRound>>();
            _runner = new Runner(_mockLogger.Object);
        }

        [Test]
        public void TestStartRoundAddsRoundToActiveRounds()
        {
            var round = new Round { Id = 1 };
            _runner.StartRound(round);
            Assert.That(_runner.ActiveRounds.Count, Is.EqualTo(1));
            Assert.That(_runner.ActiveRounds.First().Id, Is.EqualTo(round.Id));
        }

        [Test]
        public void TestRunRoundRunsRound()
        {
            var round = new Round { Id = 1 };
            var nodes = new List<Node> { new Node { PublicKey = "publicKey" } };
            _runner.StartRound(round);
            _runner.RunRound(round, nodes);
            var activeRound = _runner.ActiveRounds.First();
            Assert.That(activeRound.Nodes, Has.Length.EqualTo(nodes.Count));
        }

        [Test]
        public void TestGetRoundResultReturnsNullWhenRoundNotFinished()
        {
            var round = new Round { Id = 1 };
            _runner.StartRound(round);
            Assert.That(_runner.GetRoundResult(round), Is.Null);
        }

        [Test]
        public void TestFinishRoundRemovesRoundFromActiveRounds()
        {
            var round = new Round { Id = 1 };
            _runner.StartRound(round);
            _runner.FinishRound(round);
            Assert.That(_runner.ActiveRounds.Count, Is.EqualTo(0));
        }

        [Test]
        public void TestCancelRoundRemovesRoundFromActiveRounds()
        {
            var round = new Round { Id = 1 };
            _runner.StartRound(round);
            _runner.CancelRound(round);
            Assert.That(_runner.ActiveRounds.Count, Is.EqualTo(0));
        }
        [Test]
        public void TestProcessDealsCallsProcessDealsOnActiveRound()
        {
            var round = new Round { Id = 1 };
            _runner.StartRound(round);
            _runner.ProcessDeals(round);
            var activeRound = _runner.ActiveRounds.First();
            Assert.That(activeRound.IsStepTwoDataReady(), Is.True);
        }

        [Test]
        public void TestProcessResponsesCallsProcessResponsesOnActiveRound()
        {
            var round = new Round { Id = 1 };
            _runner.StartRound(round);
            _runner.ProcessResponses(round);
            var activeRound = _runner.ActiveRounds.First();
            Assert.IsTrue(activeRound.IsStepThreeDataReady());
        }

        [Test]
        public void TestSetNoResultSetsNoResultOnActiveRound()
        {
            var round = new Round { Id = 1 };
            var node = new Node { PublicKey = "publicKey" };
            var nodes = new List<Node> { node };
            _runner.StartRound(round);
            _runner.RunRound(round, nodes);

            _runner.SetNoResult(round, node);
            var activeRound = _runner.ActiveRounds.First();
            Assert.That(activeRound.Nodes?.First().Finalized, Is.True);
        }

        [Test]
        public void TestSetResultSetsResultOnActiveRound()
        {
            var round = new Round { Id = 1 };
            var node = new Node { PublicKey = "publicKey" };
            string[] data = { "AtDGHAvdzEBXkF9nrlWVyupD6AeTF2zHc+5EGExa13TB", "AQAAAGMygfx9vJSf4XEPUYIByz8rRU7cehXHxylasMN/1486" };
            _runner.StartRound(round);

            var nodes = new List<Node> { node };
            _runner.StartRound(round);
            _runner.RunRound(round, nodes);
            _runner.SetResult(round, node, data);
            var activeRound = _runner.ActiveRounds.First();
            string?[] r =
            [
                Convert.ToBase64String(activeRound.Nodes?.First().DistributedPublicKey?.GetBytes() ?? []),
                Convert.ToBase64String(activeRound.Nodes?.First().SecretShare?.GetBytes() ?? []),
            ];
            Assert.That(r, Is.EqualTo(data));
        }

        [Test]
        public void TestSetStepTwoDataSetsStepTwoDataOnActiveRound()
        {
            var round = new Round { Id = 1 };
            var node = new Node { PublicKey = "publicKey" };
            var data = new string[] { "data1", "data2" };
            var nodes = new List<Node> { node };
            _runner.StartRound(round);
            _runner.RunRound(round, nodes);
            _runner.SetStepTwoData(round, node, data);
            var activeRound = _runner.ActiveRounds.First();
            Assert.That(activeRound.Nodes?.First().Deals, Is.EqualTo(data));
        }

        [Test]
        public void TestSetStepThreeDataSetsStepThreeDataOnActiveRound()
        {
            var round = new Round { Id = 1 };
            var node = new Node { PublicKey = "publicKey" };
            var data = new string[] { "data1", "data2" };
            var nodes = new List<Node> { node };
            _runner.StartRound(round);
            _runner.RunRound(round, nodes);
            _runner.SetStepThreeData(round, node, data);
            var activeRound = _runner.ActiveRounds.First();
            Assert.That(activeRound.Nodes?.First().Responses, Is.EqualTo(data));
        }

        [Test]
        public void TestIsResultReadyReturnsCorrectValue()
        {
            var round = new Round { Id = 1 };
            var node = new Node { PublicKey = "publicKey" };
            var nodes = new List<Node> { node };
            _runner.StartRound(round);
            _runner.RunRound(round, nodes);
            Assert.That(_runner.IsResultReady(round), Is.False);
            _runner.FinishRound(round);
            Assert.That(_runner.IsResultReady(round), Is.False);   // The round does not exist already
        }

        [Test]
        public void TestIsStepTwoDataReadyReturnsCorrectValue()
        {
            var round = new Round { Id = 1 };
            var node = new Node { PublicKey = "publicKey" };
            var nodes = new List<Node> { node };
            _runner.StartRound(round);
            _runner.RunRound(round, nodes);
            Assert.That(_runner.IsStepTwoDataReady(round), Is.False);
            _runner.SetStepTwoData(round, new Node { PublicKey = "publicKey" }, ["data1", "data2"]);
            Assert.That(_runner.IsStepTwoDataReady(round), Is.True);
        }

        [Test]
        public void TestIsStepThreeDataReadyReturnsCorrectValue()
        {
            var round = new Round { Id = 1 };
            var node = new Node { PublicKey = "publicKey" };
            var nodes = new List<Node> { node };
            _runner.StartRound(round);
            _runner.RunRound(round, nodes);
            Assert.That(_runner.IsStepThreeDataReady(round), Is.False);
            _runner.SetStepThreeData(round, new Node { PublicKey = "publicKey" }, ["data1", "data2"]);
            Assert.That(_runner.IsStepThreeDataReady(round), Is.True);
        }

    }
}
