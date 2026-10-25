namespace Sokoban.Core.Components
{
    internal readonly struct CommittedMove
    {
        public MovementPlan Plan { get; }
        public int PreviousMoveCount { get; }

        public CommittedMove(MovementPlan plan, int previousMoveCount)
        {
            Plan = plan;
            PreviousMoveCount = previousMoveCount;
        }
    }
}
