using Sokoban.Core.Commands;
using Sokoban.Core.Components;

namespace Sokoban.Core._Sokoban.Code.Core.Ecs
{
    internal static class MoveCommandExtensions
    {
        public static GridPosition ToStep(this MoveCommand command) => command switch
        {
            MoveCommand.Up => new GridPosition(0, 1),
            MoveCommand.Down => new GridPosition(0, -1),
            MoveCommand.Left => new GridPosition(-1, 0),
            MoveCommand.Right => new GridPosition(1, 0),
            _ => default
        };
    }
}