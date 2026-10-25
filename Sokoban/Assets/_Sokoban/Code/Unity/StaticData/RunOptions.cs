namespace Sokoban.Presentation
{
    public readonly struct RunOptions
    {
        public SkinId Skin { get; }
        public float TimeLimitSeconds { get; }
        public float MoveDurationSeconds { get; }

        public RunOptions(SkinId skin, float timeLimitSeconds, float moveDurationSeconds)
        {
            Skin = skin;
            TimeLimitSeconds = timeLimitSeconds;
            MoveDurationSeconds = moveDurationSeconds;
        }
    }
}
