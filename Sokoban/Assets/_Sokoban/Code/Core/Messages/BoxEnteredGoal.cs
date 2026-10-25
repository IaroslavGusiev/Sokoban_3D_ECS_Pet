using DefaultEcs;
using Sokoban.Core.Components;

namespace Sokoban.Core.Messages
{
    public readonly struct BoxEnteredGoal
    {
        public Entity Box { get; }
        public GridPosition Goal { get; }

        public BoxEnteredGoal(Entity box, GridPosition goal)
        {
            Box = box;
            Goal = goal;
        }
    }
}
