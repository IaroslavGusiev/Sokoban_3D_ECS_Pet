using DefaultEcs;
using UnityEngine;
using UnityEngine.Serialization;

namespace Sokoban.Presentation.Ecs.Model
{
    public abstract class ComponentAdapter<T> : EntityAdapter where T : struct
    {
        [FormerlySerializedAs("_preset")]
        [SerializeField] protected T _value;

        public sealed override void Install(Entity entity)
        {
            entity.Set(_value);
        }

        public sealed override void Uninstall(Entity entity) => entity.Remove<T>();
    }
}
