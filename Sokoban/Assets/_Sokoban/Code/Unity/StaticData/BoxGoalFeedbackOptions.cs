namespace Sokoban.Presentation
{
    public readonly struct BoxGoalFeedbackOptions
    {
        public float Height { get; }
        public float ScaleAmount { get; }
        public float DurationSeconds { get; }

        public BoxGoalFeedbackOptions(float durationSeconds, float height, float scaleAmount)
        {
            Height = height;
            ScaleAmount = scaleAmount;
            DurationSeconds = durationSeconds;
        }
    }
}
