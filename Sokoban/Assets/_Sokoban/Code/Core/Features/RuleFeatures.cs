using Sokoban.Core.Features;

namespace Sokoban.Core
{
    public static class RuleFeatures
    {
        public static IFeature[] Create() => new IFeature[]
        {
            new MovementFeature(), // 1
            new MoveCountFeature(), // 2
            new GoalTrackingFeature(), // 3
            new WinFeature(), // 4
            new LevelTimerFeature(), // 5
        };
    }
}
