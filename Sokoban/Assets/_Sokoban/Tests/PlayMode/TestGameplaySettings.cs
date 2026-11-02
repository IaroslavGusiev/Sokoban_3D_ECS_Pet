using System.Reflection;
using Sokoban.Presentation;
using UnityEngine;

namespace Sokoban.Tests.PlayMode
{
    internal static class TestGameplaySettings
    {
        public static GameplaySettings Create(float timeLimit = 60f, float moveDuration = 0.16f)
        {
            var settings = ScriptableObject.CreateInstance<GameplaySettings>();
            Set(settings, "<LevelTimeLimitSeconds>k__BackingField", timeLimit);
            Set(settings, "<MoveDurationSeconds>k__BackingField", moveDuration);
            return settings;
        }

        private static void Set(GameplaySettings settings, string name, float value) =>
            typeof(GameplaySettings).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(settings, value);
    }
}
