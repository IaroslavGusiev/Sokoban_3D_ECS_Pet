using System;
using System.Collections.Generic;
using System.Linq;

namespace Sokoban.Presentation
{
    internal sealed class CharacterSelection
    {
        private readonly List<SkinId> _choices = new();

        public IReadOnlyList<SkinId> Choices { get; }
        public SkinId Selected { get; private set; }

        public CharacterSelection() =>
            Choices = _choices.AsReadOnly();

        public void Begin(ViewCatalog catalog, SkinId preferred)
        {
            _choices.Clear();
            ViewCatalog.KindEntry players = catalog?.Entries?.Single(entry => entry.Kind == ViewKind.Player);
            if (players?.Skins == null)
            {
                throw new InvalidOperationException("The player view catalog is unavailable.");
            }

            foreach (ViewCatalog.SkinEntry entry in players.Skins)
            {
                if (string.IsNullOrWhiteSpace(entry.Address) == false && _choices.Contains(entry.Skin) == false)
                {
                    _choices.Add(entry.Skin);
                }
            }

            if (_choices.Count == 0)
            {
                throw new InvalidOperationException("The player view catalog contains no characters.");
            }

            Selected = _choices.Contains(preferred) ? preferred : _choices[0];
        }

        public void Select(SkinId skin)
        {
            if (_choices.Contains(skin))
            {
                Selected = skin;
            }
        }
    }
}
