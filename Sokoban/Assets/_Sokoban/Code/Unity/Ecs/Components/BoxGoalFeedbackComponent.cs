using System;
using UnityEngine;

namespace Sokoban.Presentation.Ecs.Components
{
    [Serializable]
    public struct BoxGoalFeedbackComponent
    {
        [SerializeField] private BoxGoalAnimator animator;

        public BoxGoalAnimator Animator => animator;

        public BoxGoalFeedbackComponent(BoxGoalAnimator animator) =>
            this.animator = animator;
    }
}
