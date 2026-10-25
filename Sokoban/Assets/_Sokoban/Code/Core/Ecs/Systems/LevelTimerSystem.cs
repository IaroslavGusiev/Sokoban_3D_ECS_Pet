using System;
using System.Linq;
using DefaultEcs;
using DefaultEcs.System;
using Sokoban.Core.Components;

namespace Sokoban.Core.Systems
{
    internal sealed class LevelTimerSystem : AEntitySetSystem<float>
    {
        private readonly Entity _player;

        public LevelTimerSystem(World world)
            : base(world.GetEntities()
                .With<LevelStateTag>()
                .With<LevelTimer>()
                .Without<LevelCompletedTag>()
                .Without<LevelTimedOutTag>()
                .AsSet(), useBuffer: true)
        {
            _player = world.Single(entity => entity.Has<PlayerTag>());
        }

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
                _player.Remove<MoveRequest>();
                _player.Remove<MovementPlan>();
                
                entity.Set<LevelTimedOutTag>();
            }
        }
    }
}
