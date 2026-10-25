namespace Sokoban.Core
{
    public readonly struct LevelResult
    {
        public int Moves { get; }
        public float ElapsedSeconds { get; }
        public float RemainingSeconds { get; }

        public LevelResult(int moves, float elapsedSeconds, float remainingSeconds)
        {
            Moves = moves;
            ElapsedSeconds = elapsedSeconds;
            RemainingSeconds = remainingSeconds;
        }
    }
}
