using DefaultEcs;
using Sokoban.Core;
using DefaultEcs.System;
using Sokoban.Presentation.Ecs.Systems;

namespace Sokoban.Presentation
{
    internal sealed class PlayerAnimationFeature : IFeature
    {
        public ISystem<float> Create(World world) => 
            new PlayerAnimationSystem(world);
    }
}
