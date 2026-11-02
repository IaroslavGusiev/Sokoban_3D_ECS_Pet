using DefaultEcs;
using DefaultEcs.System;
using Sokoban.Core.Components;
using Sokoban.Presentation.Ecs.Components;

namespace Sokoban.Presentation.Ecs.Systems
{
    public sealed class BoxGoalFeedbackLifecycleSystem : AEntitySetSystem<float>
    {
        private readonly EntitySet _goals;

        public BoxGoalFeedbackLifecycleSystem(World world)
            : base(world.GetEntities()
                .With<BoxTag>()
                .With<GridPosition>()
                .With<BoxGoalFeedbackComponent>()
                .AsSet(), useBuffer: true)
        {
            try
            {
                _goals = world.GetEntities()
                    .With<GoalTag>()
                    .With<GridPosition>()
                    .AsSet();
            }
            catch
            {
                base.Dispose();
                throw;
            }
        }

        protected override void Update(float deltaTime, in Entity entity)
        {
            BoxGoalAnimator animator = entity.Get<BoxGoalFeedbackComponent>().Animator;
            if (animator == null)
            {
                entity.Remove<BoxGoalFeedbackState>();
                return;
            }

            // Occupancy is projected from Core positions, independently of arrival messages
            // and tween lifetime. This also covers undo and a newly installed/replaced view.
            animator.SetOccupied(IsGoal(entity.Get<GridPosition>()));

            if (entity.Has<BoxGoalFeedbackState>() == false)
            {
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
                    // Replay.Step can dispose its World before presentation cleanup runs.
                    if (entity.World == null || entity.IsAlive == false)
                    {
                        continue;
                    }
                    BoxGoalAnimator animator = entity.Get<BoxGoalFeedbackComponent>().Animator;
                    if (animator != null)
                    {
                        animator.StopAndRestore();
                        animator.SetOccupied(false);
                    }
                    entity.Remove<BoxGoalFeedbackState>();
                }
            }
            finally
            {
                _goals.Dispose();
                base.Dispose();
            }
        }

        private bool IsGoal(GridPosition position)
        {
            foreach (Entity goal in _goals.GetEntities())
            {
                if (goal.Get<GridPosition>() == position)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
