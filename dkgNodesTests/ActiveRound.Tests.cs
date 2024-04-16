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
            var nodes = new List<Node> { new() { Host = "localhost", Port = 5000 } };

            activeRound.Run(nodes);

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Run")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public void GetResult_WithNodes_ReturnsResult()
        {
            var round = new Round { Id = 5 };
            var activeRound = new ActiveRound(round, _loggerMock.Object);
            var nodes = new List<Node> { new() { Host = "localhost", Port = 5000 } };

            activeRound.Run(nodes);
            var result = activeRound.GetResult();

            Assert.That(result, Is.Null);
        }

        [Test]
        public void Clear_WithNodes_CallsClearInternal()
        {
            var round = new Round { Id = 5 };
            var activeRound = new ActiveRound(round, _loggerMock.Object);
            var nodes = new List<Node> { new() { Host = "localhost", Port = 5000 } };

            activeRound.Run(nodes);
            activeRound.Clear(nodes);

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("ClearInternal")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

    }
}