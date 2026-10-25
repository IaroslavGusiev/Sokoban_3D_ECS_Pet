using DefaultEcs;
using UnityEngine;

namespace Sokoban.Presentation.Ecs.Model
{
    public abstract class ComponentAdapter<T> : EntityAdapter where T : struct
    {
        [SerializeField] protected T Value;

        public sealed override void Install(Entity entity) => 
            entity.Set(Value);

        public sealed override void Uninstall(Entity entity) => 
            entity.Remove<T>();
    }
}
