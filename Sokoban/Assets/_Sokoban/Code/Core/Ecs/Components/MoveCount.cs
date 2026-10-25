namespace Sokoban.Core.Components
{
    public readonly struct MoveCount
    {
        public int Value { get; }
        public GridPosition LastPosition { get; }

        public MoveCount(int value, GridPosition lastPosition)
        {
            Value = value;
            LastPosition = lastPosition;
        }
    }
}
