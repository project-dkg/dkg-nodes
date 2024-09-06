// RoundsCacheTests.cs
using dkgServiceNode.Models;
using dkgServiceNode.Services.Cache;

namespace dkgNodesTests
{
    [TestFixture]
    public class RoundsCacheTests
    {
        private readonly RoundsCache roundsCache = new();
        [SetUp]
        public void SetUp()
        {
            roundsCache.DeleteRoundFromCache(1);
            roundsCache.DeleteRoundFromCache(2);
        }

        [Test]
        public void LoadRoundsToCache_ShouldLoadRounds()
        {
            // Arrange
            var context = new MockDkgContext();
            context.Rounds.Add(new Round { Id = 1, Result = 100 });
            context.Rounds.Add(new Round { Id = 2, Result = 200 });

            // Act
            roundsCache.LoadRoundsToCache(context.Rounds);

            // Assert
            var rounds = roundsCache.GetAllRounds();
            Assert.Multiple(() =>
            {
                Assert.That(rounds.Count, Is.EqualTo(2));
                Assert.That(rounds.First(r => r.Id == 1).Result, Is.EqualTo(100));
                Assert.That(rounds.First(r => r.Id == 2).Result, Is.EqualTo(200));
            });
        }

        [Test]
        public void GetRoundById_ShouldReturnCorrectRound()
        {
            // Arrange
            var round = new Round { Id = 1, Result = 100 };
            roundsCache.AddRoundToCache(round);

            // Act
            var result = roundsCache.GetRoundById(1);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Result, Is.EqualTo(100));
        }

        [Test]
        public void AddRoundToCache_ShouldAddRound()
        {
            // Arrange
            var round = new Round { Id = 1, Result = 100 };

            // Act
            roundsCache.AddRoundToCache(round);

            // Assert
            var result = roundsCache.GetRoundById(1);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Result, Is.EqualTo(100));
        }

        [Test]
        public void UpdateRoundInCache_ShouldUpdateRound()
        {
            // Arrange
            var round = new Round { Id = 1, Result = 100 };
            roundsCache.AddRoundToCache(round);

            // Act
            round.Result = 200;
            roundsCache.UpdateRoundInCache(round);

            // Assert
            var result = roundsCache.GetRoundById(1);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Result, Is.EqualTo(200));
        }

        [Test]
        public void DeleteRoundFromCache_ShouldRemoveRound()
        {
            // Arrange
            var round = new Round { Id = 1, Result = 100 };
            roundsCache.AddRoundToCache(round);

            // Act
            roundsCache.DeleteRoundFromCache(1);

            // Assert
            var result = roundsCache.GetRoundById(1);
            Assert.That(result, Is.Null);
        }

        [Test]
        public void RoundExists_ShouldReturnTrueIfExists()
        {
            // Arrange
            var round = new Round { Id = 1, Result = 100 };
            roundsCache.AddRoundToCache(round);

            // Act
            var exists = roundsCache.RoundExists(1);

            // Assert
            Assert.That(exists, Is.True);
        }

        [Test]
        public void RoundExists_ShouldReturnFalseIfNotExists()
        {
            // Act
            var exists = roundsCache.RoundExists(1);

            // Assert
            Assert.That(exists, Is.False);
        }

        [Test]
        public void LastRoundResult_ShouldReturnLastRoundResult()
        {
            // Arrange
            roundsCache.AddRoundToCache(new Round { Id = 1, Result = 100 });
            roundsCache.AddRoundToCache(new Round { Id = 2, Result = 200 });

            // Act
            var result = roundsCache.LastRoundResult();

            // Assert
            Assert.That(result, Is.EqualTo(200));
        }
    }

    // Mock DkgContext for testing purposes
    public class MockDkgContext
    {
        public List<Round> Rounds { get; set; } = new List<Round>();
    }
}
