using Sokoban.Core.Commands;

namespace Sokoban.Core.Components
{
    public readonly struct MoveRequest
    {
        public MoveCommand Command { get; }

        public MoveRequest(MoveCommand command) => 
            Command = command;
    }
}
