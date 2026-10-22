using System;
using DefaultEcs;
using UnityEngine;

namespace Sokoban.Unity.Ecs.Model
{
    [DisallowMultipleComponent]
    public sealed class EntityView : MonoBehaviour
    {
        [SerializeField] private EntityAdapter[] _adapters = Array.Empty<EntityAdapter>();

        // Views share the session lifetime. The session disposes the entity and its components.
        public void Install(Entity entity)
        {
            foreach (EntityAdapter adapter in _adapters)
            {
                if (adapter == null)
                {
                    throw new InvalidOperationException("Assign every adapter on the view.");
                }

                adapter.Install(entity);
            }
        }

        private void Reset() => 
            _adapters = GetComponents<EntityAdapter>();
    }
}
