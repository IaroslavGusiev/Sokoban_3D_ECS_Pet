using DefaultEcs;
using UnityEngine;
using DefaultEcs.System;
using Sokoban.Core.Components;
using Sokoban.Presentation.Ecs.Components;

namespace Sokoban.Presentation.Ecs.Systems
{
    public sealed class GridTransformSyncSystem : AEntitySetSystem<float>
    {
        public GridTransformSyncSystem(World world)
            : base(world.GetEntities()
                .With<GridPosition>()
                .With<TransformComponent>()
                .AsSet(), useBuffer: true) { }
        
        protected override void Update(float deltaTime, in Entity entity)
        {
            Transform transform = entity
                .Get<TransformComponent>()
                .Transform;

            if (transform == null)
            {
                return;
            }

            Vector3 target = entity.Has<ViewMove>()
                ? entity.Get<ViewMove>().Position
                : GridCoordinates.ToWorld(entity.Get<GridPosition>());
            
            if (transform.position != target)
            {
                transform.position = target;
            }

            if (entity.Has<PlayerTag>() && entity.Has<ActorViewState>())
            {
                var state = entity.Get<ActorViewState>();
                
                if (state.HasFacing && transform.rotation != state.Facing)
                {
                    transform.rotation = state.Facing;
                }
            }
        }
    }
}
