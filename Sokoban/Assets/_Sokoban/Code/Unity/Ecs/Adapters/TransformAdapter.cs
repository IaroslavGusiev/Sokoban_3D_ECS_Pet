using UnityEngine;
using Sokoban.Presentation.Ecs.Model;
using Sokoban.Presentation.Ecs.Components;

namespace Sokoban.Presentation.Ecs.Adapters
{
    [DisallowMultipleComponent]
    public sealed class TransformAdapter : ComponentAdapter<TransformComponent>
    {
        private void Reset() => 
            Value = new TransformComponent(transform);
    }
}
