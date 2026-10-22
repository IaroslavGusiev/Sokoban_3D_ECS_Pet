using Sokoban.Core.Commands;
using UnityEngine.InputSystem;

namespace Sokoban.Unity
{
    public sealed class KeyboardMoveInput : IPlayerInput
    {
        public bool RestartPressed
        {
            get
            {
                Keyboard keyboard = Keyboard.current;
                return keyboard != null && keyboard.rKey.wasPressedThisFrame;
            }
        }

        public MoveCommand ReadMove() => Keyboard.current switch
        {
            null => default,
            var keyboard when keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame => MoveCommand.Up,
            var keyboard when keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame => MoveCommand.Down,
            var keyboard when keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame => MoveCommand.Left,
            var keyboard when keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame => MoveCommand.Right,
            _ => default
        };
    }
}
