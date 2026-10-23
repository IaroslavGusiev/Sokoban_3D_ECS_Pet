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

            transform.position = GridCoordinates.ToWorld(entity.Get<GridPosition>());
        }
    }
}
