using System;
using DefaultEcs;
using DefaultEcs.System;
using Sokoban.Core.Components;

namespace Sokoban.Core.Systems
{
    internal sealed class LevelTimerSystem : AEntitySetSystem<float>
    {
        public LevelTimerSystem(World world)
            : base(world.GetEntities()
                .With<LevelTimer>()
                .Without<LevelCompletedTag>()
                .Without<LevelTimedOutTag>()
                .AsSet(), useBuffer: true) { }

        protected override void Update(float deltaTime, in Entity entity)
        {
            if (deltaTime == 0f)
            {
                return;
            }

            float remainingSeconds = entity.Get<LevelTimer>().RemainingSeconds;
            float remaining = Math.Max(0f, remainingSeconds - deltaTime);
            
            entity.Set(new LevelTimer(remaining));
            
            if (remaining == 0f)
            {
                entity.Remove<MoveRequest>();
                entity.Remove<MovementPlan>();
                
                entity.Set<LevelTimedOutTag>();
            }
        }
    }
}
