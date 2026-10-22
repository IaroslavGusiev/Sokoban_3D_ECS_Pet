using System;
using DefaultEcs;
using UnityEngine;
using UnityEngine.Serialization;

namespace Sokoban.Unity.Ecs.Model
{
    public abstract class ComponentAdapter<T> : EntityAdapter where T : struct
    {
        [FormerlySerializedAs("_preset")]
        [SerializeField] protected T _value;

        public sealed override void Install(Entity entity)
        {
            if (entity.Has<T>())
            {
                throw new InvalidOperationException($"The entity already has {typeof(T).Name}.");
            }

            entity.Set(_value);
        }
    }
}
