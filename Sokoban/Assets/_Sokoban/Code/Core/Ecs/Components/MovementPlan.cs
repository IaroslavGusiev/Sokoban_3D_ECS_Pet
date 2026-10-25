using System;

namespace Sokoban.Core.Components
{
    public readonly struct MovementPlan
    {
        public int StepCount { get; }
        public MovementStep First { get; }
        public MovementStep Second { get; }

        public MovementPlan(MovementStep first)
        {
            StepCount = 1;
            First = first;
            Second = default;
        }

        public MovementPlan(MovementStep first, MovementStep second)
        {
            StepCount = 2;
            First = first;
            Second = second;
        }

        public MovementStep this[int index] => index switch
        {
            0 when StepCount > 0 => First,
            1 when StepCount == 2 => Second,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };
    }
}
