using System;
using UnityEngine.UIElements;

namespace Sokoban.Presentation
{
    internal sealed class ButtonBinding : IDisposable
    {
        private readonly Button _button;
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public ButtonBinding(Button button, Func<bool> canExecute, Action execute)
        {
            _button = button;
            _execute = execute;
            _canExecute = canExecute;

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
