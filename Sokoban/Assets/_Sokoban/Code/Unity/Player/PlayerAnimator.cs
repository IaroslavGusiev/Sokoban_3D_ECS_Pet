using UnityEngine;

namespace Sokoban.Presentation
{
    [DisallowMultipleComponent]
    public sealed class PlayerAnimator : MonoBehaviour
    {
        private static readonly int Moving = Animator.StringToHash("Moving");
        
        private const int PushLayer = 1;
        private const float PushBlendSeconds = 0.05f;

        [SerializeField] private Animator animator;

        public void SetMoving(bool moving) => 
            animator.SetBool(Moving, moving);

        public void SetPushing(bool pushing, float deltaTime)
        {
            float target = pushing 
                ? 1f 
                : 0f;
            
            animator.SetLayerWeight(
                layerIndex: PushLayer, 
                weight: Mathf.MoveTowards(animator.GetLayerWeight(PushLayer), 
                    target: target, 
                    maxDelta: deltaTime / PushBlendSeconds));
        }
    }
}
