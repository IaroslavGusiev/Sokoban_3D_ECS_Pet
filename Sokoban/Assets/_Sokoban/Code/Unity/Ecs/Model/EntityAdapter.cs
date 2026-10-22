using DefaultEcs;
using UnityEngine;

namespace Sokoban.Unity.Ecs.Model
{
    public abstract class EntityAdapter : MonoBehaviour
    {
        public abstract void Install(Entity entity);
    }
}
