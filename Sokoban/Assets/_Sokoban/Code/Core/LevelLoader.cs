using DefaultEcs;
using Sokoban.Core.Components;

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

            foreach (GridPosition position in level.Goals)
            {
                CreateEntity<GoalTag>(world, position);
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
