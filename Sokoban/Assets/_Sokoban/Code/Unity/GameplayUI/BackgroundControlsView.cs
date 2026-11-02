using System;
using UnityEngine.UIElements;

namespace Sokoban.Presentation
{
    internal sealed class BackgroundControlsView : IDisposable
    {
        private readonly Label _title;
        private readonly Label _background;
        private readonly ButtonBinding _next;
        private readonly IGameplayStatus _status;

        private BackgroundStatus? _shownStatus;
        private string _shownName;

        public BackgroundControlsView(VisualElement root, IGameplayStatus status, IGameplayCommands commands)
        {
            _status = status;
            
            var group = UiElements.Require<VisualElement>(root, UiIds.Background.Controls);
            
            _background = UiElements.Require<Label>(group, UiIds.Background.Label);
            
            _title = UiElements.Require<Label>(group, UiIds.Background.Title);
            
            var next = UiElements.Require<Button>(group, UiIds.Background.Next);
            _next = new ButtonBinding(next, () => commands.CanNextBackground, commands.RequestNextBackground);
        }

        public void Refresh()
        {
            _next.Refresh();
            
            if (_shownName == _status.BackgroundName && _shownStatus == _status.BackgroundStatus)
            {
                return;
            }

            _shownName = _status.BackgroundName;
            _shownStatus = _status.BackgroundStatus;
            _background.text = $"{_shownName}\n{_shownStatus}";
            
            _title.text = _status.BackgroundStatus == BackgroundStatus.Failed 
                ? "Retry background" 
                : "Next background";
        }

        public void Dispose() =>
            _next.Dispose();
    }
}
