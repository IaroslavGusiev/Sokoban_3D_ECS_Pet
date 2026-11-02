using System;
using UnityEngine.UIElements;

namespace Sokoban.Presentation
{
    internal sealed class GameplayReplayView : IDisposable
    {
        private readonly IGameplayStatus _status;
        private readonly Label _message;
        private readonly Button _pause;
        private readonly ButtonBinding _pauseBinding;
        private readonly ButtonBinding _backBinding;
        private GameplayPhase? _shownPhase;

        public GameplayReplayView(VisualElement root, IGameplayStatus status, IGameplayCommands commands)
        {
            _status = status;
            var panel = UiElements.Require<VisualElement>(root, UiIds.Replay.Panel);
            _message = UiElements.Require<Label>(panel, UiIds.Replay.Message);
            _pause = UiElements.Require<Button>(panel, UiIds.Replay.Pause);
            var back = UiElements.Require<Button>(panel, UiIds.Replay.Back);
            _pauseBinding = new ButtonBinding(_pause, () => commands.CanToggleReplayPause, commands.RequestToggleReplayPause);
            _backBinding = new ButtonBinding(back, () => commands.CanBackFromReplay, commands.RequestBackFromReplay);
        }

        public void Refresh()
        {
            _pauseBinding.Refresh();
            _backBinding.Refresh();
            if (_shownPhase == _status.Phase)
            {
                return;
            }
            _shownPhase = _status.Phase;
            _message.text = _status.Phase switch
            {
                GameplayPhase.ReplayLoading => "Preparing replay…",
                GameplayPhase.Replaying => "Playing the completed attempt.",
                GameplayPhase.ReplayPaused => "Playback paused.",
                GameplayPhase.ReplayFinished => "Replay complete. Return when ready.",
                GameplayPhase.ReplayFailed => "Replay unavailable. Return to your results.",
                _ => ""
            };
            _pause.text = _status.Phase == GameplayPhase.ReplayPaused ? "Resume" : "Pause";
        }

        public void Dispose()
        {
            _backBinding.Dispose();
            _pauseBinding.Dispose();
        }
    }
}
