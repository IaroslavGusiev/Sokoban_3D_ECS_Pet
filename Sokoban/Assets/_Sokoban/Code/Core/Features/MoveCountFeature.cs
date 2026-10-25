using DefaultEcs;
using DefaultEcs.System;
using Sokoban.Core.Systems;

namespace Sokoban.Core.Features
{
    internal sealed class MoveCountFeature : IFeature
    {
        public ISystem<float> Create(World world) => 
            new MoveCountSystem(world);
    }
}
