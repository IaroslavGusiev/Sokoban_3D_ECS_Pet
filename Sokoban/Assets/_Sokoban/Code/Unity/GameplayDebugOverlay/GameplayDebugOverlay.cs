using Zenject;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sokoban.Presentation
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class GameplayDebugOverlay : MonoBehaviour
    {
        private IGameplayStatus _status;
        private IGameplayCommands _commands;

        private UIDocument _document;
        private GameplayOverlayView _view;

        [Inject]
        public void Construct(IGameplayStatus status, IGameplayCommands commands)
        {
            _view?.Dispose();
            _view = null;
            _status = status;
            _commands = commands;

            if (isActiveAndEnabled)
            {
                SynchronizeView();
            }
        }

        private void OnEnable() =>
            SynchronizeView();

        private void LateUpdate() =>
            SynchronizeView();

        private void OnDisable()
        {
            _view?.Dispose();
            _view = null;
        }

        private void SynchronizeView()
        {
            if (_status == null || _commands == null)
            {
                return;
            }

            if (_document == null)
            {
                _document = GetComponent<UIDocument>();
            }
            
            VisualElement root = _document != null && _document.isActiveAndEnabled
                ? _document.rootVisualElement
                : null;

            if (_view != null && _view.Root != root)
            {
                _view.Dispose();
                _view = null;
            }

            if (root == null || root.Q(UiIds.Root.Name) == null)
            {
                return;
            }

            _view ??= new GameplayOverlayView(root, _status, _commands);
            _view.Refresh();
        }
    }
}