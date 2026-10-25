using DefaultEcs;
using DefaultEcs.System;
using Sokoban.Core.Components;

namespace Sokoban.Core.Systems
{
    internal sealed class MoveCountSystem : AEntitySetSystem<float>
    {
        public MoveCountSystem(World world)
            : base(world.GetEntities()
                .With<MoveCount>()
                .With<GridPosition>()
                .AsSet(), useBuffer: true) { }

        protected override void Update(float deltaTime, in Entity entity)
        {
            var count = entity.Get<MoveCount>();
            
            var position = entity.Get<GridPosition>();
            
            if (position != count.LastPosition)
            {
                entity.Set(new MoveCount(count.Value + 1, position));
            }
        }
    }
}
