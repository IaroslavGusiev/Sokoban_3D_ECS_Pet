using System;
using DefaultEcs;
using UnityEngine;

namespace Sokoban.Presentation.Ecs.Model
{
    [DisallowMultipleComponent]
    public sealed class EntityView : MonoBehaviour
    {
        [SerializeField] private EntityAdapter[] _adapters = Array.Empty<EntityAdapter>();

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

        public void Uninstall(Entity entity)
        {
            foreach (EntityAdapter adapter in _adapters) adapter.Uninstall(entity);
        }
    }
}
