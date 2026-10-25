using UnityEngine;
using Sokoban.Core.Components;

namespace Sokoban.Presentation.Ecs.Components
{
    public struct ActorViewState
    {
        public GridPosition Position;
        public Quaternion Facing;
        public bool HasFacing;
    }
}
