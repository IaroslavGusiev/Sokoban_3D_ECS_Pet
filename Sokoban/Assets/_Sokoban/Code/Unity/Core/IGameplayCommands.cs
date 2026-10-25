namespace Sokoban.Presentation
{
    public interface IGameplayCommands
    {
        bool CanRestart { get; }
        bool CanSwitchSkin { get; }

        void RequestRestart();
        void RequestNextSkin();
    }
}
