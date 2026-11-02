using DefaultEcs;
using Sokoban.Core.Components;

namespace Sokoban.Presentation
{
    internal static class ActorQuery
    {
        public static EntityQueryBuilder Actors(World world) =>
            world.GetEntities()
                .WithEither<PlayerTag>().Or<BoxTag>()
                .With<GridPosition>();
    }
}
