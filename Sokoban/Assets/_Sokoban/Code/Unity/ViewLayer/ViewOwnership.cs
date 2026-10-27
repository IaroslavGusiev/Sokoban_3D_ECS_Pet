using System;
using DefaultEcs;
using UnityEngine;
using Object = UnityEngine.Object;
using Sokoban.Presentation.Ecs.Model;
using Sokoban.Presentation.Ecs.Components;

namespace Sokoban.Presentation
{
    internal sealed class ViewOwnership : IDisposable
    {
        public Entity Entity { get; }
        public GameObject View { get; set; }
        public ViewLoadOperation Request { get; set; }

        public bool IsAlive => Entity is { World: not null, IsAlive: true };

        public ViewOwnership(Entity entity) =>
            Entity = entity;

        public void ReleaseView()
        {
            GameObject view = View;
            View = null;
            
            if (view == null)
            {
                return;
            }

            try
            {
                if (IsAlive == false)
                {
                    return;
                }
                
                if (view.TryGetComponent(out EntityView entityView))
                {
                    entityView.Uninstall(Entity);
                }

                if (Entity.Has<TransformComponent>() && Entity.Get<TransformComponent>().Transform == view.transform)
                {
                    Entity.Remove<TransformComponent>();
                }
            }
            finally
            {
                view.SetActive(false);
                Object.Destroy(view);
            }
        }

        public void Dispose()
        {
            Request?.Cancel();
            Request = null;
            
            try
            {
                ReleaseView();
            }
            finally
            {
                if (IsAlive && Entity.Has<ViewOwnership>() && ReferenceEquals(Entity.Get<ViewOwnership>(), this))
                {
                    Entity.Remove<ViewPending>();
                    Entity.Remove<ViewOwnership>();
                }
            }
        }
    }
}
