using Sokoban.Core.Components;

namespace Sokoban.Presentation.Ecs.Components
{
    public readonly struct BoxGoalFeedbackState
    {
        public GridPosition Goal { get; }

        public BoxGoalFeedbackState(GridPosition goal) => 
            Goal = goal;
    }
}
