using System;
using System.Linq;
using UnityEngine;
using Sokoban.Core;
using UnityEngine.UIElements;
using System.Collections.Generic;

namespace Sokoban.Presentation
{
    internal sealed class GameplayResultsView : IDisposable
    {
        private readonly Label _totalTime;
        private readonly Label _totalMoves;
        private readonly Label _completedStats;
        private readonly IGameplayStatus _status;
        private readonly ButtonBinding _playAgain;
        private readonly VisualElement _resultRows;
        private readonly VisualTreeAsset _resultRowTemplate;
        private readonly List<LevelResult> _shownResults = new();

        public GameplayResultsView(VisualElement root, IGameplayStatus status, IGameplayCommands commands)
        {
            _status = status;
            _resultRows = UiElements.Require<VisualElement>(root, UiIds.Results.Rows);
            
            var template = UiElements.Require<TemplateContainer>(root, UiIds.Results.Template);
            
            _resultRowTemplate = template.templateSource ?? throw new InvalidOperationException("Gameplay result row template is missing.");
            
            UiElements.Require<Label>(template, UiIds.ResultRow.Level);
            UiElements.Require<Label>(template, UiIds.ResultRow.Moves);
            UiElements.Require<Label>(template, UiIds.ResultRow.Time);
            
            _completedStats = UiElements.Require<Label>(root, UiIds.Results.CompletedStats);
            _totalMoves = UiElements.Require<Label>(root, UiIds.Results.TotalMoves);
            _totalTime = UiElements.Require<Label>(root, UiIds.Results.TotalTime);
            
            var playAgain = UiElements.Require<Button>(root, UiIds.Results.PlayAgain);

            _playAgain = new ButtonBinding(playAgain, () => commands.CanRestart, commands.RequestRestart);
        }

        public void Refresh()
        {
            _playAgain.Refresh();

            if (_status.Phase is GameplayPhase.Completed or GameplayPhase.Finished)
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
            
            var totalMoves = 0;
            var totalSeconds = 0f;
            
            for (var index = 0; index < results.Count; index++)
            {
                LevelResult result = results[index];
                
                _shownResults.Add(result);
                _resultRows.Add(CreateRow(index, result));
                
                totalMoves += result.Moves;
                totalSeconds += result.ElapsedSeconds;
            }

            _totalMoves.text = totalMoves.ToString();
            _totalTime.text = $"{totalSeconds:F1} s";

            if (results.Count <= 0)
            {
                return;
            }
            
            LevelResult latest = results[^1];
            _completedStats.text = $"{latest.Moves} moves · {latest.ElapsedSeconds:F1} s";
        }

        private bool HaveResultsChanged(IReadOnlyList<LevelResult> results)
        {
            if (results.Count != _shownResults.Count)
            {
                return true;
            }

            return results
                .Where((t, index) => t.Moves != _shownResults[index].Moves || Mathf.Approximately(t.ElapsedSeconds, _shownResults[index].ElapsedSeconds) == false)
                .Any();
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
