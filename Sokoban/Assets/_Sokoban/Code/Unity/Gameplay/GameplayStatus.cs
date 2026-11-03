using System;
using Sokoban.Core;
using Sokoban.Core.Commands;
using System.Collections.Generic;

namespace Sokoban.Presentation
{
    public sealed class GameplayStatus : IGameplayStatus
    {
        private GameplayStatusSnapshot _snapshot = new(GameplayPhase.Loading, 0, 1, 0, 0f, default, default, default, default, string.Empty, default);
        
        private IReadOnlyList<SkinId> _characters = Array.AsReadOnly(Array.Empty<SkinId>());
        
        private IReadOnlyList<LevelResult> _results = Array.AsReadOnly(Array.Empty<LevelResult>());

        public GameplayPhase Phase =>
            _snapshot.Phase;

        public int LevelCount =>
            _snapshot.LevelCount;

        public int LevelNumber =>
            _snapshot.LevelNumber;

        public int Moves =>
            _snapshot.Moves;

        public float RemainingSeconds =>
            _snapshot.RemainingSeconds;

        public SkinId SelectedCharacter =>
            _snapshot.SelectedCharacter;

        public HintStatus HintStatus =>
            _snapshot.HintStatus;

        public MoveCommand HintMove =>
            _snapshot.HintMove;

        public BackgroundStatus BackgroundStatus =>
            _snapshot.BackgroundStatus;

        public string BackgroundName =>
            _snapshot.BackgroundName;

        public IReadOnlyList<SkinId> Characters =>
            _characters;

        public IReadOnlyList<LevelResult> Results =>
            _results;

        internal GameplayAvailability Availability =>
            _snapshot.Availability;

        internal void Publish(GameplayStatusSnapshot snapshot, IReadOnlyList<SkinId> characters, IReadOnlyList<LevelResult> results)
        {
            IReadOnlyList<SkinId> nextCharacters = CopyWhenChanged(_characters, characters);
            IReadOnlyList<LevelResult> nextResults = CopyWhenChanged(_results, results);
            _characters = nextCharacters;
            _results = nextResults;
            _snapshot = snapshot;
        }

        private static IReadOnlyList<T> CopyWhenChanged<T>(IReadOnlyList<T> previous, IReadOnlyList<T> source)
        {
            bool changed = previous.Count != source.Count;
            for (var index = 0; changed == false && index < source.Count; index++)
            {
                changed = EqualityComparer<T>.Default.Equals(previous[index], source[index]) == false;
            }

            if (changed == false)
            {
                return previous;
            }

            var copy = new T[source.Count];
            
            for (var index = 0; index < source.Count; index++)
            {
                copy[index] = source[index];
            }

            return Array.AsReadOnly(copy);
        }
    }

    internal readonly struct GameplayStatusSnapshot
    {
        public GameplayPhase Phase { get; }
        public int LevelCount { get; }
        public int LevelNumber { get; }
        public int Moves { get; }
        public float RemainingSeconds { get; }
        public SkinId SelectedCharacter { get; }
        public HintStatus HintStatus { get; }
        public MoveCommand HintMove { get; }
        public BackgroundStatus BackgroundStatus { get; }
        public string BackgroundName { get; }
        public GameplayAvailability Availability { get; }

        public GameplayStatusSnapshot(
            GameplayPhase phase,
            int levelCount,
            int levelNumber,
            int moves,
            float remainingSeconds,
            SkinId selectedCharacter,
            HintStatus hintStatus,
            MoveCommand hintMove,
            BackgroundStatus backgroundStatus,
            string backgroundName,
            GameplayAvailability availability)
        {
            Phase = phase;
            LevelCount = levelCount;
            LevelNumber = levelNumber;
            Moves = moves;
            RemainingSeconds = remainingSeconds;
            SelectedCharacter = selectedCharacter;
            HintStatus = hintStatus;
            HintMove = hintMove;
            BackgroundStatus = backgroundStatus;
            BackgroundName = backgroundName;
            Availability = availability;
        }
    }

    internal readonly struct GameplayAvailability
    {
        public bool CanRestart { get; }
        public bool CanNextLevel { get; }
        public bool CanWatchReplay { get; }
        public bool CanToggleReplayPause { get; }
        public bool CanBackFromReplay { get; }
        public bool CanChooseCharacter { get; }
        public bool CanConfirmCharacter { get; }
        public bool CanUndo { get; }
        public bool CanHint { get; }
        public bool CanNextBackground { get; }

        public GameplayAvailability(
            bool canRestart,
            bool canNextLevel,
            bool canWatchReplay,
            bool canToggleReplayPause,
            bool canBackFromReplay,
            bool canChooseCharacter,
            bool canConfirmCharacter,
            bool canUndo,
            bool canHint,
            bool canNextBackground)
        {
            CanRestart = canRestart;
            CanNextLevel = canNextLevel;
            CanWatchReplay = canWatchReplay;
            CanToggleReplayPause = canToggleReplayPause;
            CanBackFromReplay = canBackFromReplay;
            CanChooseCharacter = canChooseCharacter;
            CanConfirmCharacter = canConfirmCharacter;
            CanUndo = canUndo;
            CanHint = canHint;
            CanNextBackground = canNextBackground;
        }
    }
}
