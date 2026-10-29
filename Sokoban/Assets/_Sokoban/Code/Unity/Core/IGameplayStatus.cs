using Sokoban.Core;
using Sokoban.Core.Commands;
using System.Collections.Generic;

namespace Sokoban.Presentation
{
    public interface IGameplayStatus
    {
        GameplayPhase Phase { get; }
        IReadOnlyList<SkinId> Characters { get; }
        SkinId SelectedCharacter { get; }

        int Moves { get; }
        int LevelCount { get; }
        int LevelNumber { get; }
        float RemainingSeconds { get; }
        BackgroundStatus BackgroundStatus { get; }
        string BackgroundName { get; }
        HintStatus HintStatus { get; }
        MoveCommand HintMove { get; }
        
        IReadOnlyList<LevelResult> Results { get; }
    }
}
