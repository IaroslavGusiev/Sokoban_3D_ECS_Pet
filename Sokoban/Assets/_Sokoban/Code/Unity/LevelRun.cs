using System;
using Sokoban.Core;
using Sokoban.Core.Commands;

namespace Sokoban.Presentation
{
    internal sealed class LevelRun : IDisposable
    {
        private readonly Simulation _simulation;
        private readonly ViewLayer _viewLayer;

        public ViewStatus ViewStatus => _viewLayer.Status;
        
        public bool IsCompleted => _simulation.IsCompleted;

        public LevelRun(LevelRoot root, LevelDefinition level, IViewProvider provider, ViewFeatures features, SkinId initialSkin)
        {
            try
            {
                _simulation = new Simulation(level);
                _viewLayer = new ViewLayer(root, _simulation.World, provider, features, initialSkin);
                _viewLayer.Tick(0f);
            }
            catch
            {
                try
                {
                    _viewLayer?.Dispose();
                }
                finally
                {
                    _simulation?.Dispose();
                }

                throw;
            }
        }

        public void Tick(float deltaTime)
        {
            _simulation.Tick(deltaTime);
            _viewLayer.Tick(deltaTime);
        }

        public void Dispose()
        {
            try
            {
                _viewLayer.Dispose();
            }
            finally
            {
                _simulation.Dispose();
            }
        }

        public void SetPlayerSkin(SkinId skin) => 
            _viewLayer.SetPlayerSkin(skin);

        public void Send(MoveCommand command) => 
            _simulation.Send(command);
    }
}
