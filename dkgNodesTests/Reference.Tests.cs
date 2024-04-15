using dkgCommon.Models;

namespace dkgNodesTests
{
    [TestFixture]
    public class ReferenceTests
    {
        [Test]
        public void Constructor_Sets_Id()
        {
            // Arrange
            int expectedId = 5;

            // Act
            var reference = new Reference(expectedId);

            // Assert
            Assert.That(reference.Id, Is.EqualTo(expectedId));
        }

        [Test]
        public void Id_Can_Be_Changed()
        {
            // Arrange
            var reference = new Reference(1);
            int newId = 2;

            // Act
            reference.Id = newId;

            // Assert
            Assert.That(reference.Id, Is.EqualTo(newId));
        }
    }
}