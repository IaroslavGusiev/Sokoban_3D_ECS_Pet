using DefaultEcs;
using Sokoban.Core;
using DefaultEcs.System;
using Sokoban.Presentation.Ecs.Systems;

namespace Sokoban.Presentation
{
    internal sealed class ViewBindingFeature : IFeature
    {
        private readonly ViewContext _context;

        public ViewBindingFeature(ViewContext context) => 
            _context = context;

        public ISystem<float> Create(World world) => 
            new ViewBindingSystem(world, _context);
    }
}
