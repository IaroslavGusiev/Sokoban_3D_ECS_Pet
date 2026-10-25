using Sokoban.Core;

namespace Sokoban.Presentation
{
    public sealed class ViewFeatures
    {
        public IFeature[] Create(ViewContext context) => new IFeature[]
        {
            new SkinSwitchFeature(), // 1
            new ViewBindingFeature(context), // 2
            new ActorMotionFeature(context.MoveDurationSeconds), // 3
            new TransformSyncFeature(), // 4
            new PlayerAnimationFeature(), // 5
            new BoxGoalFeedbackFeature(), // 6
        };
    }
}
