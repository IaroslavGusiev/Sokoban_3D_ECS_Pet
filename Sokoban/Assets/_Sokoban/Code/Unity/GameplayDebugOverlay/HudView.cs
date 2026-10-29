using UnityEngine;
using UnityEngine.UIElements;

namespace Sokoban.Presentation
{
    internal sealed class HudView
    {
        private readonly IGameplayStatus _status;
        private readonly Label _level;
        private readonly Label _remaining;
        private readonly Label _moves;

        private int _shownLevel = -1;
        private int _shownLevelCount = -1;
        private int _shownTenths = int.MinValue;
        private int _shownMoves = -1;

        public HudView(VisualElement root, IGameplayStatus status)
        {
            _status = status;
            _level = UiElements.Require<Label>(root, UiIds.Hud.Level);
            _remaining = UiElements.Require<Label>(root, UiIds.Hud.Remaining);
            _moves = UiElements.Require<Label>(root, UiIds.Hud.Moves);
        }

        public void Refresh()
        {
            if (_shownLevel != _status.LevelNumber || _shownLevelCount != _status.LevelCount)
            {
                _shownLevel = _status.LevelNumber;
                _shownLevelCount = _status.LevelCount;
                _level.text = $"{_shownLevel:00} / {(_shownLevelCount > 0 ? _shownLevelCount.ToString("00") : "?")}";
            }

            int tenths = _status.LevelCount > 0 ? Mathf.RoundToInt(_status.RemainingSeconds * 10f) : -1;
            if (_shownTenths != tenths)
            {
                _shownTenths = tenths;
                _remaining.text = tenths >= 0 ? $"{tenths / 10f:F1} s" : "--";
            }

            if (_shownMoves != _status.Moves)
            {
                _shownMoves = _status.Moves;
                _moves.text = _shownMoves.ToString();
            }
        }
    }
}
