using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sokoban.Presentation
{
    [CreateAssetMenu(menuName = "Sokoban/Level List")]
    public sealed class LevelList : ScriptableObject
    {
        [SerializeField] private string[] levels = Array.Empty<string>();

        public IReadOnlyList<string> Levels => levels;
        public int Count => levels.Length;

        public void Validate()
        {
            if (levels.Length == 0)
            {
                throw new InvalidOperationException("Assign at least one Addressable level in the level list.");
            }

            var invalid = new List<int>();
            for (int index = 0; index < levels.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(levels[index]))
                {
                    invalid.Add(index);
                }
            }

            if (invalid.Count > 0)
            {
                throw new InvalidOperationException($"Assign nonempty Addressable level addresses at indices: {string.Join(", ", invalid)}.");
            }
        }
    }
}
