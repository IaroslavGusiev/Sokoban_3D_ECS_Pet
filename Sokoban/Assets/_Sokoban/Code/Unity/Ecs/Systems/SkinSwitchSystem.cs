using DefaultEcs;
using UnityEngine;
using DefaultEcs.System;
using Sokoban.Presentation.Ecs.Model;
using Sokoban.Presentation.Ecs.Components;

namespace Sokoban.Presentation.Ecs.Systems
{
    public sealed class SkinSwitchSystem : AEntitySetSystem<float>
    {
        public SkinSwitchSystem(World world)
            : base(world.GetEntities().With<SkinChangeRequest>().AsSet(), useBuffer: true) { }

        protected override void Update(float deltaTime, in Entity entity)
        {
            SkinId skin = entity.Get<SkinChangeRequest>().Id;
            
            if (entity.Has<ViewOwnership>())
            {
                entity
                    .Get<ViewOwnership>()
                    .ReleaseView();
            }
            
            if (entity.Has<TransformComponent>())
            {
                Transform transform = entity.Get<TransformComponent>().Transform;
                
                if (transform != null)
                {
                    if (transform.TryGetComponent(out EntityView view))
                    {
                        view.Uninstall(entity);
                    }
                    
                    Object.Destroy(transform.gameObject);
                }
                entity.Remove<TransformComponent>();
            }

            entity.Set(new ViewSkin(skin));
            entity.Remove<ViewFailed>();
            entity.Remove<SkinChangeRequest>();
        }
    }
}
