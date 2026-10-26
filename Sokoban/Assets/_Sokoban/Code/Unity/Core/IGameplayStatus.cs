using Sokoban.Core;
using Sokoban.Core.Commands;
using System.Collections.Generic;

namespace Sokoban.Presentation
{
    public interface IGameplayStatus
    {
        GameplayPhase Phase { get; }

        int Moves { get; }
        int LevelCount { get; }
        int LevelNumber { get; }
        float RemainingSeconds { get; }
        HintStatus HintStatus { get; }
        MoveCommand HintMove { get; }
        
        IReadOnlyList<LevelResult> Results { get; }
    }
}
