using DefaultEcs;
using Sokoban.Core;
using DefaultEcs.System;
using Sokoban.Presentation.Ecs.Systems;

namespace Sokoban.Presentation
{
    internal sealed class TransformSyncFeature : IFeature
    {
        public ISystem<float> Create(World world) => 
            new GridTransformSyncSystem(world);
    }
}
