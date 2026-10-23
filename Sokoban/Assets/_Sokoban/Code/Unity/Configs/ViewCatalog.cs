using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;

namespace Sokoban.Presentation
{
    [CreateAssetMenu(menuName = "Sokoban/View Catalog")]
    public sealed class ViewCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class KindEntry
        {
            public ViewKind Kind;
            public SkinEntry[] Skins = Array.Empty<SkinEntry>();
        }

        [Serializable]
        public sealed class SkinEntry
        {
            public SkinId Skin;
            public string Address;
        }

        [SerializeField] private KindEntry[] entries = Array.Empty<KindEntry>();

        public IReadOnlyList<KindEntry> Entries => entries;

        public SkinEntry Resolve(ViewKey key)
        {
            KindEntry entry = entries.Single(entry => entry.Kind == key.Kind);
            foreach (SkinEntry skin in entry.Skins)
            {
                if (skin.Skin == key.Skin) return skin;
            }

            return entry.Skins.Single(skin => skin.Skin == SkinId.Default);
        }

        public void Validate()
        {
            var problems = new List<string>();
            foreach (ViewKind kind in Enum.GetValues(typeof(ViewKind)))
            {
                int count = entries.Count(entry => entry.Kind == kind);
                if (count == 0)
                {
                    problems.Add($"{kind}: missing");
                }
                else if (count > 1)
                {
                    problems.Add($"{kind}: duplicated");
                }
            }

            foreach (KindEntry entry in entries)
            {
                if (entry.Skins.All(skin => skin.Skin != SkinId.Default))
                {
                    problems.Add($"{entry.Kind}: missing Default");
                }

                foreach (var group in entry.Skins.GroupBy(skin => skin.Skin))
                {
                    var key = new ViewKey(entry.Kind, group.Key);
                    if (group.Count() > 1)
                    {
                        problems.Add($"{key}: duplicated");
                    }

                    if (group.Any(skin => string.IsNullOrWhiteSpace(skin.Address)))
                    {
                        problems.Add($"{key}: empty address");
                    }
                }
            }

            if (problems.Count > 0)
            {
                throw new InvalidOperationException($"Fix Addressable view catalog entries: {string.Join("; ", problems)}.");
            }
        }
    }
}
