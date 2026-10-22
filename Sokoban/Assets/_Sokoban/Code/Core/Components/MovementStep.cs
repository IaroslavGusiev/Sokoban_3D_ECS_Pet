using DefaultEcs;

namespace Sokoban.Core.Components
{
    public readonly struct MovementStep
    {
        public Entity Target { get; }
        public GridPosition Destination { get; }
        public GridPosition ExpectedSource { get; }

        public MovementStep(Entity target, GridPosition expectedSource, GridPosition destination)
        {
            Target = target;
            Destination = destination;
            ExpectedSource = expectedSource;
        }
    }
}
