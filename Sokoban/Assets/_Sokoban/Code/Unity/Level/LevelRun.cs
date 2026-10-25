using System;
using Sokoban.Core;
using Sokoban.Core.Commands;

namespace Sokoban.Presentation
{
    internal sealed class LevelRun : IDisposable
    {
        private readonly ViewLayer _viewLayer;
        private readonly Simulation _simulation;

        private MoveCommand _bufferedMove;

        public ViewStatus ViewStatus => _viewLayer.Status;
        
        public bool IsCompleted => _simulation.IsCompleted;
        
        public bool IsTimedOut => _simulation.IsTimedOut;
        
        public float RemainingSeconds => _simulation.RemainingSeconds;
        
        public LevelResult Result => _simulation.Result;

        public LevelRun(LevelRoot root, LevelDefinition level, IViewProvider provider, ViewFeatures features, LevelPopulator populator, RunOptions options)
        {
            try
            {
                _simulation = new Simulation(level, populator, options.TimeLimitSeconds);
                _viewLayer = new ViewLayer(root, _simulation.World, provider, features, options);
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
            if (_viewLayer.IsMoving == false && _bufferedMove != 0)
            {
                _simulation.Send(_bufferedMove);
                _bufferedMove = 0;
            }

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

        public void TickView(float deltaTime) => 
            _viewLayer.Tick(deltaTime);

        public void SetPlayerSkin(SkinId skin) => 
            _viewLayer.SetPlayerSkin(skin);

        public void Send(MoveCommand command)
        {
            if (command is not (MoveCommand.Up or MoveCommand.Down or MoveCommand.Left or MoveCommand.Right)
                || _bufferedMove != 0 || _simulation.IsCompleted || _simulation.IsTimedOut) return;

            _bufferedMove = command;
        }
    }
}
