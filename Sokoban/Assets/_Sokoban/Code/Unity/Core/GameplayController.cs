using System;
using Zenject;
using UnityEngine;
using Sokoban.Core;
using Sokoban.Core.Commands;
using System.Threading;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;

namespace Sokoban.Presentation
{
    public sealed class GameplayController : IInitializable, ITickable, IDisposable, IGameplayStatus, IGameplayCommands
    {
        private enum TickResult { ProcessCommands, SkipCommands }

        private readonly IPlayerInput _input;
        private readonly ILevelSource _source;
        private readonly LevelRunFactory _runFactory;
        private readonly IStaticDataProvider _staticData;
        private readonly IHintService _hintService;
        private readonly List<LevelResult> _results = new();
        private readonly CancellationTokenSource _cancellation = new();

        private LevelRun _run;
        private LevelDefinition _level;

        private int _levelIndex;
        private SkinId _playerSkin;
        private float _completionTimer;
        private bool _restartRequested;
        private bool _nextSkinRequested;
        private bool _undoRequested;
        private bool _hintRequested;
        private HintCalculation _hintCalculation;
        private LevelRun _hintRun;
        private long _hintRevision;
        
        public int LevelCount { get; private set; }
        public GameplayPhase Phase { get; private set; }
        public IReadOnlyList<LevelResult> Results { get; }

        public float RemainingSeconds => _run?.RemainingSeconds ?? _staticData.GetGameplaySettings().LevelTimeLimitSeconds;
        
        public int Moves => _run?.Result.Moves ?? 0;

        public int LevelNumber => _levelIndex + 1;

        public bool CanRestart => Phase != GameplayPhase.Loading;

        public bool CanSwitchSkin => Phase != GameplayPhase.Loading && _run != null;

        public bool CanUndo => Phase == GameplayPhase.Playing && _run != null && _run.CanUndo;

        public bool CanHint => Phase == GameplayPhase.Playing && _run != null && _run.CanHint
            && _hintCalculation == null && _hintRequested == false;
        public HintStatus HintStatus { get; private set; }
        public MoveCommand HintMove { get; private set; }

        [Inject]
        internal GameplayController(
            IPlayerInput input,
            ILevelSource source,
            LevelRunFactory runFactory,
            IStaticDataProvider staticData,
            IHintService hintService)
        {
            _input = input;
            _source = source;
            _runFactory = runFactory;
            _staticData = staticData;
            _hintService = hintService;
            Results = _results.AsReadOnly();
        }

        public void Initialize()
        {
            LoadLevelAsync(0, _cancellation.Token)
                .Forget();
        }

        public void Dispose()
        {
            TransitionTo(GameplayPhase.Loading);
            _cancellation.Cancel();
            _cancellation.Dispose();
            ReleaseRun();
        }

        public void RequestRestart()
        {
            if (CanRestart)
            {
                _restartRequested = true;
            }
        }

        public void RequestNextSkin()
        {
            if (CanSwitchSkin)
            {
                _nextSkinRequested = true;
            }
        }

        public void RequestUndo()
        {
            if (CanUndo)
            {
                _undoRequested = true;
            }
        }

        public void RequestHint()
        {
            if (CanHint) _hintRequested = true;
        }

        public void Tick() => Tick(Time.deltaTime);

        internal void Tick(float deltaTime)
        {
            bool restartRequested = _restartRequested;
            bool nextSkinRequested = _nextSkinRequested;
            bool undoRequested = _undoRequested;
            bool hintRequested = _hintRequested;
            _restartRequested = false;
            _nextSkinRequested = false;
            _undoRequested = false;
            _hintRequested = false;

            if (Phase == GameplayPhase.Loading)
            {
                return;
            }

            undoRequested = (undoRequested || _input.UndoPressed) && CanUndo
                && restartRequested == false && _input.RestartPressed == false && _input.NextLevelPressed == false;
            if (hintRequested && CanHint && restartRequested == false && _input.RestartPressed == false && _input.NextLevelPressed == false)
                StartHint();
            HandleSkinInput(nextSkinRequested);
            
            if (TickState(deltaTime, undoRequested) == TickResult.ProcessCommands && Phase != GameplayPhase.Loading)
            {
                HandleCommands(restartRequested);
            }
            UpdateHint();
        }

