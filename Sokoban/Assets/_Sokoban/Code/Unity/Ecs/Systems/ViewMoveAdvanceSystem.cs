using DefaultEcs;
using DefaultEcs.System;
using Sokoban.Presentation.Ecs.Components;

namespace Sokoban.Presentation.Ecs.Systems
{
    internal sealed class ViewMoveAdvanceSystem : AEntitySetSystem<float>
    {
        public ViewMoveAdvanceSystem(World world)
            : base(ActorQuery
                .Actors(world)
                .With<ViewMove>()
                .AsSet(), 
                useBuffer: true) { }

        protected override void Update(float deltaTime, in Entity entity)
        {
            ViewMove move = entity
                .Get<ViewMove>()
                .Advance(deltaTime);
            
            if (move.IsDone)
            {
                entity.Remove<ViewMove>();
            }
            
            else entity.Set(move);
        }
    }
}
