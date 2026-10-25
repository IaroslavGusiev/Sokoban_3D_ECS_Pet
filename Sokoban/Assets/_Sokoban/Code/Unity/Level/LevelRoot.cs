using UnityEngine;

namespace Sokoban.Presentation
{
    public sealed class LevelRoot
    {
        public Transform Transform { get; }

        public LevelRoot(Transform transform) => 
            Transform = transform;
    }
}
