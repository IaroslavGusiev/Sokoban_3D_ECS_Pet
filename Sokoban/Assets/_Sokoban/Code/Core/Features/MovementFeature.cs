using DefaultEcs;
using DefaultEcs.System;
using Sokoban.Core.Systems;

namespace Sokoban.Core.Features
{
    internal sealed class MovementFeature : IFeature
    {
        public ISystem<float> Create(World world)
        {
            var validation = new MovementValidationSystem(world);
            
            try
            {
                return new SequentialSystem<float>(validation, new ApplyMovementSystem(world));
            }
            catch
            {
                validation.Dispose();
                throw;
            }
        }
    }
}
