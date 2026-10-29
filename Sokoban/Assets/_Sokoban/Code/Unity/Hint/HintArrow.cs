using System;
using UnityEngine;
using Sokoban.Core.Commands;
using Sokoban.Core.Components;
using Sokoban.Presentation.Ecs;

namespace Sokoban.Presentation
{
    public sealed class HintArrow : MonoBehaviour
    {
        public void Show(GridPosition player, MoveCommand move)
        {
            Vector3 direction = move switch
            {
                MoveCommand.Up => Vector3.forward,
                MoveCommand.Down => Vector3.back,
                MoveCommand.Left => Vector3.left,
                MoveCommand.Right => Vector3.right,
                _ => throw new ArgumentOutOfRangeException(nameof(move))
            };
            
            transform.position = GridCoordinates.ToWorld(player) + Vector3.up * 1.8f + direction * 0.55f;
            transform.rotation = Quaternion.LookRotation(direction);
            
            gameObject.SetActive(true);
        }
    }
}
