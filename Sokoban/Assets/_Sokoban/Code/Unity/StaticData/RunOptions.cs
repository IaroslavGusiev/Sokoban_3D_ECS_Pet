namespace Sokoban.Presentation
{
    public readonly struct RunOptions
    {
        public SkinId Skin { get; }
        public float TimeLimitSeconds { get; }
        public float MoveDurationSeconds { get; }
        public BoxGoalFeedbackOptions GoalFeedback { get; }

        public RunOptions(SkinId skin, float timeLimitSeconds, float moveDurationSeconds, BoxGoalFeedbackOptions goalFeedback = default)
        {
            Skin = skin;
            TimeLimitSeconds = timeLimitSeconds;
            MoveDurationSeconds = moveDurationSeconds;
            GoalFeedback = goalFeedback;
        }
    }
}
