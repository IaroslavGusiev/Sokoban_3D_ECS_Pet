namespace Sokoban.Presentation
{
    public interface IGameplayCommands
    {
        bool CanRestart { get; }
        bool CanNextLevel { get; }
        bool CanWatchReplay { get; }
        bool CanToggleReplayPause { get; }
        bool CanBackFromReplay { get; }
        bool CanChooseCharacter { get; }
        bool CanConfirmCharacter { get; }
        bool CanUndo { get; }
        bool CanHint { get; }
        bool CanNextBackground { get; }

        void RequestRestart();
        void RequestNextLevel();
        void RequestWatchReplay();
        void RequestToggleReplayPause();
        void RequestBackFromReplay();
        void RequestCharacter(SkinId skin);
        void RequestConfirmCharacter();
        void RequestUndo();
        void RequestHint();
        void RequestNextBackground();
    }
}
