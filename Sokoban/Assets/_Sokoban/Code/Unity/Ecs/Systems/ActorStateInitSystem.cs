using DefaultEcs;
using DefaultEcs.System;
using Sokoban.Core.Components;
using Sokoban.Presentation.Ecs.Components;

namespace Sokoban.Presentation.Ecs.Systems
{
    internal sealed class ActorStateInitSystem : AEntitySetSystem<float>
    {
        public ActorStateInitSystem(World world)
            : base(ActorQuery
                .Actors(world)
                .Without<ObservedPosition>()
                .AsSet(), 
                useBuffer: true) { }

        protected override void Update(float deltaTime, in Entity entity)
        {
            entity.Set(new ObservedPosition(entity.Get<GridPosition>()));
        }
    }
}
