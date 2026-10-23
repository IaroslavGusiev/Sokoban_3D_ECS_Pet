using Sokoban.Core;

namespace Sokoban.Presentation
{
    public sealed class ViewFeatures
    {
        public IFeature[] Create(ViewContext context) => new IFeature[]
        {
            new SkinSwitchFeature(), // 1
            new ViewBindingFeature(context), // 2
            new TransformSyncFeature(), // 3
        };
    }
}
