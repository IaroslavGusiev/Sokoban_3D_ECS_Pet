using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sokoban.Presentation
{
    [CreateAssetMenu(menuName = "Sokoban/Background Catalog")]
    public sealed class BackgroundCatalog : ScriptableObject
    {
        [field: SerializeField] public List<BackgroundPreset> Presets { get; private set; } = new();
    }

    [Serializable]
    public sealed class BackgroundPreset
    {
        [field: SerializeField] public string Name { get; private set; }
        [field: SerializeField] public string Address { get; private set; }
        [field: SerializeField] public float RotationDegrees { get; private set; }
        [field: SerializeField] public float Exposure { get; private set; } = 10f;

        public BackgroundPreset(string name, string address, float rotationDegrees = 0f, float exposure = 10f)
        {
            Name = name;
            Address = address;
            RotationDegrees = rotationDegrees;
            Exposure = exposure;
        }
    }
}
