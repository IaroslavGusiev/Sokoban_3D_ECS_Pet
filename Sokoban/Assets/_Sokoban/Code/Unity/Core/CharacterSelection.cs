using System;
using System.Collections.Generic;
using System.Linq;

namespace Sokoban.Presentation
{
    internal sealed class CharacterSelection
    {
        private readonly List<SkinId> _choices = new();
        private SkinId? _requested;
        private bool _confirmationRequested;

        public IReadOnlyList<SkinId> Choices { get; }
        public SkinId Selected { get; private set; }

        public CharacterSelection() =>
            Choices = _choices.AsReadOnly();

        public void Begin(ViewCatalog catalog, SkinId preferred)
        {
            _choices.Clear();
            ClearRequests();
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

        public void RequestSelection(SkinId skin)
        {
            if (_choices.Contains(skin))
            {
                _requested = skin;
            }
        }

        public void RequestConfirmation() =>
            _confirmationRequested = true;

        public bool ConsumeConfirmation(out SkinId skin)
        {
            if (_requested.HasValue)
            {
                Selected = _requested.Value;
            }

            skin = Selected;
            bool confirmed = _confirmationRequested;
            ClearRequests();
            return confirmed;
        }

        public void ClearRequests()
        {
            _requested = null;
            _confirmationRequested = false;
        }
    }
}
