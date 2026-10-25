using DefaultEcs;
using Sokoban.Core;
using DefaultEcs.System;
using Sokoban.Presentation.Ecs.Systems;

namespace Sokoban.Presentation
{
    internal sealed class BoxGoalFeedbackFeature : IFeature
    {
        public ISystem<float> Create(World world)
        {
            var lifecycle = new BoxGoalFeedbackLifecycleSystem(world);
            try
            {
                return new SequentialSystem<float>(lifecycle, new BoxGoalFeedbackSystem(world));
            }
            catch
            {
                lifecycle.Dispose();
                throw;
            }
        }
    }
}
