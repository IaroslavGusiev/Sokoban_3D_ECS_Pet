using NUnit.Framework;
using Sokoban.Presentation;
using UnityEngine;

namespace Sokoban.Tests.Editor
{
    public sealed class ConfigSerializationTests
    {
        [Test]
        public void GameplaySettings_RoundTripsSerializedProperties()
        {
            var config = ScriptableObject.CreateInstance<GameplaySettings>();
            var restored = ScriptableObject.CreateInstance<GameplaySettings>();
            try
            {
                JsonUtility.FromJsonOverwrite("{\"<LevelTimeLimitSeconds>k__BackingField\":17.5,\"<CompletionDelaySeconds>k__BackingField\":1.25,\"<MoveDurationSeconds>k__BackingField\":0.3}", config);
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(config), restored);
                Assert.That(restored.LevelTimeLimitSeconds, Is.EqualTo(17.5f));
                Assert.That(restored.CompletionDelaySeconds, Is.EqualTo(1.25f));
                Assert.That(restored.MoveDurationSeconds, Is.EqualTo(0.3f));
            }
            finally
            {
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(restored);
            }
        }

        [Test]
        public void LevelList_RoundTripsSerializedOrder()
        {
            var config = ScriptableObject.CreateInstance<LevelList>();
            var restored = ScriptableObject.CreateInstance<LevelList>();
            try
            {
                JsonUtility.FromJsonOverwrite("{\"<Levels>k__BackingField\":[{\"address\":\"levels/second\"},{\"address\":\"levels/first\"}]}", config);
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(config), restored);
                Assert.That(restored.Count, Is.EqualTo(2));
                Assert.That(restored.Levels[0].Address, Is.EqualTo("levels/second"));
                Assert.That(restored.Levels[1].Address, Is.EqualTo("levels/first"));
            }
            finally
            {
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(restored);
            }
        }

        [Test]
        public void BackgroundCatalog_RoundTripsOrderedAddressesAndOrientation()
        {
            var config = ScriptableObject.CreateInstance<BackgroundCatalog>();
            var restored = ScriptableObject.CreateInstance<BackgroundCatalog>();
            try
            {
                config.Presets.Add(new BackgroundPreset("First", "backgrounds/first", 45f, 8.5f));
                config.Presets.Add(new BackgroundPreset("Second", "backgrounds/second", 90f, 10f));
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(config), restored);
                Assert.That(restored.Presets.Count, Is.EqualTo(2));
                Assert.That(restored.Presets[0].Name, Is.EqualTo("First"));
                Assert.That(restored.Presets[0].Address, Is.EqualTo("backgrounds/first"));
                Assert.That(restored.Presets[0].RotationDegrees, Is.EqualTo(45f));
                Assert.That(restored.Presets[0].Exposure, Is.EqualTo(8.5f));
                Assert.That(restored.Presets[1].Address, Is.EqualTo("backgrounds/second"));
            }
            finally
            {
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(restored);
            }
        }

        [Test]
        public void ViewCatalog_RoundTripsSerializedSkinEntries()
        {
            var config = ScriptableObject.CreateInstance<ViewCatalog>();
            var restored = ScriptableObject.CreateInstance<ViewCatalog>();
            try
            {
                JsonUtility.FromJsonOverwrite("{\"<Entries>k__BackingField\":[{\"Kind\":3,\"Skins\":[{\"Skin\":0,\"Address\":\"views/default\"},{\"Skin\":1,\"Address\":\"views/alternate\"}]}]}", config);
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(config), restored);
                Assert.That(restored.Entries.Count, Is.EqualTo(1));
                Assert.That(restored.Resolve(new ViewKey(ViewKind.Player, SkinId.Alternate)).Address, Is.EqualTo("views/alternate"));
                Assert.That(restored.Resolve(new ViewKey(ViewKind.Player, SkinId.Punk)).Address, Is.EqualTo("views/default"));
            }
            finally
            {
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(restored);
            }
        }
    }
}
