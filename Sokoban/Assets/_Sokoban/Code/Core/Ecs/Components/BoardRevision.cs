namespace Sokoban.Core.Components
{
    public readonly struct BoardRevision
    {
        public long Value { get; }

        public BoardRevision(long value) =>
            Value = value;
    }
}
