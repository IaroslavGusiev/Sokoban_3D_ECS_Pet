using DefaultEcs;
using Sokoban.Core;
using DefaultEcs.System;
using Sokoban.Unity.Ecs.Systems;

namespace Sokoban.Unity
{
    internal sealed class TransformSyncFeature : IFeature
    {
        public ISystem<float> Create(World world) => 
            new GridTransformSyncSystem(world);
    }
}
