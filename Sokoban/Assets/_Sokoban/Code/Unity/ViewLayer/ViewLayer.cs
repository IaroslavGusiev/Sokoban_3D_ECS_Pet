using System;
using DefaultEcs;
using UnityEngine;
using Sokoban.Core;
using System.Threading;
using Sokoban.Core.Commands;
using Sokoban.Core.Components;
using Object = UnityEngine.Object;
using Sokoban.Presentation.Ecs.Components;

namespace Sokoban.Presentation
{
    public sealed class ViewLayer : IDisposable
    {
        private readonly Transform _root;
        private readonly EntitySet _players;
        private readonly EntitySet _failedViews;
        private readonly EntitySet _missingViews;
        private readonly EntitySet _movingViews;
        private readonly SystemPipeline _pipeline;
        private readonly CancellationTokenSource _cancellation = new();
        
        private HintArrow _hintArrow;

        public bool HasBoardFailure
        {
            get
            {
                foreach (Entity entity in _failedViews.GetEntities())
                {
                    if (entity.Has<PlayerTag>() == false)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool HasPlayerFailure
        {
            get
            {
                foreach (Entity player in _players.GetEntities())
                {
                    if (player.Has<ViewFailed>())
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool IsPlayerDeferred
        {
            get
            {
                foreach (Entity player in _players.GetEntities())
                {
                    if (player.Has<PlayerViewDeferred>())
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool IsMoving => _movingViews.Count > 0;

        public bool HasHintArrow => _hintArrow != null;

        public ViewStatus Status =>
            _failedViews.Count > 0
                ? ViewStatus.Failed
                : _missingViews.Count > 0
                    ? ViewStatus.Binding
                    : ViewStatus.Ready;

        public ViewLayer(LevelRoot root, World world, IViewProvider provider, ViewPipelineFactory pipelineFactory, RunOptions options)
        {
            try
            {
                _root = new GameObject("Level view").transform;
                _root.SetParent(root.Transform, false);
                
                _missingViews = ViewQuery
                    .Viewable(world)
                    .Without<TransformComponent>()
                    .Without<PlayerViewDeferred>()
                    .AsSet();
                
                _failedViews = world
                    .GetEntities()
                    .With<ViewFailed>()
                    .AsSet();
                
                _players = world
                    .GetEntities()
                    .With<PlayerTag>()
                    .AsSet();

                _movingViews = world.GetEntities().With<ViewMove>().AsSet();
                
                foreach (Entity player in _players.GetEntities())
                {
                    player.Set(new ViewSkin(options.Skin));
                    if (options.DeferPlayerView)
                    {
                        player.Set<PlayerViewDeferred>();
                    }
                }
                
                var context = new ViewContext(_root, provider, _cancellation.Token, options.MoveDurationSeconds);
                _pipeline = pipelineFactory.Create(world, context);
            }
            catch
            {
                _cancellation.Cancel();
                DestroyRoot();
                
                _players?.Dispose();
                _movingViews?.Dispose();
                _failedViews?.Dispose();
                _missingViews?.Dispose();
                _cancellation.Dispose();
                
                throw;
            }
        }

        public void Tick(float deltaTime) => 
            _pipeline.Tick(deltaTime);

        public void SetPlayerSkin(SkinId skin)
        {
            foreach (Entity player in _players.GetEntities())
            {
                player.Set(new SkinChangeRequest(skin));
                player.Remove<PlayerViewDeferred>();
            }
        }

        public void Dispose()
        {
            try
            {
                _cancellation.Cancel();
                DestroyRoot();
            }
            finally
            {
                try
                {
                    _pipeline.Dispose();
                }
                finally
                {
                    _players.Dispose();
                    _movingViews.Dispose();
                    _failedViews.Dispose();
                    _missingViews.Dispose();
                    _cancellation.Dispose();
                }
            }
        }

        public void ShowHint(GameObject prefab, GridPosition player, MoveCommand move)
        {
            if (_cancellation.IsCancellationRequested)
            {
                throw new ObjectDisposedException(nameof(ViewLayer));
            }
            
            if (_hintArrow == null)
            {
                HintArrow arrow = prefab != null 
                    ? prefab.GetComponent<HintArrow>() 
                    : null;
                
                if (arrow == null)
                {
                    throw new InvalidOperationException("The hint arrow view must contain HintArrow.");
                }
                
                _hintArrow = Object.Instantiate(arrow, _root);
            }
            
            _hintArrow.Show(player, move);
        }

        public void HideHint()
        {
            if (_hintArrow != null)
            {
                _hintArrow.gameObject.SetActive(false);
            }
        }

        private void DestroyRoot()
        {
            if (_root != null)
            {
                _root.gameObject.SetActive(false);
                Object.Destroy(_root.gameObject);
            }
        }
    }
}
