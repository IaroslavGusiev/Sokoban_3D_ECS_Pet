using DefaultEcs;
using DefaultEcs.System;
using Sokoban.Core.Components;
using Sokoban.Core.Messages;

namespace Sokoban.Core.Systems
{
    internal sealed class GoalTrackingSystem : AEntitySetSystem<float>
    {
        private readonly World _world;
        private readonly EntitySet _goals;

        public GoalTrackingSystem(World world)
            : base(world.GetEntities()
                .With<BoxTag>()
                .With<GridPosition>()
                .With<BoxGoalState>()
                .AsSet(), useBuffer: true)
        {
            _world = world;
            
            _goals = world.GetEntities()
                .With<GoalTag>()
                .With<GridPosition>()
                .AsSet();
        }

        protected override void Update(float deltaTime, in Entity entity)
        {
            var position = entity.Get<GridPosition>();
            
            if (position == entity.Get<BoxGoalState>().Position)
            {
                return;
            }

            entity.Set(new BoxGoalState(position));
            
            foreach (Entity goal in _goals.GetEntities())
            {
                if (goal.Get<GridPosition>() != position)
                {
                    continue;
                }
                
                _world.Publish(new BoxEnteredGoal(entity, position));
                break;
            }
        }

        public override void Dispose()
        {
            _goals.Dispose();
            base.Dispose();
        }
    }
}
