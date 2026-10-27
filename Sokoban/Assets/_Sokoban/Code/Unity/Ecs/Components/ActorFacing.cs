using UnityEngine;

namespace Sokoban.Presentation.Ecs.Components
{
    public readonly struct ActorFacing
    {
        public Quaternion Value { get; }

        public ActorFacing(Quaternion value) =>
            Value = value;
    }
}
