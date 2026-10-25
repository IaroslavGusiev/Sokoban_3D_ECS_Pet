using DefaultEcs;
using DefaultEcs.System;
using Sokoban.Core.Components;

namespace Sokoban.Core.Systems
{
    internal sealed class ApplyMovementSystem : ISystem<float>
    {
        private readonly World _world;
        private readonly EntitySet _plans;

        public bool IsEnabled { get; set; } = true;

        public ApplyMovementSystem(World world)
        {
            _world = world;
            
            _plans = world
                .GetEntities()
                .With<MovementPlan>()
                .AsSet();
        }

        public void Update(float deltaTime)
        {
            foreach (Entity owner in _plans
                         .GetEntities()
                         .ToArray())
            {
                ApplyPlan(owner);
            }
        }

        public void Dispose() =>
            _plans.Dispose();

        private void ApplyPlan(Entity owner)
        {
            var plan = owner.Get<MovementPlan>();
            bool canApply = CanApplyPlan(plan);
            owner.Remove<MovementPlan>();

            if (canApply == false)
            {
                return;
            }
            
            for (int index = 0; index < plan.StepCount; index++)
            {
                MovementStep step = plan[index];
                step.Target.Set(step.Destination);
            }
        }

        private bool CanApplyPlan(MovementPlan plan) => plan.StepCount switch
        {
            1 => IsTargetUnchanged(plan.First),
            2 => plan.First.Target != plan.Second.Target
                && IsTargetUnchanged(plan.First)
                && IsTargetUnchanged(plan.Second),
            _ => false
        };

        private bool IsTargetUnchanged(MovementStep step) =>
            IsTargetAvailable(step.Target)
            && step.Target.Get<GridPosition>() == step.ExpectedSource;

        private bool IsTargetAvailable(Entity target) =>
            ReferenceEquals(target.World, _world)
            && target.IsAlive
            && target.IsEnabled()
            && target.Has<GridPosition>()
            && target.IsEnabled<GridPosition>();
    }
}
