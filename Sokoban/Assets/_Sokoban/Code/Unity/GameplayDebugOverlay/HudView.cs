using UnityEngine;
using UnityEngine.UIElements;

namespace Sokoban.Presentation
{
    internal sealed class HudView
    {
        private readonly Label _level;
        private readonly Label _moves;
        private readonly Label _remaining;
        private readonly IGameplayStatus _status;

        private int _shownLevel = -1;
        private int _shownMoves = -1;
        private int _shownLevelCount = -1;
        private int _shownTenths = int.MinValue;

        public HudView(VisualElement root, IGameplayStatus status)
        {
            _status = status;
            
            _level = UiElements.Require<Label>(root, UiIds.Hud.Level);
            _moves = UiElements.Require<Label>(root, UiIds.Hud.Moves);
            _remaining = UiElements.Require<Label>(root, UiIds.Hud.Remaining);
        }

        public void Refresh()
        {
            if (_shownLevel != _status.LevelNumber || _shownLevelCount != _status.LevelCount)
            {
                _shownLevel = _status.LevelNumber;
                _shownLevelCount = _status.LevelCount;
                
                _level.text = $"{_shownLevel:00} / {(_shownLevelCount > 0 ? _shownLevelCount.ToString("00") : "?")}";
            }

            int tenths = _status.LevelCount > 0 
                ? Mathf.RoundToInt(_status.RemainingSeconds * 10f) 
                : -1;
            
            if (_shownTenths != tenths)
            {
                _shownTenths = tenths;
                _remaining.text = tenths >= 0 ? $"{tenths / 10f:F1} s" : "--";
            }

            if (_shownMoves == _status.Moves)
            {
                return;
            }
            
            _shownMoves = _status.Moves;
            _moves.text = _shownMoves.ToString();
        }
    }
}
