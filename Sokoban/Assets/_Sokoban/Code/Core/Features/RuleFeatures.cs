using Sokoban.Core.Features;

namespace Sokoban.Core
{
    public static class RuleFeatures
    {
        public static IFeature[] Create() => new IFeature[]
        {
            new UndoFeature(), // 1
            new MovementFeature(), // 2
            new MoveCountFeature(), // 3
            new GoalTrackingFeature(), // 4
            new WinFeature(), // 5
            new LevelTimerFeature(), // 6
        };
    }
}
