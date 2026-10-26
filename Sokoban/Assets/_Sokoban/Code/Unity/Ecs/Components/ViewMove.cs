using UnityEngine;

namespace Sokoban.Presentation.Ecs.Components
{
    public readonly struct ViewMove
    {
        public Vector3 From { get; }
        public Vector3 To { get; }
        public float Elapsed { get; }
        public float Duration { get; }
        public bool IsPushing { get; }

        public ViewMove(Vector3 from, Vector3 to, float duration, bool isPushing = false, float elapsed = 0f)
        {
            From = from;
            To = to;
            Duration = duration;
            IsPushing = isPushing;
            Elapsed = elapsed;
        }

        public ViewMove Advance(float deltaTime) =>
            new(From, To, Duration, IsPushing, Mathf.Min(Elapsed + deltaTime, Duration));

        public bool IsDone => Elapsed >= Duration;

        public Vector3 Position => Vector3.Lerp(From, To, Mathf.Clamp01(Elapsed / Duration));
    }
}
