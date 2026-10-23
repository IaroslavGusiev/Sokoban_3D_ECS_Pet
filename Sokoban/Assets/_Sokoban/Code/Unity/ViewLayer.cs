using DefaultEcs;
using UnityEngine;
using Sokoban.Core;
using System.Threading;
using Sokoban.Core.Components;
using Sokoban.Presentation.Ecs.Components;

namespace Sokoban.Presentation
{
    public sealed class ViewLayer : IWorldLayer
    {
        private readonly Transform _root;
        private readonly EntitySet _players;
        private readonly EntitySet _failedViews;
        private readonly EntitySet _missingViews;
        private readonly SystemPipeline _pipeline;
        private readonly CancellationTokenSource _cancellation = new();

        public ViewStatus Status => _failedViews.Count > 0
            ? ViewStatus.Failed
            : _missingViews.Count > 0 ? ViewStatus.Binding : ViewStatus.Ready;

        public ViewLayer(LevelRoot root, World world, IViewProvider provider, ViewFeatures features, SkinId initialSkin = SkinId.Default)
        {
            try
            {
                _root = new GameObject("Level view").transform;
                _root.SetParent(root.Transform, false);
                
                _missingViews = ViewQuery
                    .Viewable(world)
                    .Without<TransformComponent>()
                    .AsSet();
                
                _failedViews = world
                    .GetEntities()
                    .With<ViewFailed>()
                    .AsSet();
                
                _players = world
                    .GetEntities()
                    .With<PlayerTag>()
                    .AsSet();
                
                foreach (Entity player in _players.GetEntities())
                {
                    player.Set(new ViewSkin(initialSkin));
                }
                
                var context = new ViewContext(_root, provider, _cancellation.Token);
                _pipeline = new SystemPipeline(world, features.Create(context));
            }
            catch
            {
                _cancellation.Cancel();
                DestroyRoot();
                _players?.Dispose();
                _failedViews?.Dispose();
                _missingViews?.Dispose();
                _cancellation.Dispose();
                throw;
            }
        }

        public void SetPlayerSkin(SkinId skin)
        {
            foreach (Entity player in _players.GetEntities())
            {
                player.Set(new SkinChangeRequest(skin));
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
                    _failedViews.Dispose();
                    _missingViews.Dispose();
                    _cancellation.Dispose();
                }
            }
        }

        public void Tick(float deltaTime) => 
            _pipeline.Tick(deltaTime);

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
