using Sokoban.Core.Commands;

namespace Sokoban.Presentation
{
    public interface IPlayerInput
    {
        MoveCommand ReadMove();
        bool RestartPressed { get; }
        bool NextLevelPressed { get; }
        bool NextSkinPressed { get; }
    }
}
