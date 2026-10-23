using DefaultEcs;
using DefaultEcs.System;
using Sokoban.Core;
using Sokoban.Presentation.Ecs.Systems;

namespace Sokoban.Presentation
{
    internal sealed class SkinSwitchFeature : IFeature
    {
        public ISystem<float> Create(World world) => new SkinSwitchSystem(world);
    }
}
