using System.Collections.Generic;
using NUnit.Framework;
using Sokoban.Core.Components;

namespace Sokoban.Tests.EditMode
{
    public sealed class GridPositionTests
    {
        [TestCase(0, 0)]
        [TestCase(2, 3)]
        [TestCase(-2, -3)]
        [TestCase(int.MinValue, int.MaxValue)]
        [TestCase(int.MaxValue, int.MinValue)]
        public void EqualCoordinates_AreEqualAndHaveEqualHashCodes(int x, int y)
        {
            var first = new GridPosition(x, y);
            var second = new GridPosition(x, y);

            Assert.That(first.Equals(second), Is.True);
            Assert.That(second.Equals(first), Is.True);
            Assert.That(first.Equals((object)second), Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }

        [TestCase(2, 3, 4, 3)]
        [TestCase(2, 3, 2, 4)]
        [TestCase(2, 3, 3, 2)]
        [TestCase(-2, -3, -2, 3)]
        [TestCase(int.MinValue, 0, int.MaxValue, 0)]
        [TestCase(0, int.MinValue, 0, int.MaxValue)]
        public void DifferentCoordinates_AreNotEqual(int firstX, int firstY, int secondX, int secondY)
        {
            var first = new GridPosition(firstX, firstY);
            var second = new GridPosition(secondX, secondY);

            Assert.That(first.Equals(second), Is.False);
            Assert.That(second.Equals(first), Is.False);
            Assert.That(first.Equals((object)second), Is.False);
        }

        [TestCase(0, 0, 0, 0, true)]
        [TestCase(-2, -3, -2, -3, true)]
        [TestCase(int.MinValue, int.MaxValue, int.MinValue, int.MaxValue, true)]
        [TestCase(2, 3, 4, 3, false)]
        [TestCase(2, 3, 2, 4, false)]
        public void EqualityOperators_CompareBothCoordinates(
            int firstX, int firstY, int secondX, int secondY, bool expectedEqual)
        {
            var first = new GridPosition(firstX, firstY);
            var second = new GridPosition(secondX, secondY);

            Assert.That(first == second, Is.EqualTo(expectedEqual));
            Assert.That(first != second, Is.EqualTo(!expectedEqual));
            Assert.That(second == first, Is.EqualTo(expectedEqual));
            Assert.That(second != first, Is.EqualTo(!expectedEqual));
        }

        [Test]
        public void ObjectEquals_RejectsNullAndUnrelatedType()
        {
            var position = new GridPosition(2, 3);

            Assert.That(position.Equals((object)null), Is.False);
            Assert.That(position.Equals((object)"(2, 3)"), Is.False);
        }

        [Test]
        public void DefaultPosition_EqualsOrigin()
        {
            Assert.That(default(GridPosition).Equals(new GridPosition(0, 0)), Is.True);
        }

        [Test]
        public void Dictionary_UsesEquivalentPositionsAsTheSameKey()
        {
            var originalKey = new GridPosition(-2, 3);
            var equivalentKey = new GridPosition(-2, 3);
            var differentKey = new GridPosition(-2, 4);
            var positions = new Dictionary<GridPosition, string>
            {
                [originalKey] = "player"
            };

            Assert.That(positions[equivalentKey], Is.EqualTo("player"));
            Assert.That(positions.ContainsKey(differentKey), Is.False);

            positions[equivalentKey] = "box";

            Assert.That(positions.Count, Is.EqualTo(1));
            Assert.That(positions[originalKey], Is.EqualTo("box"));

            positions[differentKey] = "goal";

            Assert.That(positions.Count, Is.EqualTo(2));
            Assert.That(positions[originalKey], Is.EqualTo("box"));
            Assert.That(positions[differentKey], Is.EqualTo("goal"));
        }
    }
}
