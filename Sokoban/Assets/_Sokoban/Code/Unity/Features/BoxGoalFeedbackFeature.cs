using DefaultEcs;
using Sokoban.Core;
using DefaultEcs.System;
using Sokoban.Presentation.Ecs.Systems;

namespace Sokoban.Presentation
{
    internal sealed class BoxGoalFeedbackFeature : IFeature
    {
        private readonly BoxGoalFeedbackOptions _options;

        public BoxGoalFeedbackFeature(BoxGoalFeedbackOptions options) => 
            _options = options;

        public ISystem<float> Create(World world)
        {
            var lifecycle = new BoxGoalFeedbackLifecycleSystem(world);
            try
            {
                return new SequentialSystem<float>(lifecycle, new BoxGoalFeedbackSystem(world, _options));
            }
            catch
            {
                lifecycle.Dispose();
                throw;
            }
        }
    }
}
