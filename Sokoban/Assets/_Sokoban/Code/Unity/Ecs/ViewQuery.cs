using DefaultEcs;
using Sokoban.Core.Components;

namespace Sokoban.Presentation
{
    internal static class ViewQuery
    {
        public static EntityQueryBuilder Viewable(World world) =>
            world.GetEntities()
                .WithEither<WallTag>().Or<BoxTag>().Or<PlayerTag>().Or<GoalTag>().Or<FloorTag>()
                .With<GridPosition>();
    }
}
