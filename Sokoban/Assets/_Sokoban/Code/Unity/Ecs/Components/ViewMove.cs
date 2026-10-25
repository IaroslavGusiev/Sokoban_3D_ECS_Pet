using UnityEngine;

namespace Sokoban.Presentation.Ecs.Components
{
    public struct ViewMove
    {
        public Vector3 From;
        public Vector3 To;
        public float Elapsed;
        public float Duration;
        public bool IsPushing;

        public Vector3 Position => Vector3.Lerp(From, To, Mathf.Clamp01(Elapsed / Duration));
    }
}
