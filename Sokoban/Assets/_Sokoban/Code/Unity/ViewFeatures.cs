using Sokoban.Core;

namespace Sokoban.Unity
{
    public sealed class ViewFeatures
    {
        public IFeature[] Create() => new IFeature[]
        {
            new TransformSyncFeature(), // 1
        };
    }
}
