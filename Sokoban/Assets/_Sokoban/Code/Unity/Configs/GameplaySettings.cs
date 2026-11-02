using UnityEngine;

namespace Sokoban.Presentation
{
    [CreateAssetMenu(menuName = "Sokoban/Gameplay Settings")]
    public sealed class GameplaySettings : ScriptableObject
    {
        [field: SerializeField] public float LevelTimeLimitSeconds { get; private set; } = 60f;

        [field: SerializeField] public float MoveDurationSeconds { get; private set; } = 0.16f;
    }
}
