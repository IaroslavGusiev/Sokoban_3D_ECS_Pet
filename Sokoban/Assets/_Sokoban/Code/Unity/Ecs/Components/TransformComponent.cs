using System;
using UnityEngine;

namespace Sokoban.Presentation.Ecs.Components
{
    [Serializable]
    public struct TransformComponent
    {
        [SerializeField] private Transform _transform;

        public Transform Transform => _transform;

        public TransformComponent(Transform transform)
        {
            _transform = transform;
        }
    }
}
