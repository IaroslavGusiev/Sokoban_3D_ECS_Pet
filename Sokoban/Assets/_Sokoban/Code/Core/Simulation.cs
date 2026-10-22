using System;
using DefaultEcs;
using Sokoban.Core.Commands;
using Sokoban.Core.Components;
using System.Collections.Generic;

namespace Sokoban.Core
{
    public sealed class Simulation : IWorldLayer
    {
        private readonly Entity _player;
        private readonly SystemPipeline _pipeline;

        public World World { get; }

        public Simulation(LevelDefinition level) 
            : this(level, RuleFeatures.Create()) { }

        internal Simulation(LevelDefinition level, IReadOnlyList<IFeature> features)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            World = new World();
            
            try
            {
                _player = LevelLoader.Populate(World, level);
                _pipeline = new SystemPipeline(World, features);
            }
            catch
            {
                World.Dispose();
                throw;
            }
        }

        public void Tick(float deltaTime) => 
            _pipeline.Tick(deltaTime);

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
            if (command is not (MoveCommand.Up or MoveCommand.Down or MoveCommand.Left or MoveCommand.Right) 
                || _player.Has<MoveRequest>())
            {
                return;
            }

            _player.Set(new MoveRequest(command));
        }
    }
}