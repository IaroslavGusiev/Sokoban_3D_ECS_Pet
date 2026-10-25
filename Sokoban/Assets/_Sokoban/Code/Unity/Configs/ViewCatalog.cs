using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;

namespace Sokoban.Presentation
{
    [CreateAssetMenu(menuName = "Sokoban/View Catalog")]
    public sealed class ViewCatalog : ScriptableObject
    {
        [field: SerializeField] public List<KindEntry> Entries { get; private set; }

        public SkinEntry Resolve(ViewKey key)
        {
            KindEntry entry = Entries.Single(entry => entry.Kind == key.Kind);
            
            foreach (SkinEntry skin in entry.Skins.Where(skin => skin.Skin == key.Skin))
            {
                return skin;
            }

            return entry.Skins.Single(skin => skin.Skin == SkinId.Default);
        }

        [Serializable]
        public sealed class KindEntry
        {
            public ViewKind Kind;
            public List<SkinEntry> Skins;
        }

        [Serializable]
        public sealed class SkinEntry
        {
            public SkinId Skin;
            public string Address;
        }
    }
}
