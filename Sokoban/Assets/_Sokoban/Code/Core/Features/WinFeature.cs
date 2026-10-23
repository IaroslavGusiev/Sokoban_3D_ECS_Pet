using DefaultEcs;
using DefaultEcs.System;
using Sokoban.Core.Systems;

namespace Sokoban.Core.Features
{
    internal sealed class WinFeature : IFeature
    {
        public ISystem<float> Create(World world) => new WinCheckSystem(world);
    }
}
