namespace Sokoban.Presentation
{
    public interface IGameplayCommands
    {
        bool CanRestart { get; }
        bool CanSwitchSkin { get; }
        bool CanUndo { get; }
        bool CanHint { get; }

        void RequestRestart();
        void RequestNextSkin();
        void RequestUndo();
        void RequestHint();
    }
}
