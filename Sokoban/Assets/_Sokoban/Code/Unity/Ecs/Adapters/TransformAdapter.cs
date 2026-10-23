using Sokoban.Presentation.Ecs.Components;
using Sokoban.Presentation.Ecs.Model;
using UnityEngine;

namespace Sokoban.Presentation.Ecs.Adapters
{
    [DisallowMultipleComponent]
    public sealed class TransformAdapter : ComponentAdapter<TransformComponent>
    {
        private void Reset() => 
            _value = new TransformComponent(transform);
    }
}
