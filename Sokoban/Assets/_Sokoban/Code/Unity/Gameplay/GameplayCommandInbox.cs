using System;

namespace Sokoban.Presentation
{
    public sealed class GameplayCommandInbox : IGameplayCommands
    {
        private readonly GameplayStatus _status;

        private GameplayRequests _pending;
        private SkinId? _character;
        private bool _accepting;
        private bool _closed;

        public bool CanRestart =>
            _closed == false && _accepting && _status.Availability.CanRestart;

        public bool CanNextLevel =>
            _closed == false && _accepting && _status.Availability.CanNextLevel;

        public bool CanWatchReplay =>
            _closed == false && _accepting && _status.Availability.CanWatchReplay;

        public bool CanToggleReplayPause =>
            _closed == false && _accepting && _status.Availability.CanToggleReplayPause;

        public bool CanBackFromReplay =>
            _closed == false && _accepting && _status.Availability.CanBackFromReplay;

        public bool CanConfirmCharacter =>
            _closed == false && _accepting && _status.Availability.CanConfirmCharacter;

        public bool CanUndo =>
            _closed == false && _accepting && _status.Availability.CanUndo;

        public bool CanHint =>
            _closed == false && _accepting && _status.Availability.CanHint && (_pending & GameplayRequests.Hint) == 0;

        public bool CanNextBackground =>
            _closed == false && _accepting && _status.Availability.CanNextBackground;

        public bool CanChooseCharacter =>
            _closed == false && _accepting && _status.Availability.CanChooseCharacter;

        public GameplayCommandInbox(GameplayStatus status) =>
            _status = status;

        public void RequestRestart()
        {
            if (CanRestart)
            {
                _pending |= GameplayRequests.Restart;
            }
        }

        public void RequestNextLevel()
        {
            if (CanNextLevel)
            {
                _pending |= GameplayRequests.NextLevel;
            }
        }

        public void RequestWatchReplay()
        {
            if (CanWatchReplay)
            {
                _pending |= GameplayRequests.WatchReplay;
            }
        }

        public void RequestToggleReplayPause()
        {
            if (CanToggleReplayPause)
            {
                _pending |= GameplayRequests.ToggleReplayPause;
            }
        }

        public void RequestBackFromReplay()
        {
            if (CanBackFromReplay)
            {
                _pending |= GameplayRequests.BackFromReplay;
            }
        }

        public void RequestConfirmCharacter()
        {
            if (CanConfirmCharacter)
            {
                _pending |= GameplayRequests.ConfirmCharacter;
            }
        }

        public void RequestUndo()
        {
            if (CanUndo)
            {
                _pending |= GameplayRequests.Undo;
            }
        }

        public void RequestHint()
        {
            if (CanHint)
            {
                _pending |= GameplayRequests.Hint;
            }
        }

        public void RequestNextBackground()
        {
            if (CanNextBackground)
            {
                _pending |= GameplayRequests.NextBackground;
            }
        }

        public void RequestCharacter(SkinId skin)
        {
            if (CanChooseCharacter == false)
            {
                return;
            }

            foreach (SkinId choice in _status.Characters)
            {
                if (choice == skin)
                {
                    _character = skin;
                    return;
                }
            }
        }

        internal GameplayRequests Consume(out SkinId? character)
        {
            GameplayRequests requests = _pending;
            character = _character;
            Clear();
            return requests;
        }

        internal void Clear()
        {
            _pending = GameplayRequests.None;
            _character = null;
        }

        internal void Close()
        {
            Clear();
            _closed = true;
        }

        internal void BeginScope()
        {
            Clear();
            // Old published values cannot admit requests while their run/replay is being replaced.
            _accepting = false;
        }

        internal void AcceptPublication(GameplayAvailability availability)
        {
            _accepting = true;
            if (availability.CanRestart == false)
            {
                _pending &= ~GameplayRequests.Restart;
            }

            if (availability.CanNextLevel == false)
            {
                _pending &= ~GameplayRequests.NextLevel;
            }

            if (availability.CanWatchReplay == false)
            {
                _pending &= ~GameplayRequests.WatchReplay;
            }

            if (availability.CanToggleReplayPause == false)
            {
                _pending &= ~GameplayRequests.ToggleReplayPause;
            }

            if (availability.CanBackFromReplay == false)
            {
                _pending &= ~GameplayRequests.BackFromReplay;
            }

            if (availability.CanConfirmCharacter == false)
            {
                _pending &= ~GameplayRequests.ConfirmCharacter;
            }

            if (availability.CanUndo == false)
            {
                _pending &= ~GameplayRequests.Undo;
            }

            if (availability.CanHint == false)
            {
                _pending &= ~GameplayRequests.Hint;
            }

            if (availability.CanNextBackground == false)
            {
                _pending &= ~GameplayRequests.NextBackground;
            }

            if (availability.CanChooseCharacter == false)
            {
                _character = null;
            }
        }
    }

    [Flags]
    internal enum GameplayRequests
    {
        None = 0,
        Restart = 1 << 0,
        NextLevel = 1 << 1,
        WatchReplay = 1 << 2,
        ToggleReplayPause = 1 << 3,
        BackFromReplay = 1 << 4,
        ConfirmCharacter = 1 << 5,
        Undo = 1 << 6,
        Hint = 1 << 7,
        NextBackground = 1 << 8
    }
}
