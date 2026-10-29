using System;
using UnityEngine.UIElements;

namespace Sokoban.Presentation
{
    internal sealed class ButtonBinding : IDisposable
    {
        private readonly Button _button;
        private readonly Func<bool> _canExecute;
        private readonly Action _execute;

        public ButtonBinding(Button button, Func<bool> canExecute, Action execute)
        {
            _button = button;
            _canExecute = canExecute;
            _execute = execute;
            _button.clicked += Activate;
        }

        public void Refresh()
        {
            bool enabled = _canExecute();
            if (_button.enabledSelf != enabled)
            {
                _button.SetEnabled(enabled);
            }
        }

        public void Dispose() =>
            _button.clicked -= Activate;

        private void Activate()
        {
            if (_canExecute())
            {
                _execute();
            }
        }
    }
}
