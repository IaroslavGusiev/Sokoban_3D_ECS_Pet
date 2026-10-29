using System;

namespace Sokoban.Core
{
    public sealed class SimulationReplay : IDisposable
    {
        private readonly SimulationRecording _recording;
        private Simulation _simulation;
        
        private int _nextInput;

        public Simulation Simulation => _simulation ?? throw new ObjectDisposedException(nameof(SimulationReplay));
        
        public bool IsFinished => _nextInput == _recording.Inputs.Count;

        public SimulationReplay(SimulationRecording recording)
        {
            _recording = recording ?? throw new ArgumentNullException(nameof(recording));
            _simulation = new Simulation(recording.InitialLevel, new LevelPopulator(), recording.TimeLimitSeconds,
                new SimulationPipelineFactory().Create, recordInputs: false);
        }

        public bool Step()
        {
            if (IsFinished)
            {
                return false;
            }

            try
            {
                SimulationInput input = _recording.Inputs[_nextInput];
                switch (input.Kind)
                {
                    case SimulationInputKind.Move:
                        Simulation.Send(input.Command);
                        break;
                    case SimulationInputKind.Undo:
                        Simulation.RequestUndo();
                        break;
                    case SimulationInputKind.Tick:
                        Simulation.Tick(input.DeltaTime);
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported simulation input: {input.Kind}.");
                }
                _nextInput++;
                return true;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Run()
        {
            while (Step())
            {
                
            }
        }

        public void Dispose()
        {
            Simulation simulation = _simulation;
            _simulation = null;
            simulation?.Dispose();
        }
    }
}
