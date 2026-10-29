using System;
using Sokoban.Core.Commands;
using UnityEngine.UIElements;

namespace Sokoban.Presentation
{
    internal sealed class GameplayControlsView : IDisposable
    {
        private readonly IGameplayStatus _status;

        private readonly Label _phaseDetail;
        private readonly Label _hintMessage;
        private readonly Label _hintTitle;
        private readonly ButtonBinding _restart;
        private readonly ButtonBinding _undo;
        private readonly ButtonBinding _hint;

        private GameplayPhase? _shownPhase;
        private HintStatus? _shownHint;
        private MoveCommand _shownHintMove;

        public GameplayControlsView(VisualElement root, IGameplayStatus status, IGameplayCommands commands)
        {
            _status = status;
            _phaseDetail = UiElements.Require<Label>(root, UiIds.Controls.PhaseDetail);
            _hintMessage = UiElements.Require<Label>(root, UiIds.Controls.HintMessage);
            _hintTitle = UiElements.Require<Label>(root, UiIds.Controls.HintTitle);
            Button restart = UiElements.Require<Button>(root, UiIds.Controls.Restart);
            Button undo = UiElements.Require<Button>(root, UiIds.Controls.Undo);
            Button hint = UiElements.Require<Button>(root, UiIds.Controls.Hint);

            _restart = new ButtonBinding(restart, () => commands.CanRestart, commands.RequestRestart);
            _undo = new ButtonBinding(undo, () => commands.CanUndo, commands.RequestUndo);
            _hint = new ButtonBinding(hint, () => commands.CanHint, commands.RequestHint);
        }

        public void Refresh()
        {
            RefreshAvailability();
            RefreshHint();
            RefreshPhaseDetail();
        }

        public void Dispose()
        {
            _restart.Dispose();
            _undo.Dispose();
            _hint.Dispose();
        }

        private void RefreshAvailability()
        {
            _restart.Refresh();
            _undo.Refresh();
            _hint.Refresh();
        }

        private void RefreshHint()
        {
            if (_shownHint == _status.HintStatus && _shownHintMove == _status.HintMove)
            {
                return;
            }

            _shownHint = _status.HintStatus;
            _shownHintMove = _status.HintMove;
            _hintTitle.text = _status.HintStatus == HintStatus.Calculating ? "Calculating…" : "Hint";
            _hintMessage.text = _status.HintStatus switch
            {
                HintStatus.Calculating => "Finding a shortest move…",
                HintStatus.Available => $"Hint: {_status.HintMove}",
                HintStatus.NoSolution => "No solution found",
                HintStatus.BudgetExhausted => "Search budget exhausted",
                HintStatus.Failed => "Calculation failed. Try another hint.",
                HintStatus.AlreadySolved => "Already solved",
                _ => ""
            };
            UiElements.SetVisible(_hintMessage, _hintMessage.text.Length > 0);
        }

        private void RefreshPhaseDetail()
        {
            if (_shownPhase != _status.Phase)
            {
                _shownPhase = _status.Phase;
                _phaseDetail.text = $"Phase: {_status.Phase}";
            }

        }

    }
}
