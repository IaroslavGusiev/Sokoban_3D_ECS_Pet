using System;
using DefaultEcs;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace Sokoban.Presentation.Ecs.Model
{
    [DisallowMultipleComponent]
    public sealed class EntityView : MonoBehaviour
    {
        [SerializeField] private List<EntityAdapter> adapters;

        public void Install(Entity entity)
        {
            foreach (EntityAdapter adapter in adapters)
            {
                if (adapter == null)
                {
                    throw new InvalidOperationException("Assign every adapter on the view.");
                }

                adapter.Install(entity);
            }
        }

        public void Uninstall(Entity entity)
        {
            foreach (EntityAdapter adapter in adapters)
            {
                adapter.Uninstall(entity);
            }
        }

        private void Reset()
        {
            adapters = GetComponents<EntityAdapter>()
                .ToList();
        }
    }
}
