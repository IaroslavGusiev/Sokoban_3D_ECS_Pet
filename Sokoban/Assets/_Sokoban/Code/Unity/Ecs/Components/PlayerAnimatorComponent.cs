using System;
using UnityEngine;

namespace Sokoban.Presentation.Ecs.Components
{
    [Serializable]
    public struct PlayerAnimatorComponent
    {
        [SerializeField] private PlayerAnimator animator;
        
        public PlayerAnimator Animator => animator;
        
        public PlayerAnimatorComponent(PlayerAnimator animator) => 
            this.animator = animator;
    }
}
