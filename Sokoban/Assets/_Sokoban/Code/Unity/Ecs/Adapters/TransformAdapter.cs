using Sokoban.Unity.Ecs.Components;
using Sokoban.Unity.Ecs.Model;
using UnityEngine;

namespace Sokoban.Unity.Ecs.Adapters
{
    [DisallowMultipleComponent]
    public sealed class TransformAdapter : ComponentAdapter<TransformComponent>
    {
        private void Reset() => 
            _value = new TransformComponent(transform);
    }
}