        private void HandleSkinInput(bool requested)
        {
            if (_run != null && (requested || _input.NextSkinPressed))
            {
                _playerSkin = SkinCycle.Next(_playerSkin);
                _run.SetPlayerSkin(_playerSkin);
            }
        }

        private void TransitionTo(GameplayPhase next)
        {
            Phase = next;
            if (next != GameplayPhase.Playing) InvalidateHint();

            if (next == GameplayPhase.Completed)
            {
                _completionTimer = 0f;
                _results.Add(_run.Result);
            }
        }

        private TickResult TickState(float deltaTime, bool undoRequested)
        {
            switch (Phase)
            {
                case GameplayPhase.Loading:
                    return TickResult.SkipCommands;

                case GameplayPhase.BindingViews:
                case GameplayPhase.ViewsFailed:
                    TickBinding(deltaTime);
                    break;
                
                case GameplayPhase.Playing:
                    return TickPlaying(deltaTime, undoRequested);
                
                case GameplayPhase.Completed:
                    return TickCompleted(deltaTime);
                
                case GameplayPhase.LevelFailed:
                    TickLevelFailed(deltaTime);
                    break;

                case GameplayPhase.Finished:
                    _run.TickView(deltaTime);
                    break;
            }

            return TickResult.ProcessCommands;
        }

        private void TickBinding(float deltaTime)
        {
            _run.TickView(deltaTime);
            
            TransitionTo(_run.ViewStatus switch
            {
                ViewStatus.Ready => GameplayPhase.Playing,
                ViewStatus.Failed => GameplayPhase.ViewsFailed,
                _ => GameplayPhase.BindingViews
            });
        }

        private TickResult TickPlaying(float deltaTime, bool undoRequested)
        {
            if (undoRequested == false || _run.RequestUndo() == false)
            {
                _run.Send(_input.ReadMove());
            }
            _run.Tick(deltaTime);
            
            if (_run.IsTimedOut)
            {
                Restart();
                return TickResult.SkipCommands;
            }

            if (_run.ViewStatus == ViewStatus.Failed)
            {
                TransitionTo(GameplayPhase.ViewsFailed);
            }
            else if (_run.IsCompleted)
            {
                TransitionTo(GameplayPhase.Completed);
            }

            return TickResult.ProcessCommands;
        }

        private TickResult TickCompleted(float deltaTime)
        {
            _run.TickView(deltaTime);
            _completionTimer += deltaTime;
            
            if (_completionTimer >= _staticData.GetGameplaySettings().CompletionDelaySeconds)
            {
                if (_levelIndex + 1 < LevelCount)
                {
                    LoadLevelAsync(_levelIndex + 1, _cancellation.Token)
                        .Forget();
                }
                else
                {
                    TransitionTo(GameplayPhase.Finished);
                }

                return TickResult.SkipCommands;
            }

            return TickResult.ProcessCommands;
        }

        private void HandleCommands(bool requested)
        {
            bool restart = requested || _input.RestartPressed;

            if (Phase == GameplayPhase.Finished)
            {
                if (restart)
                {
                    _results.Clear();
                    
                    LoadLevelAsync(0, _cancellation.Token)
                        .Forget();
                }
                return;
            }

            if (_input.NextLevelPressed)
            {
                LoadLevelAsync((_levelIndex + 1) % _source.Count, _cancellation.Token)
                    .Forget();
            }
            else if (restart)
            {
                Restart();
            }
        }

