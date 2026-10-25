using UnityEngine;

namespace Sokoban.Presentation
{
    [CreateAssetMenu(menuName = "Sokoban/Level List")]
    public sealed class LevelList : ScriptableObject
    {
        [field: SerializeField] public LevelConfig[] Levels { get; private set; }

        public int Count => Levels.Length;
    }
}
