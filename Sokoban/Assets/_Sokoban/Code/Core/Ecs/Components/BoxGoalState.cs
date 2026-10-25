namespace Sokoban.Core.Components
{
    public readonly struct BoxGoalState
    {
        public GridPosition Position { get; }

        public BoxGoalState(GridPosition position) => 
            Position = position;
    }
}
