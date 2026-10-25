using DefaultEcs;
using Sokoban.Core;
using DefaultEcs.System;
using Sokoban.Presentation.Ecs.Systems;

namespace Sokoban.Presentation
{
    internal sealed class ActorMotionFeature : IFeature
    {
        private readonly float _duration;
        
        public ActorMotionFeature(float duration) => 
            _duration = duration;
        
        public ISystem<float> Create(World world) => 
            new ActorMotionSystem(world, _duration);
    }
}
