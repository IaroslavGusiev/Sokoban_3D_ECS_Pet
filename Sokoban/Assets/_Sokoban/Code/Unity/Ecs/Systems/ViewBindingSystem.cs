using System;
using DefaultEcs;
using UnityEngine;
using DefaultEcs.System;
using Sokoban.Core.Components;
using System.Collections.Generic;
using Object = UnityEngine.Object;
using Sokoban.Presentation.Ecs.Model;
using Sokoban.Presentation.Ecs.Components;

namespace Sokoban.Presentation.Ecs.Systems
{
    public sealed class ViewBindingSystem : ISystem<float>
    {
        private readonly World _world;
        private readonly ViewContext _context;

        private readonly EntitySet _candidates;
        private readonly List<ViewOwnership> _snapshot = new();
        private readonly Dictionary<Entity, ViewOwnership> _owned = new();

        private bool _disposed;

        public bool IsEnabled { get; set; } = true;

        public ViewBindingSystem(World world, ViewContext context)
        {
            _world = world;
            _context = context;
            _candidates = ViewQuery.Viewable(world)
                .Without<ViewPending>()
                .Without<ViewFailed>()
                .Without<TransformComponent>()
                .AsSet();
        }

        public void Update(float deltaTime)
        {
            if (IsEnabled == false || _disposed)
            {
                return;
            }

            _snapshot.Clear();
            _snapshot.AddRange(_owned.Values);
            
            foreach (ViewOwnership ownership in _snapshot)
            {
                if (_context.Token.IsCancellationRequested || IsOwned(ownership) == false)
                {
                    _owned.Remove(ownership.Entity);
                    ownership.Dispose();
                }
            }

            if (_context.Token.IsCancellationRequested == false)
            {
                foreach (Entity entity in _candidates.GetEntities().ToArray())
                {
                    StartBinding(entity);
                }
                
                foreach (ViewOwnership ownership in _snapshot)
                {
                    ApplyCompletion(ownership);
                }
            }

            _snapshot.Clear();
        }

        public void Dispose()
        {
            _disposed = true;
            
            try
            {
                foreach (ViewOwnership ownership in _owned.Values)
                {
                    ownership.Dispose();
                }
            }
            finally
            {
                _owned.Clear();
                _snapshot.Clear();
                _candidates.Dispose();
            }
        }

        private bool IsOwned(ViewOwnership ownership)
        {
            Entity entity = ownership.Entity;
            
            return ReferenceEquals(entity.World, _world)
                   && ownership.IsAlive
                   && entity.Has<ViewOwnership>()
                   && ReferenceEquals(entity.Get<ViewOwnership>(), ownership);
        }

        private void StartBinding(Entity entity)
        {
            if (entity.IsAlive == false)
            {
                return;
            }

            if (_owned.TryGetValue(entity, out ViewOwnership ownership) == false)
            {
                ownership = new ViewOwnership(entity);
                _owned.Add(entity, ownership);
                entity.Set(ownership);
            }

            entity.Set<ViewPending>();
            ownership.Request = new ViewLoadOperation(_context.Provider, ViewKeyMap.Get(entity), _context.Token);
            ApplyCompletion(ownership);
        }

        private void ApplyCompletion(ViewOwnership ownership)
        {
            ViewLoadOperation request = ownership.Request;
            
            if (request == null || request.Task.IsCompleted == false || IsCurrent(ownership, request) == false)
            {
                return;
            }

            Entity entity = ownership.Entity;
            
            ViewLoadOperation.Completion completion = request.Task
                .GetAwaiter()
                .GetResult();
            
            if (ViewKeyMap.Get(entity) != request.Key || completion.IsCanceled)
            {
                FinishRequest(ownership);
                return;
            }

            if (completion.Error != null)
            {
                entity.Set<ViewFailed>();
                FinishRequest(ownership);
                return;
            }

            Bind(ownership, request, completion.Prefab);
        }

        private void Bind(ViewOwnership ownership, ViewLoadOperation request, GameObject prefab)
        {
            Entity entity = ownership.Entity;
            GameObject view = null;
            try
            {
                var position = entity.Get<GridPosition>();

                Vector3 viewPosition = entity.Has<ViewMove>()
                    ? entity.Get<ViewMove>().Position
                    : GridCoordinates.ToWorld(position);

                Quaternion rotation = entity.Has<PlayerTag>()
                                      && entity.Has<ActorFacing>()
                    ? entity.Get<ActorFacing>().Value
                    : prefab.transform.rotation;

                view = Object.Instantiate(prefab, viewPosition, rotation, _context.Root);
                view.name = $"{request.Key} ({position.X}, {position.Y})";
                
                if (IsCurrent(ownership, request) == false)
                {
                    return;
                }

                if (view.TryGetComponent(out EntityView entityView))
                {
                    entityView.Install(entity);
                }

                if (IsCurrent(ownership, request) == false)
                {
                    return;
                }

                ownership.View = view;
                view = null;
                entity.Set(new TransformComponent(ownership.View.transform));
                FinishRequest(ownership);
            }
            catch (Exception exception)
            {
                ownership.ReleaseView();
                if (IsCurrent(ownership, request))
                {
                    Debug.LogException(exception);
                    entity.Set<ViewFailed>();
                    FinishRequest(ownership);
                }
            }
            finally
            {
                if (view != null)
                {
                    view.SetActive(false);
                    Object.Destroy(view);
                }
            }
        }

        private static void FinishRequest(ViewOwnership ownership)
        {
            ownership.Entity.Remove<ViewPending>();
            ownership.Request = null;
        }

        private bool IsCurrent(ViewOwnership ownership, ViewLoadOperation request) =>
            _disposed == false
            && _context.Token.IsCancellationRequested == false
            && IsOwned(ownership)
            && ReferenceEquals(ownership.Request, request);
    }
}
