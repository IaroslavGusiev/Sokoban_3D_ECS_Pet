using System;
using DefaultEcs;
using UnityEngine;
using DefaultEcs.System;
using Cysharp.Threading.Tasks;
using Sokoban.Core.Components;
using Object = UnityEngine.Object;
using Sokoban.Presentation.Ecs.Model;
using Sokoban.Presentation.Ecs.Components;

namespace Sokoban.Presentation.Ecs.Systems
{
    public sealed class ViewBindingSystem : AEntitySetSystem<float>
    {
        private readonly ViewContext _context;

        public ViewBindingSystem(World world, ViewContext context)
            : base(ViewQuery.Viewable(world)
                .Without<ViewPending>()
                .Without<ViewFailed>()
                .Without<TransformComponent>()
                .AsSet(), useBuffer: true)
        {
            _context = context;
        }

        protected override void Update(float deltaTime, in Entity entity)
        {
            entity.Set<ViewPending>();
            
            BindAsync(entity, ViewKeyMap.Get(entity))
                .Forget();
        }

        private async UniTaskVoid BindAsync(Entity entity, ViewKey key)
        {
            // Entity identity, view tags and GridPosition presence stay stable for this run.
            // Position values may change while awaiting; read them after checking run cancellation.
            GameObject prefab;
            
            try
            {
                prefab = await _context.Provider.LoadAsync(key, _context.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception)
            {
                Fail(entity, key);
                return;
            }

            if (_context.Token.IsCancellationRequested)
            {
                return;
            }

            if (ViewKeyMap.Get(entity) != key)
            {
                entity.Remove<ViewPending>();
                return;
            }

            GameObject view = null;
            
            try
            {
                var position = entity.Get<GridPosition>();
                
                Vector3 viewPosition = entity.Has<ViewMove>() ? entity.Get<ViewMove>().Position : GridCoordinates.ToWorld(position);
                Quaternion rotation = entity.Has<PlayerTag>() && entity.Has<ActorViewState>() && entity.Get<ActorViewState>().HasFacing
                    ? entity.Get<ActorViewState>().Facing : prefab.transform.rotation;
                view = Object.Instantiate(prefab, viewPosition, rotation, _context.Root);
                view.name = $"{key} ({position.X}, {position.Y})";
                
                if (view.TryGetComponent(out EntityView entityView))
                {
                    entityView.Install(entity);
                }

                entity.Set(new TransformComponent(view.transform));
                entity.Remove<ViewPending>();
            }
            catch (Exception exception)
            {
                if (view != null)
                {
                    Object.Destroy(view);
                }

                if (_context.Token.IsCancellationRequested == false)
                {
                    Debug.LogException(exception);
                    Fail(entity, key);
                }
            }
        }

        private void Fail(Entity entity, ViewKey key)
        {
            if (_context.Token.IsCancellationRequested) return;
            if (ViewKeyMap.Get(entity) != key)
            {
                entity.Remove<ViewPending>();
                return;
            }

            entity.Set<ViewFailed>();
            entity.Remove<ViewPending>();
        }
    }
}
