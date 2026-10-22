using Sokoban.Core;
using Sokoban.Core.Components;
using System.Collections.Generic;

namespace Sokoban.Unity
{
    public static class HardcodedLevel
    {
        public static LevelDefinition Create()
        {
            var walls = new List<GridPosition>();
            
            for (var y = 0; y < 7; y++)
            {
                for (var x = 0; x < 7; x++)
                {
                    if (x == 0 || x == 6 || y == 0 || y == 6)
                    {
                        walls.Add(new GridPosition(x, y));
                    }
                }
            }

            return new LevelDefinition(7, 7, new GridPosition(3, 2), walls,
                new[] { new GridPosition(2, 3), new GridPosition(4, 3) },
                new[] { new GridPosition(2, 4), new GridPosition(4, 4) });
        }
    }
}
