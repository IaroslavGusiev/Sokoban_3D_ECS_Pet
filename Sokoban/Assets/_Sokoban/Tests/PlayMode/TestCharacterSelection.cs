using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sokoban.Presentation;
using UnityEngine;

namespace Sokoban.Tests.PlayMode
{
    internal static class TestCharacterSelection
    {
        public static void Configure(TestStaticData data, ICollection<UnityEngine.Object> owned)
        {
            if (data.Views != null)
            {
                return;
            }

            var catalog = ScriptableObject.CreateInstance<ViewCatalog>();
            owned.Add(catalog);
            var players = new ViewCatalog.KindEntry
            {
                Kind = ViewKind.Player,
                Skins = ((SkinId[])Enum.GetValues(typeof(SkinId)))
                    .Select(skin => new ViewCatalog.SkinEntry { Skin = skin, Address = "test/player/" + skin })
                    .ToList()
            };
            typeof(ViewCatalog).GetProperty(nameof(ViewCatalog.Entries), BindingFlags.Instance | BindingFlags.Public)
                .SetValue(catalog, new List<ViewCatalog.KindEntry> { players });
            data.Views = catalog;
        }

        // Existing non-selection scenarios enter through the same public confirmation boundary,
        // without adding a simulation tick or a production bypass for the new entry flow.
        public static void Tick(GameplayController controller) =>
            Tick(controller, Time.deltaTime);

        public static void Tick(GameplayController controller, float deltaTime)
        {
            controller.Tick(deltaTime);
            if (controller.Phase == GameplayPhase.SelectingCharacter)
            {
                controller.RequestConfirmCharacter();
                controller.Tick(0f);
            }
        }
    }
}
