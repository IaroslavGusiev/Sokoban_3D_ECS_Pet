using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sokoban.Presentation
{
    internal sealed class GameplayOverlayView : IDisposable
    {
        private const float CompactAspectThreshold = 1.55f;

        private readonly IGameplayStatus _status;

        private readonly VisualElement _overlay;
        private readonly Label _phase;
        private readonly HudView _hud;
        private readonly GameplayControlsView _controlsView;
        private readonly BackgroundControlsView _backgroundView;
        private readonly GameplayResultsView _resultsView;
        private readonly CharacterSelectionView _characterSelectionView;

        private GameplayPhase? _shownPhase;

        public VisualElement Root { get; }

        public GameplayOverlayView(VisualElement root, IGameplayStatus status, IGameplayCommands commands)
        {
            Root = root;
            _status = status;
            _overlay = UiElements.Require<VisualElement>(Root, UiIds.Root.Name);
            _phase = UiElements.Require<Label>(Root, UiIds.Hud.Notice);
            UiElements.Require<VisualElement>(Root, UiIds.Results.Panel);
            UiElements.Require<VisualElement>(Root, UiIds.Results.Completed);
            UiElements.Require<VisualElement>(Root, UiIds.Selection.Panel);

            try
            {
                _hud = new HudView(Root, status);
                _controlsView = new GameplayControlsView(Root, status, commands);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                _backgroundView = new BackgroundControlsView(Root, status, commands);
#else
                UiElements.SetVisible(UiElements.Require<VisualElement>(Root, UiIds.Background.Controls), false);
#endif
                _resultsView = new GameplayResultsView(Root, status, commands);
                _characterSelectionView = new CharacterSelectionView(Root, status, commands);

                Root.pickingMode = PickingMode.Ignore;
                foreach (ScrollView scroll in Root.Query<ScrollView>().ToList())
                {
                    scroll.pickingMode = PickingMode.Ignore;
                    scroll.contentViewport.pickingMode = PickingMode.Ignore;
                    scroll.contentViewport.parent.pickingMode = PickingMode.Ignore;
                    scroll.contentContainer.pickingMode = PickingMode.Ignore;
                }

                Root.RegisterCallback<NavigationMoveEvent>(KeepMovementInGameplay, TrickleDown.TrickleDown);
                _overlay.RegisterCallback<GeometryChangedEvent>(Resize);
                UiElements.SetVisible(_overlay, true);
            }
            catch
            {
                // A later binding failure must release callbacks already owned by earlier children.
                Dispose();
                throw;
            }
        }

        public void Refresh()
        {
            _hud.Refresh();
            RefreshPhase();
            _controlsView.Refresh();
            _backgroundView?.Refresh();
            _resultsView.Refresh();
            _characterSelectionView.Refresh();
        }

        public void Dispose()
        {
            _characterSelectionView?.Dispose();
            _resultsView?.Dispose();
            _backgroundView?.Dispose();
            _controlsView?.Dispose();
            Root.UnregisterCallback<NavigationMoveEvent>(KeepMovementInGameplay, TrickleDown.TrickleDown);
            _overlay.UnregisterCallback<GeometryChangedEvent>(Resize);
            if (_shownPhase.HasValue)
            {
                Root.RemoveFromClassList(PhaseClass(_shownPhase.Value));
            }
            UiElements.SetVisible(_overlay, false);
        }

        private void RefreshPhase()
        {
            if (_shownPhase == _status.Phase)
            {
                return;
            }

            if (_shownPhase.HasValue)
            {
                Root.RemoveFromClassList(PhaseClass(_shownPhase.Value));
            }
            _shownPhase = _status.Phase;
            Root.AddToClassList(PhaseClass(_status.Phase));
            _phase.text = _status.Phase switch
            {
                GameplayPhase.Loading => "Loading level…",
                GameplayPhase.BindingViews => "Preparing board…",
                GameplayPhase.Playing or GameplayPhase.Completed or GameplayPhase.Finished
                    or GameplayPhase.SelectingCharacter or GameplayPhase.BindingCharacter or GameplayPhase.CharacterFailed => "",
                GameplayPhase.ViewsFailed => "Views unavailable. Restart to retry.",
                _ => "Level unavailable. Restart to retry."
            };
        }

        private static string PhaseClass(GameplayPhase phase) => phase switch
        {
            GameplayPhase.Loading => UiIds.Phases.Loading,
            GameplayPhase.BindingViews => UiIds.Phases.BindingViews,
            GameplayPhase.Playing => UiIds.Phases.Playing,
            GameplayPhase.Completed => UiIds.Phases.Completed,
            GameplayPhase.ViewsFailed => UiIds.Phases.ViewsFailed,
            GameplayPhase.Finished => UiIds.Phases.Finished,
            GameplayPhase.SelectingCharacter => UiIds.Phases.SelectingCharacter,
            GameplayPhase.BindingCharacter => UiIds.Phases.BindingCharacter,
            GameplayPhase.CharacterFailed => UiIds.Phases.CharacterFailed,
            _ => UiIds.Phases.LevelFailed
        };

        private void KeepMovementInGameplay(NavigationMoveEvent evt)
        {
            if (_status.Phase is GameplayPhase.SelectingCharacter or GameplayPhase.BindingCharacter or GameplayPhase.CharacterFailed)
            {
                return;
            }

            // Tab/Shift+Tab retain Next/Previous navigation. Directional keys still reach
            // KeyboardMoveInput; they must not also move focus or scroll a focused panel.
            if (evt.direction == NavigationMoveEvent.Direction.Left
                || evt.direction == NavigationMoveEvent.Direction.Right
                || evt.direction == NavigationMoveEvent.Direction.Up
                || evt.direction == NavigationMoveEvent.Direction.Down)
            {
                ((VisualElement)evt.currentTarget).focusController.IgnoreEvent(evt);
                evt.StopPropagation();
            }
        }

        private void Resize(GeometryChangedEvent evt) =>
            _overlay.EnableInClassList(UiIds.Classes.Compact, evt.newRect.width < evt.newRect.height * CompactAspectThreshold);
    }
}
