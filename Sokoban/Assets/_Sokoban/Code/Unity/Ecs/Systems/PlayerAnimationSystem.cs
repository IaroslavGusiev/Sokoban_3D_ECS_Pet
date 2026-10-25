using DefaultEcs;
using DefaultEcs.System;
using Sokoban.Core.Components;
using Sokoban.Presentation.Ecs.Components;

namespace Sokoban.Presentation.Ecs.Systems
{
    public sealed class PlayerAnimationSystem : AEntitySetSystem<float>
    {
        public PlayerAnimationSystem(World world) 
            : base(world
                .GetEntities()
                .With<PlayerTag>()
                .With<PlayerAnimatorComponent>()
                .AsSet(), useBuffer: true) { }

        protected override void Update(float deltaTime, in Entity entity)
        {
            PlayerAnimator animator = entity.Get<PlayerAnimatorComponent>().Animator;
            
            if (animator == null)
            {
                return;
            }
            
            bool moving = entity.Has<ViewMove>();
            
            animator.SetMoving(moving);
            animator.SetPushing(moving && entity.Get<ViewMove>().IsPushing, deltaTime);
        }
    }
}
