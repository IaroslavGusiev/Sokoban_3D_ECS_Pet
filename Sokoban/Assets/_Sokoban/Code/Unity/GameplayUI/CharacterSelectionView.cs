using System;
using System.Linq;
using UnityEngine.UIElements;
using System.Collections.Generic;

namespace Sokoban.Presentation
{
    internal sealed class CharacterSelectionView : IDisposable
    {
        private readonly IGameplayStatus _status;
        private readonly IGameplayCommands _commands;

        private readonly Button _confirm;
        private readonly Label _feedback;
        private readonly VisualElement _panel;
        private readonly VisualElement _container;
        private readonly VisualTreeAsset _template;
        private readonly ButtonBinding _confirmation;

        private readonly Dictionary<SkinId, UnityEngine.Texture> _portraits = new();
        private readonly List<(SkinId Skin, Button Button, ButtonBinding Binding)> _choices = new();
        
        private IVisualElementScheduledItem _focus;
        private GameplayPhase? _shownPhase;
        private bool _disposed;

        public CharacterSelectionView(VisualElement root, IGameplayStatus status, IGameplayCommands commands)
        {
            _status = status;
            _commands = commands;
            
            _panel = UiElements.Require<VisualElement>(root, UiIds.Selection.Panel);
            _container = UiElements.Require<VisualElement>(_panel, UiIds.Selection.Choices);
            
            var template = UiElements.Require<TemplateContainer>(_panel, UiIds.Selection.Template);
            _template = template.templateSource ?? throw new InvalidOperationException("Character choice template is missing.");

            UiElements.Require<Label>(template, UiIds.Choice.Name);
            UiElements.Require<Button>(template, UiIds.Choice.Button);
            UiElements.Require<Image>(template, UiIds.Choice.Portrait);

            foreach (CharacterPortrait portrait in UiElements.Require<VisualElement>(_panel, UiIds.Selection.Portraits)
                         .Query<CharacterPortrait>()
                         .ToList()
                         .Where(portrait => portrait.Portrait == null || _portraits.TryAdd(portrait.Skin, portrait.Portrait) == false))
            {
                throw new InvalidOperationException($"Missing or duplicate portrait for {portrait.Skin}.");
            }

            _confirm = UiElements.Require<Button>(_panel, UiIds.Selection.Confirm);
            _feedback = UiElements.Require<Label>(_panel, UiIds.Selection.Feedback);

            _confirmation = new ButtonBinding(_confirm, () => _commands.CanConfirmCharacter, _commands.RequestConfirmCharacter);
        }

        public void Refresh()
        {
            RefreshChoices();
            
            foreach ((SkinId Skin, Button Button, ButtonBinding Binding) choice in _choices)
            {
                choice.Button.EnableInClassList(UiIds.Choice.SelectedClass, choice.Skin == _status.SelectedCharacter);
                choice.Binding.Refresh();
            }

            _confirmation.Refresh();
            
            if (_shownPhase == _status.Phase)
            {
                return;
            }

            _shownPhase = _status.Phase;
            
            _feedback.text = _status.Phase switch
            {
                GameplayPhase.BindingCharacter => "Preparing your character…",
                GameplayPhase.CharacterFailed => _commands.CanChooseCharacter
                    ? "Character unavailable. Retry or choose another."
                    : "Your character is unavailable. Retry the same character.",
                _ => "Choose a character, then confirm to begin."
            };
            
            _confirm.text = _status.Phase switch
            {
                GameplayPhase.BindingCharacter => "Loading…",
                GameplayPhase.CharacterFailed => "Retry",
                _ => "Start"
            };

            _focus?.Pause();
            
            if (_commands.CanChooseCharacter)
            {
                _focus = _panel.schedule.Execute(FocusSelected);
            }
        }

        public void Dispose()
        {
            _disposed = true;
            _focus?.Pause();
            _confirmation.Dispose();
            ReleaseChoices();
        }

        private void RefreshChoices()
        {
            bool changed = _choices.Count != _status.Characters.Count;
            
            for (var index = 0; changed == false && index < _choices.Count; index++)
            {
                changed = _choices[index].Skin != _status.Characters[index];
            }

            if (changed == false)
            {
                return;
            }

            ReleaseChoices();
            _container.Clear();
            
            foreach (SkinId skin in _status.Characters)
            {
                TemplateContainer row = _template.CloneTree();
                
                row.pickingMode = PickingMode.Ignore;
                var button = UiElements.Require<Button>(row, UiIds.Choice.Button);
                UiElements.Require<Label>(button, UiIds.Choice.Name).text = DisplayName(skin);
                
                if (_portraits.TryGetValue(skin, out UnityEngine.Texture portrait) == false)
                {
                    throw new InvalidOperationException($"Character selection is missing a portrait for {skin}.");
                }
                
                UiElements.Require<Image>(button, UiIds.Choice.Portrait).image = portrait;
                var binding = new ButtonBinding(button, () => _commands.CanChooseCharacter, () => _commands.RequestCharacter(skin));
                
                _choices.Add((skin, button, binding));
                _container.Add(row);
            }
        }

        private void ReleaseChoices()
        {
            foreach ((SkinId Skin, Button Button, ButtonBinding Binding) choice in _choices)
            {
                choice.Binding.Dispose();
            }

            _choices.Clear();
        }

        private void FocusSelected()
        {
            if (_disposed || _commands.CanChooseCharacter == false)
            {
                return;
            }

            foreach ((SkinId Skin, Button Button, ButtonBinding Binding) choice in _choices
                         .Where(choice => choice.Skin == _status.SelectedCharacter))
            {
                choice.Button.Focus();
                return;
            }
        }

        private static string DisplayName(SkinId skin) => skin switch
        {
            SkinId.Default => "Doctor",
            SkinId.Alternate => "Farmer",
            SkinId.Punk => "Punk",
            _ => skin.ToString()
        };

    }
}
