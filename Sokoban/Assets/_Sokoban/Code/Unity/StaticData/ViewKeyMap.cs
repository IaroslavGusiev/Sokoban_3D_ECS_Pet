using System;
using DefaultEcs;
using Sokoban.Core.Components;
using Sokoban.Presentation.Ecs.Components;

namespace Sokoban.Presentation
{
    public static class ViewKeyMap
    {
        public static ViewKey Get(in Entity entity) => new ViewKey(GetKind(entity),
            entity.Has<ViewSkin>() ? entity.Get<ViewSkin>().Id : SkinId.Default);

        private static ViewKind GetKind(in Entity entity) => entity switch
        {
            _ when entity.Has<WallTag>() => ViewKind.Wall,
            _ when entity.Has<BoxTag>() => ViewKind.Box,
            _ when entity.Has<PlayerTag>() => ViewKind.Player,
            _ when entity.Has<GoalTag>() => ViewKind.Goal,
            _ when entity.Has<FloorTag>() => ViewKind.Floor,
            _ => throw new InvalidOperationException("Entity has no view tag.")
        };
    }
}
