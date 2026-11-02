using System;
using UnityEngine.UIElements;

namespace Sokoban.Presentation
{
    internal sealed class GameplayOverlayView : IDisposable
    {
        private const float CompactAspectThreshold = 1.55f;

        private readonly Label _phase;
        private readonly HudView _hud;
        private readonly VisualElement _overlay;
        private readonly IGameplayStatus _status;
        private readonly GameplayResultsView _resultsView;
        private readonly GameplayReplayView _replayView;
        private readonly GameplayControlsView _controlsView;
        private readonly BackgroundControlsView _backgroundView;
        private readonly CharacterSelectionView _characterSelectionView;

        private GameplayPhase? _shownPhase;
        
        public VisualElement Root { get; }

        public GameplayOverlayView(VisualElement root, IGameplayStatus status, IGameplayCommands commands)
        {
            Root = root;
            _status = status;

            _phase = UiElements.Require<Label>(Root, UiIds.Hud.Notice);
            _overlay = UiElements.Require<VisualElement>(Root, UiIds.Root.Name);

            UiElements.Require<VisualElement>(Root, UiIds.Results.Panel);
            UiElements.Require<VisualElement>(Root, UiIds.Selection.Panel);
            UiElements.Require<VisualElement>(Root, UiIds.Results.Completed);

            // A later binding failure must release callbacks already owned by earlier children.
            try
            {
                _hud = new HudView(Root, status);
                _controlsView = new GameplayControlsView(Root, status, commands);
                
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                _backgroundView = new BackgroundControlsView(Root, status, commands);
#else
                _backgroundView = null;
                UiElements.SetVisible(UiElements.Require<VisualElement>(Root, UiIds.Background.Controls), false);
#endif
                
                _resultsView = new GameplayResultsView(Root, status, commands);
                _replayView = new GameplayReplayView(Root, status, commands);
                _characterSelectionView = new CharacterSelectionView(Root, status, commands);

                Root.pickingMode = PickingMode.Ignore;
                
                foreach (ScrollView scroll in Root
                             .Query<ScrollView>()
                             .ToList())
                {
                    scroll.pickingMode = PickingMode.Ignore;
                    scroll.contentViewport.pickingMode = PickingMode.Ignore;
                    scroll.contentContainer.pickingMode = PickingMode.Ignore;
                    scroll.contentViewport.parent.pickingMode = PickingMode.Ignore;
                }

                Root.RegisterCallback<NavigationMoveEvent>(KeepMovementInGameplay, TrickleDown.TrickleDown);
                _overlay.RegisterCallback<GeometryChangedEvent>(Resize);
                
                UiElements.SetVisible(_overlay, true);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Refresh()
        {
            _hud.Refresh();
            RefreshPhase();

            _resultsView.Refresh();
            _replayView.Refresh();
            _controlsView.Refresh();
            _backgroundView?.Refresh();

            _characterSelectionView.Refresh();
        }

        public void Dispose()
        {
            _characterSelectionView?.Dispose();
            
            _replayView?.Dispose();
            _resultsView?.Dispose();
            _controlsView?.Dispose();
            _backgroundView?.Dispose();

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

            string phaseClass = PhaseClass(_status.Phase);
            
            if (_shownPhase.HasValue)
            {
                Root.RemoveFromClassList(PhaseClass(_shownPhase.Value));
            }
            
            _shownPhase = _status.Phase;
            Root.AddToClassList(phaseClass);
            
            _phase.text = _status.Phase switch
            {
                GameplayPhase.Loading => "Loading level…",
                GameplayPhase.BindingViews => "Preparing board…",
                GameplayPhase.Playing 
                    or GameplayPhase.Completed 
                    or GameplayPhase.Finished
                    or GameplayPhase.SelectingCharacter 
                    or GameplayPhase.BindingCharacter 
                    or GameplayPhase.CharacterFailed
                    or GameplayPhase.ReplayLoading
                    or GameplayPhase.Replaying
                    or GameplayPhase.ReplayPaused
                    or GameplayPhase.ReplayFinished
                    or GameplayPhase.ReplayFailed => "",
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
            GameplayPhase.LevelFailed => UiIds.Phases.LevelFailed,
            GameplayPhase.Finished => UiIds.Phases.Finished,
            GameplayPhase.SelectingCharacter => UiIds.Phases.SelectingCharacter,
            GameplayPhase.BindingCharacter => UiIds.Phases.BindingCharacter,
            GameplayPhase.CharacterFailed => UiIds.Phases.CharacterFailed,
            GameplayPhase.ReplayLoading => UiIds.Phases.ReplayLoading,
            GameplayPhase.Replaying => UiIds.Phases.Replaying,
            GameplayPhase.ReplayPaused => UiIds.Phases.ReplayPaused,
            GameplayPhase.ReplayFinished => UiIds.Phases.ReplayFinished,
            GameplayPhase.ReplayFailed => UiIds.Phases.ReplayFailed,
            _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "Unsupported gameplay phase.")
        };

        private void KeepMovementInGameplay(NavigationMoveEvent evt)
        {
            if (_status.Phase 
                is GameplayPhase.SelectingCharacter 
                or GameplayPhase.BindingCharacter 
                or GameplayPhase.CharacterFailed)
            {
                return;
            }
            
            if (evt.direction 
                is NavigationMoveEvent.Direction.Left 
                or NavigationMoveEvent.Direction.Right 
                or NavigationMoveEvent.Direction.Up 
                or NavigationMoveEvent.Direction.Down)
            {
                ((VisualElement) evt.currentTarget).focusController.IgnoreEvent(evt);
                evt.StopPropagation();
            }
        }

        private void Resize(GeometryChangedEvent evt) =>
            _overlay.EnableInClassList(UiIds.Classes.Compact, evt.newRect.width < evt.newRect.height * CompactAspectThreshold);
    }
}
