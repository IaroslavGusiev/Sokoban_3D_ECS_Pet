using System;
using DefaultEcs;
using Sokoban.Core.Commands;
using Sokoban.Core.Components;
using System.Collections.Generic;

namespace Sokoban.Core
{
    public sealed class Simulation : IDisposable
    {
        private readonly Entity _player;
        private readonly Entity _levelState;
        private readonly SystemPipeline _pipeline;
        private readonly MoveHistory _history = new();
        
        private readonly float _timeLimitSeconds;

        public World World { get; }

        public bool IsTimedOut => _levelState.Has<LevelTimedOutTag>();

        public bool IsCompleted => _levelState.Has<LevelCompletedTag>();

        public bool CanUndo => IsCompleted == false && IsTimedOut == false && _history.Entries.Count > 0;

        public float RemainingSeconds => _levelState.Get<LevelTimer>().RemainingSeconds;
        
        public LevelResult Result => new(_levelState.Get<MoveCount>().Value, _timeLimitSeconds - RemainingSeconds, RemainingSeconds);

        public Simulation(LevelDefinition level, LevelPopulator populator, float timeLimitSeconds)
            : this(level, populator, timeLimitSeconds, RuleFeatures.Create()) { }

        internal Simulation(LevelDefinition level, LevelPopulator populator, float timeLimitSeconds, IReadOnlyList<IFeature> features)
        {
            if (float.IsNaN(timeLimitSeconds) || float.IsInfinity(timeLimitSeconds) || timeLimitSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(timeLimitSeconds));
            }

            _timeLimitSeconds = timeLimitSeconds;
            World = new World();
            
            try
            {
                _player = populator.Populate(World, level);
                _levelState = World.CreateEntity();
                _levelState.Set<LevelStateTag>();
                _levelState.Set(new LevelTimer(timeLimitSeconds));
                _levelState.Set(new MoveCount(0));
                _levelState.Set(_history);
                
                _pipeline = new SystemPipeline(World, features);
            }
            catch
            {
                World.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            try
            {
                _pipeline.Dispose();
            }
            finally
            {
                _history.Entries.Clear();
                World.Dispose();
            }
        }

        public void Send(MoveCommand command)
        {
            if (CanAcceptCommand(command) == false)
            {
                return;
            }

            _player.Set(new MoveRequest(command));
        }

        public bool RequestUndo()
        {
            if (CanUndo == false)
            {
                return false;
            }

            _levelState.Set<UndoRequest>();
            return true;
        }
        
        public void Tick(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            }

            _pipeline.Tick(deltaTime);
        }

        private bool CanAcceptCommand(MoveCommand command)
        {
            return command is MoveCommand.Up or MoveCommand.Down or MoveCommand.Left or MoveCommand.Right
                   && _player.Has<MoveRequest>() == false && _levelState.Has<UndoRequest>() == false
                   && IsCompleted == false && IsTimedOut == false;
        }
    }
}
