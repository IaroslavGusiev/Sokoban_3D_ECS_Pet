using Sokoban.Core.Commands;

namespace Sokoban.Unity
{
    public interface IPlayerInput
    {
        MoveCommand ReadMove();
        bool RestartPressed { get; }
    }
}
