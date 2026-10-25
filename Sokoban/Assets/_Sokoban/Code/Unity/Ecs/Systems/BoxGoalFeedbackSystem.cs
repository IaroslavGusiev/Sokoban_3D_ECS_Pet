using System;
using DefaultEcs;
using DefaultEcs.System;
using Sokoban.Core.Messages;
using Sokoban.Core.Components;
using System.Collections.Generic;
using Sokoban.Presentation.Ecs.Components;

namespace Sokoban.Presentation.Ecs.Systems
{
    public sealed class BoxGoalFeedbackSystem : ISystem<float>
    {
        private readonly World _world;

        private readonly List<Entity> _consumed = new();
        private readonly List<BoxEnteredGoal> _messages = new();
        private readonly Dictionary<Entity, GridPosition> _pending = new();

        private readonly IDisposable _subscription;
        
        public bool IsEnabled { get; set; } = true;

        public BoxGoalFeedbackSystem(World world)
        {
            _world = world;
            
            _subscription = world.Subscribe<BoxEnteredGoal>(OnBoxEnteredGoal);
        }

        public void Update(float deltaTime)
        {
            foreach (BoxEnteredGoal message in _messages)
            {
                _pending[message.Box] = message.Goal;
            }
            
            _messages.Clear();

            foreach (KeyValuePair<Entity, GridPosition> pending in _pending)
            {
                if (TryStartFeedback(pending.Key, pending.Value))
                {
                    _consumed.Add(pending.Key);
                }
            }
            foreach (Entity entity in _consumed)
            {
                _pending.Remove(entity);
            }
            
            _consumed.Clear();
        }

        public void Dispose()
        {
            _subscription.Dispose();
            _messages.Clear();
            _pending.Clear();
            _consumed.Clear();
        }

        private void OnBoxEnteredGoal(in BoxEnteredGoal message) => 
            _messages.Add(message);

        private bool TryStartFeedback(Entity entity, GridPosition goal)
        {
            if (entity.IsAlive == false || ReferenceEquals(entity.World, _world) == false
                || entity.Has<GridPosition>() == false || entity.Get<GridPosition>() != goal)
            {
                return true;
            }
            if (entity.Has<ViewMove>() || entity.Has<ViewPending>() || entity.Has<TransformComponent>() == false)
            {
                return false;
            }
            if (entity.Has<BoxGoalFeedbackComponent>() == false)
            {
                return entity.Has<TransformComponent>();
            }

            BoxGoalAnimator animator = entity.Get<BoxGoalFeedbackComponent>().Animator;
            if (animator != null)
            {
                animator.PlayGoalFeedback();
                if (animator.IsPlaying)
                {
                    entity.Set(new BoxGoalFeedbackState(goal));
                }
            }
            return true;
        }
    }
}
