using Sokoban.Core;
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
        
        IReadOnlyList<LevelResult> Results { get; }
    }
}
