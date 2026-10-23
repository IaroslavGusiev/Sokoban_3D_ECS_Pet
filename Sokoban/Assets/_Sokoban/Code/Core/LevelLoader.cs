using DefaultEcs;
using Sokoban.Core.Components;
using System.Collections.Generic;

namespace Sokoban.Core
{
    internal static class LevelLoader
    {
        internal static Entity Populate(World world, LevelDefinition level)
        {
            Entity player = CreateEntity<PlayerTag>(world, level.PlayerStart);
            
            foreach (GridPosition position in level.Walls)
            {
                CreateEntity<WallTag>(world, position);
            }

            foreach (GridPosition position in level.Boxes)
            {
                CreateEntity<BoxTag>(world, position);
            }

            var goals = new HashSet<GridPosition>(level.Goals);
            
            for (var y = 0; y < level.Height; y++)
            {
                for (var x = 0; x < level.Width; x++)
                {
                    var position = new GridPosition(x, y);
                    Entity floor = CreateEntity<FloorTag>(world, position);
                    
                    if (goals.Contains(position))
                    {
                        floor.Set<GoalTag>();
                    }
                }
            }

            return player;
        }

        private static Entity CreateEntity<T>(World world, GridPosition position) where T : struct
        {
            Entity entity = world.CreateEntity();
            
            entity.Set(position);
            entity.Set<T>();
            
            return entity;
        }
    }
}
