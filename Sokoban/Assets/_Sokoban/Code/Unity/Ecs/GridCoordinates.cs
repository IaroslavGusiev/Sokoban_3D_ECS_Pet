using UnityEngine;
using Sokoban.Core.Components;

namespace Sokoban.Presentation.Ecs
{
    public static class GridCoordinates
    {
        public static Vector3 ToWorld(GridPosition position) => 
            new(position.X, 0f, position.Y);
    }
}
