using System;
using Sokoban.Core;
using Sokoban.Core.Commands;
using UnityEngine;

namespace Sokoban.Presentation
{
    internal sealed class LevelRun : IDisposable
    {
        private readonly ViewLayer _viewLayer;
        private readonly Simulation _simulation;
        private readonly LevelDefinition _level;

        private MoveCommand _bufferedMove;

        public ViewStatus ViewStatus => _viewLayer.Status;
        
        public bool IsCompleted => _simulation.IsCompleted;
        
        public bool IsTimedOut => _simulation.IsTimedOut;
        
        public float RemainingSeconds => _simulation.RemainingSeconds;
        
        public LevelResult Result => _simulation.Result;

        public bool CanUndo => ViewStatus == ViewStatus.Ready && _viewLayer.IsMoving == false && _simulation.CanUndo;

        public bool CanHint => ViewStatus == ViewStatus.Ready && _viewLayer.IsMoving == false && IsCompleted == false && IsTimedOut == false;
        public long BoardRevision => _simulation.BoardRevision;
        public bool HasHintArrow => _viewLayer.HasHintArrow;

        public LevelRun(LevelRoot root, LevelDefinition level, IViewProvider provider, LevelPopulator populator,
            SimulationPipelineFactory simulationPipelineFactory, ViewPipelineFactory viewPipelineFactory, RunOptions options)
        {
            _level = level;
            try
            {
                _simulation = new Simulation(level, populator, options.TimeLimitSeconds, simulationPipelineFactory);
                _viewLayer = new ViewLayer(root, _simulation.World, provider, viewPipelineFactory, options);
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

        public bool RequestUndo()
        {
            if (CanUndo == false || _simulation.RequestUndo() == false)
            {
                return false;
            }

            _bufferedMove = default;
            return true;
        }

        public LevelDefinition CaptureHintBoard()
        {
            GameplaySnapshot snapshot = _simulation.CaptureSnapshot();
            return new LevelDefinition(_level.Width, _level.Height, snapshot.PlayerPosition, _level.Walls, snapshot.BoxPositions, _level.Goals);
        }

        public void ShowHint(GameObject prefab, MoveCommand move) =>
            _viewLayer.ShowHint(prefab, _simulation.CaptureSnapshot().PlayerPosition, move);

        public void HideHint() =>
            _viewLayer.HideHint();

        public void Send(MoveCommand command)
        {
            if (command is not (MoveCommand.Up or MoveCommand.Down or MoveCommand.Left or MoveCommand.Right)
                || _bufferedMove != 0 || _simulation.IsCompleted || _simulation.IsTimedOut) return;

            _bufferedMove = command;
        }
    }
}
