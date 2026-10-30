using System;
using DefaultEcs;
using System.Linq;
using Sokoban.Core.Commands;
using Sokoban.Core.Components;
using System.Collections.Generic;

namespace Sokoban.Core
{
    public sealed class Simulation : IDisposable
    {
        private readonly LevelDefinition _initialLevel;

        private readonly Entity _player;
        private readonly Entity _levelState;
        private readonly SystemPipeline _pipeline;
        private readonly MoveHistory _history = new();
        
        private List<SimulationInput> _inputs;

        private readonly float _timeLimitSeconds;

        public World World { get; }

        public bool IsCompleted =>
            _levelState.Has<LevelCompletedTag>();

        public bool IsTimedOut =>
            _levelState.Has<LevelTimedOutTag>();

        public float RemainingSeconds =>
            _levelState.Get<LevelTimer>().RemainingSeconds;

        public LevelResult Result =>
            new(_levelState.Get<MoveCount>().Value,
                _timeLimitSeconds - RemainingSeconds,
                RemainingSeconds);

        public GridPosition PlayerPosition =>
            _player.Get<GridPosition>();

        public long BoardRevision =>
            _levelState.Get<BoardRevision>().Value;

        public bool CanUndo =>
            IsCompleted == false
            && IsTimedOut == false
            && _history.Entries.Count > 0;

        public Simulation(LevelDefinition level, LevelPopulator populator, float timeLimitSeconds, SimulationPipelineFactory pipelineFactory)
            : this(level, populator, timeLimitSeconds, pipelineFactory.Create, recordInputs: true) { }

        internal Simulation(LevelDefinition level, LevelPopulator populator, float timeLimitSeconds, Func<World, SystemPipeline> createPipeline, bool recordInputs = false)
        {
            if (float.IsNaN(timeLimitSeconds) || float.IsInfinity(timeLimitSeconds) || timeLimitSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(timeLimitSeconds));
            }

            _timeLimitSeconds = timeLimitSeconds;
            
            _initialLevel = level;
            
            _inputs = recordInputs 
                ? new List<SimulationInput>() 
                : null;
            
            World = new World();
            
            try
            {
                _player = populator.Populate(World, level);
                
                _levelState = World.CreateEntity();
                InitializeLevelState(timeLimitSeconds);

                _pipeline = createPipeline(World);
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
                
                _inputs?.Clear();
                _inputs = null;
                
                World.Dispose();
            }
        }

        public void Send(MoveCommand command)
        {
            _inputs?.Add(new SimulationInput(SimulationInputKind.Move, command));
            
            if (CanAcceptCommand(command) == false)
            {
                return;
            }

            _player.Set(new MoveRequest(command));
        }

        public bool RequestUndo()
        {
            _inputs?.Add(new SimulationInput(SimulationInputKind.Undo));
            
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
            _inputs?.Add(new SimulationInput(SimulationInputKind.Tick, deltaTime: deltaTime));
        }

        public SimulationRecording ExportRecording()
        {
            return _inputs == null 
                ? throw new InvalidOperationException("Recording export requires a live recording-enabled Simulation.") 
                : new SimulationRecording(_initialLevel, _timeLimitSeconds, _inputs);
        }

        public GameplaySnapshot CaptureSnapshot()
        {
            GridPosition playerPosition = PlayerPosition;

            IEnumerable<GridPosition> positions = World
                .Where(entity => entity.Has<BoxTag>())
                .Select(entity => entity.Get<GridPosition>());

            return new GameplaySnapshot(playerPosition, positions, Result, IsCompleted, IsTimedOut, CanUndo, BoardRevision);
        }

        private bool CanAcceptCommand(MoveCommand command)
        {
            return command is MoveCommand.Up or MoveCommand.Down or MoveCommand.Left or MoveCommand.Right
                   && _player.Has<MoveRequest>() == false && _levelState.Has<UndoRequest>() == false
                   && IsCompleted == false && IsTimedOut == false;
        }

        private void InitializeLevelState(float timeLimitSeconds)
        {
            _levelState.Set<LevelStateTag>();
            _levelState.Set(new LevelTimer(timeLimitSeconds));
            _levelState.Set(new MoveCount(0));
            _levelState.Set(new BoardRevision(0));
            _levelState.Set(_history);
        }
    }
}
