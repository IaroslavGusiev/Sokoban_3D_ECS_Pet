using Sokoban.Core.Commands;
using UnityEngine.InputSystem;

namespace Sokoban.Presentation
{
    public sealed class KeyboardMoveInput : IPlayerInput
    {
        public bool NextSkinPressed => Keyboard.current.kKey.wasPressedThisFrame;
        public bool UndoPressed => Keyboard.current.zKey.wasPressedThisFrame;

        public bool NextLevelPressed
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Keyboard keyboard = Keyboard.current;
                return keyboard.nKey.wasPressedThisFrame;
#else
                return false;
#endif
            }
        }

        public bool RestartPressed
        {
            get
            {
                Keyboard keyboard = Keyboard.current;
                return keyboard.rKey.wasPressedThisFrame;
            }
        }

        public MoveCommand ReadMove() => Keyboard.current switch
        {
            var keyboard when keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame => MoveCommand.Up,
            var keyboard when keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame => MoveCommand.Down,
            var keyboard when keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame => MoveCommand.Left,
            var keyboard when keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame => MoveCommand.Right,
            _ => default
        };
    }
}
