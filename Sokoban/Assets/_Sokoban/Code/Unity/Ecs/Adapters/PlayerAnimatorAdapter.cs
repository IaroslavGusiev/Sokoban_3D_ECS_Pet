using UnityEngine;
using Sokoban.Presentation.Ecs.Model;
using Sokoban.Presentation.Ecs.Components;

namespace Sokoban.Presentation.Ecs.Adapters
{
    [DisallowMultipleComponent]
    public sealed class PlayerAnimatorAdapter : ComponentAdapter<PlayerAnimatorComponent>
    {
        private void Reset() => Value = 
            new PlayerAnimatorComponent(GetComponent<PlayerAnimator>());
    }
}
