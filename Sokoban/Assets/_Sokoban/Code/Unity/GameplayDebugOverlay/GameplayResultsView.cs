using System;
using System.Collections.Generic;
using Sokoban.Core;
using UnityEngine.UIElements;

namespace Sokoban.Presentation
{
    internal sealed class GameplayResultsView : IDisposable
    {
        private readonly IGameplayStatus _status;

        private readonly VisualElement _resultRows;
        private readonly VisualTreeAsset _resultRowTemplate;
        private readonly Label _completedStats;
        private readonly Label _totalMoves;
        private readonly Label _totalTime;
        private readonly ButtonBinding _playAgain;
        private readonly List<LevelResult> _shownResults = new();

        public GameplayResultsView(VisualElement root, IGameplayStatus status, IGameplayCommands commands)
        {
            _status = status;
            _resultRows = UiElements.Require<VisualElement>(root, UiIds.Results.Rows);
            TemplateContainer template = UiElements.Require<TemplateContainer>(root, UiIds.Results.Template);
            _resultRowTemplate = template.templateSource
                                 ?? throw new InvalidOperationException("Gameplay result row template is missing.");
            UiElements.Require<Label>(template, UiIds.ResultRow.Level);
            UiElements.Require<Label>(template, UiIds.ResultRow.Moves);
            UiElements.Require<Label>(template, UiIds.ResultRow.Time);
            _completedStats = UiElements.Require<Label>(root, UiIds.Results.CompletedStats);
            _totalMoves = UiElements.Require<Label>(root, UiIds.Results.TotalMoves);
            _totalTime = UiElements.Require<Label>(root, UiIds.Results.TotalTime);
            Button playAgain = UiElements.Require<Button>(root, UiIds.Results.PlayAgain);

            _playAgain = new ButtonBinding(playAgain, () => commands.CanRestart, commands.RequestRestart);
        }

        public void Refresh()
        {
            _playAgain.Refresh();

            if (_status.Phase == GameplayPhase.Completed || _status.Phase == GameplayPhase.Finished)
            {
                RefreshResults();
            }
        }

        public void Dispose() =>
            _playAgain.Dispose();

        private void RefreshResults()
        {
            IReadOnlyList<LevelResult> results = _status.Results;
            if (HaveResultsChanged(results) == false)
            {
                return;
            }

            _shownResults.Clear();
            _resultRows.Clear();
            int totalMoves = 0;
            float totalSeconds = 0f;
            for (int index = 0; index < results.Count; index++)
            {
                LevelResult result = results[index];
                _shownResults.Add(result);
                _resultRows.Add(CreateRow(index, result));
                totalMoves += result.Moves;
                totalSeconds += result.ElapsedSeconds;
            }

            _totalMoves.text = totalMoves.ToString();
            _totalTime.text = $"{totalSeconds:F1} s";
            if (results.Count > 0)
            {
                LevelResult latest = results[results.Count - 1];
                _completedStats.text = $"{latest.Moves} moves · {latest.ElapsedSeconds:F1} s";
            }
        }

        private bool HaveResultsChanged(IReadOnlyList<LevelResult> results)
        {
            if (results.Count != _shownResults.Count)
            {
                return true;
            }

            for (int index = 0; index < results.Count; index++)
            {
                if (results[index].Moves != _shownResults[index].Moves
                    || results[index].ElapsedSeconds != _shownResults[index].ElapsedSeconds)
                {
                    return true;
                }
            }

            return false;
        }

        private TemplateContainer CreateRow(int index, LevelResult result)
        {
            TemplateContainer row = _resultRowTemplate.CloneTree();
            row.pickingMode = PickingMode.Ignore;
            row.AddToClassList(UiIds.Results.RowClass);
            UiElements.Require<Label>(row, UiIds.ResultRow.Level).text = (index + 1).ToString("00");
            UiElements.Require<Label>(row, UiIds.ResultRow.Moves).text = result.Moves.ToString();
            UiElements.Require<Label>(row, UiIds.ResultRow.Time).text = $"{result.ElapsedSeconds:F1} s";
            return row;
        }
    }
}
