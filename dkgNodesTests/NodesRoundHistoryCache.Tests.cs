// NodesRoundHistoryCacheTests.cs
using dkgServiceNode.Models;
using dkgServiceNode.Services.Cache;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace dkgNodesTests
{
    [TestFixture]
    public class NodesRoundHistoryCacheTests
    {
        private readonly NodesRoundHistoryCache nodesRoundHistoryCache = new();
        [SetUp]
        public void SetUp()
        {

        }

        [Test]
        public void LoadNodesRoundHistoriesToCache_ShouldLoadHistories()
        {
            // Arrange
            var histories = new List<NodesRoundHistory>
            {
                new NodesRoundHistory { NodeId = 100991, RoundId = 1, NodeRandom = 100 },
                new NodesRoundHistory { NodeId = 100991, RoundId = 2, NodeRandom = 200 },
                new NodesRoundHistory { NodeId = 100992, RoundId = 1, NodeRandom = 300 }
            };

            // Act
            nodesRoundHistoryCache.LoadNodesRoundHistoriesToCache(histories);

            // Assert
            var result = nodesRoundHistoryCache.GetLastNodeRoundHistory(100991, 2);
            Assert.That(result, Is.Null);

            result = nodesRoundHistoryCache.GetLastNodeRoundHistory(100991, 3);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.NodeRandom, Is.EqualTo(200));
        }

        [Test]
        public void LoadNodesRoundHistoryToCache_ShouldAddOrUpdateHistory()
        {
            // Arrange
            var history = new NodesRoundHistory { NodeId = 101991, RoundId = 1, NodeRandom = 100 };

            // Act
            nodesRoundHistoryCache.LoadNodesRoundHistoryToCache(history);

            // Assert
            var result = nodesRoundHistoryCache.GetLastNodeRoundHistory(101991, 2);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.NodeRandom, Is.EqualTo(100));

            // Update the history
            history.NodeRandom = 200;
            nodesRoundHistoryCache.LoadNodesRoundHistoryToCache(history);

            // Assert the update
            result = nodesRoundHistoryCache.GetLastNodeRoundHistory(101991, 2);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.NodeRandom, Is.EqualTo(200));
        }

        [Test]
        public void GetLastNodeRoundHistory_ShouldReturnCorrectHistory()
        {
            // Arrange
            nodesRoundHistoryCache.LoadNodesRoundHistoryToCache(new NodesRoundHistory { NodeId = 101891, RoundId = 1, NodeRandom = 100 });
            nodesRoundHistoryCache.LoadNodesRoundHistoryToCache(new NodesRoundHistory { NodeId = 101891, RoundId = 2, NodeRandom = 200 });

            // Act
            var result = nodesRoundHistoryCache.GetLastNodeRoundHistory(101891, 2);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.NodeRandom, Is.EqualTo(100));
        }

        [Test]
        public void CheckNodeQualification_ShouldReturnTrueIfQualified()
        {
            // Arrange
            nodesRoundHistoryCache.LoadNodesRoundHistoryToCache(new NodesRoundHistory { NodeId = 101791, RoundId = 1, NodeRandom = 100 });

            // Act
            var isQualified = nodesRoundHistoryCache.CheckNodeQualification(101791, 1);

            // Assert
            Assert.That(isQualified, Is.True);
        }

        [Test]
        public void CheckNodeQualification_ShouldReturnFalseIfNotQualified()
        {
            // Act
            var isQualified = nodesRoundHistoryCache.CheckNodeQualification(1, 1);

            // Assert
            Assert.That(isQualified, Is.False);
        }

        [Test]
        public void GetNodeRandomForRound_ShouldReturnCorrectRandom()
        {
            // Arrange
            nodesRoundHistoryCache.LoadNodesRoundHistoryToCache(new NodesRoundHistory { NodeId = 101691, RoundId = 1, NodeRandom = 100 });

            // Act
            var random = nodesRoundHistoryCache.GetNodeRandomForRound(101691, 1);

            // Assert
            Assert.That(random, Is.EqualTo(100));
        }
    }

}
