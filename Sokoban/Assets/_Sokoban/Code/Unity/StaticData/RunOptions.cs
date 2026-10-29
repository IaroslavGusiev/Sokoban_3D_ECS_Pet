namespace Sokoban.Presentation
{
    public readonly struct RunOptions
    {
        public SkinId Skin { get; }
        public bool DeferPlayerView { get; }
        public float TimeLimitSeconds { get; }
        public float MoveDurationSeconds { get; }

        public RunOptions(SkinId skin, float timeLimitSeconds, float moveDurationSeconds, bool deferPlayerView = false)
        {
            Skin = skin;
            DeferPlayerView = deferPlayerView;
            TimeLimitSeconds = timeLimitSeconds;
            MoveDurationSeconds = moveDurationSeconds;
        }
    }
}
