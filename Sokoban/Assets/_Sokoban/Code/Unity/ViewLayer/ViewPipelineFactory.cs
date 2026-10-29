using DefaultEcs;
using Sokoban.Core;
using DefaultEcs.System;
using System.Collections.Generic;
using Sokoban.Presentation.Ecs.Systems;

namespace Sokoban.Presentation
{
    public sealed class ViewPipelineFactory
    {
        public SystemPipeline Create(World world, ViewContext context) =>
            new(CreateSystems(world, context));

        private static IEnumerable<ISystem<float>> CreateSystems(World world, ViewContext context)
        {
            yield return new SkinSwitchSystem(world);
            yield return new ViewBindingSystem(world, context);
            yield return new ActorMotionSystem(world, context.MoveDurationSeconds);
            yield return new GridTransformSyncSystem(world);
            yield return new PlayerAnimationSystem(world);
            yield return new BoxGoalFeedbackCompositeSystem(world);
        }
    }
}
