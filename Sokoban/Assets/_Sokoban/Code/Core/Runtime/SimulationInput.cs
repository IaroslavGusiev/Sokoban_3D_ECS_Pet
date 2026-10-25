using Sokoban.Core.Commands;

namespace Sokoban.Core
{
    public readonly struct SimulationInput
    {
        public SimulationInputKind Kind { get; }
        public MoveCommand Command { get; }
        public float DeltaTime { get; }

        internal SimulationInput(SimulationInputKind kind, MoveCommand command = default, float deltaTime = 0f)
        {
            Kind = kind;
            Command = command;
            DeltaTime = deltaTime;
        }
    }
}
