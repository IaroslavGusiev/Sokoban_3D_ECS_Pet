using Sokoban.Core.Features;

namespace Sokoban.Core
{
    public static class RuleFeatures
    {
        public static IFeature[] Create() => new IFeature[]
        {
            new MovementFeature(), // 1
            new WinFeature(), // 2
        };
    }
}
