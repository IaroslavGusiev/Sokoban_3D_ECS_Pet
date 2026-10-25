namespace Sokoban.Core.Components
{
    public readonly struct LevelTimer
    {
        public float RemainingSeconds { get; }

        public LevelTimer(float remainingSeconds) => 
            RemainingSeconds = remainingSeconds;
    }
}
