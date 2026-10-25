namespace Sokoban.Core.Components
{
    public readonly struct MoveCount
    {
        public int Value { get; }

        public MoveCount(int value) =>
            Value = value;
    }
}
