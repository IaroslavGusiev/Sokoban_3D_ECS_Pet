using DefaultEcs;
using UnityEngine;
using Sokoban.Presentation.Ecs.Model;
using Sokoban.Presentation.Ecs.Components;

namespace Sokoban.Presentation.Ecs.Adapters
{
    [DisallowMultipleComponent]
    public sealed class BoxGoalFeedbackAdapter : EntityAdapter
    {
        [SerializeField] private BoxGoalAnimator animator;

        private void OnDisable() => 
            StopFeedback();

        private void OnDestroy() => 
            StopFeedback();

        public override void Install(Entity entity) => 
            entity.Set(new BoxGoalFeedbackComponent(animator));

        public override void Uninstall(Entity entity)
        {
            StopFeedback();
            
            entity.Remove<BoxGoalFeedbackState>();
            entity.Remove<BoxGoalFeedbackComponent>();
        }

        private void StopFeedback()
        {
            if (animator != null)
            {
                animator.StopAndRestore();
            }
        }

        private void Reset() =>
            animator = GetComponent<BoxGoalAnimator>();
    }
}
