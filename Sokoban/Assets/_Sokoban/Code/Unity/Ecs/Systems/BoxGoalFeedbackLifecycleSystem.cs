using DefaultEcs;
using DefaultEcs.System;
using Sokoban.Core.Components;
using Sokoban.Presentation.Ecs.Components;

namespace Sokoban.Presentation.Ecs.Systems
{
    public sealed class BoxGoalFeedbackLifecycleSystem : AEntitySetSystem<float>
    {
        public BoxGoalFeedbackLifecycleSystem(World world)
            : base(world.GetEntities()
                .With<BoxTag>()
                .With<GridPosition>()
                .With<BoxGoalFeedbackComponent>()
                .With<BoxGoalFeedbackState>()
                .AsSet(), useBuffer: true) { }

        protected override void Update(float deltaTime, in Entity entity)
        {
            BoxGoalAnimator animator = entity.Get<BoxGoalFeedbackComponent>().Animator;
            if (animator == null)
            {
                entity.Remove<BoxGoalFeedbackState>();
                return;
            }

            if (entity.Has<ViewMove>() || entity.Get<GridPosition>() != entity.Get<BoxGoalFeedbackState>().Goal)
            {
                animator.StopAndRestore();
                entity.Remove<BoxGoalFeedbackState>();
                return;
            }

            animator.Tick(deltaTime);
            
            if (animator.IsPlaying == false)
            {
                entity.Remove<BoxGoalFeedbackState>();
            }
        }

        public override void Dispose()
        {
            try
            {
                foreach (Entity entity in Set.GetEntities().ToArray())
                {
                    BoxGoalAnimator animator = entity.Get<BoxGoalFeedbackComponent>().Animator;
                    if (animator != null)
                    {
                        animator.StopAndRestore();
                    }
                    entity.Remove<BoxGoalFeedbackState>();
                }
            }
            finally
            {
                base.Dispose();
            }
        }
    }
}
