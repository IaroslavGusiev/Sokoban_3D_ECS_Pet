using DefaultEcs;
using DefaultEcs.System;
using Sokoban.Core.Systems;

namespace Sokoban.Core.Features
{
    internal sealed class LevelTimerFeature : IFeature
    {
        public ISystem<float> Create(World world) => 
            new LevelTimerSystem(world);
    }
}