        private void Restart()
        {
            if (Phase == GameplayPhase.LevelFailed)
            {
                LoadLevelAsync(_levelIndex, _cancellation.Token).Forget();
            }
            else
            {
                ReleaseRun();
                StartRun();
            }
        }

        private async UniTask LoadLevelAsync(int index, CancellationToken ct)
        {
            TransitionTo(GameplayPhase.Loading);
            _levelIndex = index;

            try
            {
                await _staticData.LoadAsync(ct);
                
                if (ct.IsCancellationRequested)
                {
                    return;
                }
                
                LevelCount = _source.Count;
                LevelDefinition next = await _source.LoadAsync(index, ct);
                
                if (ct.IsCancellationRequested)
                {
                    return;
                }

                ReleaseRun();
                _level = next;
                
                StartRun();
            }
            catch (OperationCanceledException)
            {
                // ignored
            }
            catch (Exception exception)
            {
                TransitionTo(GameplayPhase.LevelFailed);
                Debug.LogException(exception);
            }
        }

        private void StartRun()
        {
            try
            {
                GameplaySettings settings = _staticData.GetGameplaySettings();
                _run = _runFactory.Create(_level, new RunOptions(_playerSkin, settings.LevelTimeLimitSeconds, settings.MoveDurationSeconds));
            }
            catch (Exception exception)
            {
                TransitionTo(GameplayPhase.LevelFailed);
                Debug.LogException(exception);
                return;
            }

            TransitionTo(GameplayPhase.BindingViews);
        }

        private void ReleaseRun()
        {
            InvalidateHint();
            try
            {
                _run?.Dispose();
            }
            finally
            {
                _run = null;
            }
        }

        private void TickLevelFailed(float deltaTime) => 
            _run?.TickView(deltaTime);

        private void StartHint()
        {
            InvalidateHint();
            _hintRun = _run;
            _hintRevision = _run.BoardRevision;
            _hintCalculation = new HintCalculation(_hintService, _run.CaptureHintBoard());
            HintStatus = HintStatus.Calculating;
        }

        private bool IsHintCurrent() =>
            Phase == GameplayPhase.Playing && ReferenceEquals(_hintRun, _run) && _run != null
            && _run.CanHint && _run.BoardRevision == _hintRevision;

        private void UpdateHint()
        {
            if (_hintRun == null) return;
            if (IsHintCurrent() == false)
            {
                InvalidateHint();
                return;
            }
            HintCalculation request = _hintCalculation;
            if (request == null || request.Task.IsCompleted == false) return;
            HintCalculation.Completion completion = request.Task.GetAwaiter().GetResult();
            if (ReferenceEquals(request, _hintCalculation) == false || IsHintCurrent() == false) return;
            _hintCalculation = null;
            if (completion.IsCanceled)
            {
                InvalidateHint();
                return;
            }
            if (completion.Error != null)
            {
                HintStatus = HintStatus.Failed;
                Debug.LogException(completion.Error);
                return;
            }
            HintStatus = completion.Result.Outcome switch
            {
                HintOutcome.Found => HintStatus.Available,
                HintOutcome.NoSolution => HintStatus.NoSolution,
                HintOutcome.BudgetExhausted => HintStatus.BudgetExhausted,
                HintOutcome.AlreadySolved => HintStatus.AlreadySolved,
                _ => HintStatus.Failed
            };
            if (HintStatus == HintStatus.Available)
            {
                try
                {
                    _run.ShowHint(completion.Result.Move);
                    HintMove = completion.Result.Move;
                }
                catch (Exception exception)
                {
                    HintStatus = HintStatus.Failed;
                    _run.HideHint();
                    Debug.LogException(exception);
                }
            }
        }

        private void InvalidateHint()
        {
            _hintRequested = false;
            _hintCalculation?.Cancel();
            _hintCalculation = null;
            _hintRun = null;
            HintStatus = HintStatus.None;
            HintMove = default;
            _run?.HideHint();
        }
    }
}
