using DefaultEcs;
using UnityEngine;

namespace Sokoban.Presentation.Ecs.Model
{
    public abstract class EntityAdapter : MonoBehaviour
    {
        public abstract void Install(Entity entity);
        
        public abstract void Uninstall(Entity entity);
    }
}
