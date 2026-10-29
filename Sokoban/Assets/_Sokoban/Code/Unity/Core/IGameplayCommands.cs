namespace Sokoban.Presentation
{
    public interface IGameplayCommands
    {
        bool CanRestart { get; }
        bool CanChooseCharacter { get; }
        bool CanConfirmCharacter { get; }
        bool CanUndo { get; }
        bool CanHint { get; }
        bool CanNextBackground { get; }

        void RequestRestart();
        void RequestCharacter(SkinId skin);
        void RequestConfirmCharacter();
        void RequestUndo();
        void RequestHint();
        void RequestNextBackground();
    }
}
