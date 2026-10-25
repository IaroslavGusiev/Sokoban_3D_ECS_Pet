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
        private readonly SystemPipeline _pipeline;
        
        private readonly float _timeLimitSeconds;

        public World World { get; }

        public bool IsTimedOut => _player.Has<LevelTimedOutTag>();

        public bool IsCompleted => _player.Has<LevelCompletedTag>();

        public float RemainingSeconds => _player.Get<LevelTimer>().RemainingSeconds;
        
        public LevelResult Result => new(_player.Get<MoveCount>().Value, _timeLimitSeconds - RemainingSeconds, RemainingSeconds);

        public Simulation(LevelDefinition level, LevelPopulator populator, float timeLimitSeconds)
            : this(level, populator, timeLimitSeconds, RuleFeatures.Create()) { }

        internal Simulation(LevelDefinition level, LevelPopulator populator, float timeLimitSeconds, IReadOnlyList<IFeature> features)
        {
            _timeLimitSeconds = timeLimitSeconds;
            World = new World();
            
            try
            {
                _player = populator.Populate(World, level);
                _player.Set(new LevelTimer(timeLimitSeconds));
                _player.Set(new MoveCount(0, _player.Get<GridPosition>()));
                
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
        
        public void Tick(float deltaTime) => 
            _pipeline.Tick(deltaTime);

        private bool CanAcceptCommand(MoveCommand command)
        {
            return command is MoveCommand.Up or MoveCommand.Down or MoveCommand.Left or MoveCommand.Right
                   && _player.Has<MoveRequest>() == false && IsCompleted == false && IsTimedOut == false;
        }
    }
}
