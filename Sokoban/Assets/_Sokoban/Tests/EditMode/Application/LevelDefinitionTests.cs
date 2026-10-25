using Sokoban.Core;
using System;
using System.Collections.Generic;
using NUnit.Framework;
using System.Linq;
using Sokoban.Core.Components;

namespace Sokoban.Tests.App
{
    public sealed class LevelDefinitionTests
    {
        private static LevelDefinition CreateLevel()
        {
            var walls = from y in Enumerable.Range(0, 7)
                        from x in Enumerable.Range(0, 7)
                        where x == 0 || x == 6 || y == 0 || y == 6
                        select new GridPosition(x, y);
            return new LevelDefinition(7, 7, new GridPosition(3, 2), walls,
                new[] { new GridPosition(2, 3), new GridPosition(4, 3) },
                new[] { new GridPosition(2, 4), new GridPosition(4, 4) });
        }

        [Test]
        public void Definition_PreservesItsDataWhenCallerChangesInputCollections()
        {
            var walls = new[] { new GridPosition(0, 0) };
            var boxes = new List<GridPosition> { new GridPosition(2, 1) };
            var goals = new[] { new GridPosition(2, 2) };
            var level = new LevelDefinition(4, 4, new GridPosition(1, 1), walls, boxes, goals);

            walls[0] = new GridPosition(9, 9);
            boxes.Clear();
            goals[0] = new GridPosition(8, 8);

            Assert.That(level.Width, Is.EqualTo(4));
            Assert.That(level.Height, Is.EqualTo(4));
            Assert.That(level.PlayerStart, Is.EqualTo(new GridPosition(1, 1)));
            Assert.That(level.Walls, Is.EqualTo(new[] { new GridPosition(0, 0) }));
            Assert.That(level.Boxes, Is.EqualTo(new[] { new GridPosition(2, 1) }));
            Assert.That(level.Goals, Is.EqualTo(new[] { new GridPosition(2, 2) }));
        }

        [Test]
        public void Definition_DoesNotExposeMutableCollections()
        {
            LevelDefinition level = CreateLevel();

            foreach (IReadOnlyList<GridPosition> positions in new[] { level.Walls, level.Boxes, level.Goals })
            {
                if (positions is IList<GridPosition> collection)
                {
                    Assert.Throws<NotSupportedException>(() => collection[0] = new GridPosition(99, 99));
                }
            }
        }
    }
}
