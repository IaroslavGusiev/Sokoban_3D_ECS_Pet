namespace Sokoban.Presentation
{
    internal static class UiIds
    {
        internal static class Root
        {
            public const string Name = "overlayRoot";
        }

        internal static class Hud
        {
            public const string Panel = "statusPanel";
            public const string Level = "level";
            public const string Remaining = "remaining";
            public const string Moves = "moves";
            public const string Notice = "phase";
        }

        internal static class Controls
        {
            public const string Scroll = "controlsScroll";
            public const string Panel = "controlsPanel";
            public const string Restart = "restart";
            public const string RestartTitle = "restartTitle";
            public const string Undo = "undo";
            public const string Hint = "hint";
            public const string HintMessage = "hintMessage";
            public const string HintTitle = "hintTitle";
            public const string Debug = "debug";
            public const string PhaseDetail = "phaseDetail";
        }

        internal static class Results
        {
            public const string Panel = "results";
            public const string Completed = "completed";
            public const string Rows = "resultRows";
            public const string Template = "resultRowTemplate";
            public const string CompletedStats = "completedStats";
            public const string TotalMoves = "totalMoves";
            public const string TotalTime = "totalTime";
            public const string PlayAgain = "playAgain";
            public const string NextLevel = "nextLevel";
            public const string WatchReplay = "watchReplay";
            public const string WatchFinalReplay = "watchFinalReplay";
            public const string RowClass = "result-template";
        }

        internal static class Replay
        {
            public const string Panel = "replayPanel";
            public const string Message = "replayMessage";
            public const string Pause = "pauseReplay";
            public const string Back = "backFromReplay";
        }

        internal static class ResultRow
        {
            public const string Level = "rowLevel";
            public const string Moves = "rowMoves";
            public const string Time = "rowTime";
        }

        internal static class Selection
        {
            public const string Panel = "characterSelection";
            public const string Choices = "characterChoices";
            public const string Template = "characterChoiceTemplate";
            public const string Portraits = "characterPortraits";
            public const string Feedback = "characterFeedback";
            public const string Confirm = "confirmCharacter";
        }

        internal static class Choice
        {
            public const string Button = "characterChoice";
            public const string Portrait = "characterPortrait";
            public const string Name = "characterName";
            public const string SelectedClass = "selected";
        }

        internal static class Background
        {
            public const string Controls = "backgroundControls";
            public const string Label = "background";
            public const string Title = "backgroundTitle";
            public const string Next = "nextBackground";
        }

        internal static class Classes
        {
            public const string Hidden = "hidden";
            public const string Compact = "compact";
        }

        internal static class Phases
        {
            public const string Loading = "phase-loading";
            public const string BindingViews = "phase-binding-views";
            public const string Playing = "phase-playing";
            public const string Completed = "phase-completed";
            public const string ViewsFailed = "phase-views-failed";
            public const string LevelFailed = "phase-level-failed";
            public const string Finished = "phase-finished";
            public const string SelectingCharacter = "phase-selecting-character";
            public const string BindingCharacter = "phase-binding-character";
            public const string CharacterFailed = "phase-character-failed";
            public const string ReplayLoading = "phase-replay-loading";
            public const string Replaying = "phase-replaying";
            public const string ReplayPaused = "phase-replay-paused";
            public const string ReplayFinished = "phase-replay-finished";
            public const string ReplayFailed = "phase-replay-failed";
        }
    }
}
