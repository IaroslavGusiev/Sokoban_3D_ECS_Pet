using UnityEngine;

namespace Sokoban.Unity
{
    public sealed class LevelRoot
    {
        public Transform Transform { get; }

        public LevelRoot(Transform transform)
        {
            Transform = transform;
        }
    }
}
