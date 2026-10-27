using Sokoban.Core.Components;

namespace Sokoban.Presentation.Ecs.Components
{
    public readonly struct ObservedPosition
    {
        public GridPosition Value { get; }

        public ObservedPosition(GridPosition value) =>
            Value = value;
    }
}
