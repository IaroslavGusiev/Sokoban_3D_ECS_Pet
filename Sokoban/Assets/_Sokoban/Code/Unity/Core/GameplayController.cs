using System;
using Zenject;
using UnityEngine;
using Sokoban.Core;
using System.Threading;
using Sokoban.Core.Commands;
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
        private readonly HintCoordinator _hints;
        private readonly BackgroundCoordinator _backgrounds;
        private readonly List<LevelResult> _results = new();
        private readonly CharacterSelection _characters = new();
        private readonly CancellationTokenSource _cancellation = new();

        private LevelRun _run;
        private LevelDefinition _level;

        private int _levelIndex;
        private SkinId _playerSkin;
        private bool _hasConfirmedCharacter;
        private bool _undoRequested;
        private float _completionTimer;
        private bool _restartRequested;
        private bool _nextSkinRequested;

        public int LevelCount { get; private set; }
        public GameplayPhase Phase { get; private set; }
        public IReadOnlyList<LevelResult> Results { get; }

        public float RemainingSeconds => _run?.RemainingSeconds ?? _staticData.GetGameplaySettings().LevelTimeLimitSeconds;
        
        public int Moves => _run?.Result.Moves ?? 0;

        public int LevelNumber => _levelIndex + 1;

        public bool CanRestart => Phase != GameplayPhase.Loading;

        internal bool CanSwitchSkin => Phase != GameplayPhase.Loading && _run is { IsPlayerDeferred: false };

        public IReadOnlyList<SkinId> Characters => _characters.Choices;
        public SkinId SelectedCharacter => _characters.Selected;
        public bool CanChooseCharacter => Phase is GameplayPhase.SelectingCharacter or GameplayPhase.CharacterFailed;
        public bool CanConfirmCharacter => CanChooseCharacter && Characters.Count > 0;
        
        public bool CanUndo => Phase == GameplayPhase.Playing && _run is { CanUndo: true };

        public bool CanNextBackground => Phase != GameplayPhase.Loading && _backgrounds.CanPreview;
        
        public BackgroundStatus BackgroundStatus => _backgrounds.Status;
        
        public string BackgroundName => _backgrounds.SelectedName;

        public bool CanHint => _hints.CanRequest(_run, Phase);
        
        public HintStatus HintStatus => _hints.Status;
        
        public MoveCommand HintMove => _hints.Move;
        
        internal GameplayController(
            IPlayerInput input,
            ILevelSource source,
            HintCoordinator hints,
            BackgroundCoordinator backgrounds,
            LevelRunFactory runFactory,
            IStaticDataProvider staticData)
        {
            _input = input;
            _hints = hints;
            _backgrounds = backgrounds;
            _source = source;
            _runFactory = runFactory;
            _staticData = staticData;
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
            try
            {
                _hints.Dispose();
                ReleaseRun();
            }
            finally
            {
                _backgrounds.Dispose();
            }
        }

        public void RequestRestart()
        {
            if (CanRestart)
            {
                _restartRequested = true;
            }
        }

        internal void RequestNextSkin()
        {
            if (CanSwitchSkin)
            {
                _nextSkinRequested = true;
            }
        }

        public void RequestCharacter(SkinId skin)
        {
            if (CanChooseCharacter)
            {
                _characters.RequestSelection(skin);
            }
        }

        public void RequestConfirmCharacter()
        {
            if (CanConfirmCharacter)
            {
                _characters.RequestConfirmation();
            }
        }

        public void RequestUndo()
        {
            if (CanUndo)
            {
                _undoRequested = true;
            }
        }
        
        public void RequestNextBackground() =>
            _backgrounds.RequestNext();

        public void RequestHint() => 
            _hints.Request(_run, Phase);

        public void Tick() => 
            Tick(Time.deltaTime);

        internal void Tick(float deltaTime)
        {
            bool restartRequested = _restartRequested;
            bool nextSkinRequested = _nextSkinRequested;
            bool undoRequested = _undoRequested;
            bool hintRequested = _hints.ConsumeRequest();
            
            _restartRequested = false;
            _nextSkinRequested = false;
            _undoRequested = false;

            if (Phase == GameplayPhase.Loading)
            {
                _backgrounds.Update();
                return;
            }

            if (Phase is GameplayPhase.SelectingCharacter or GameplayPhase.BindingCharacter or GameplayPhase.CharacterFailed)
            {
                if (restartRequested)
                {
                    Restart();
                }
                else
                {
                    TickCharacter(deltaTime);
                }

                _hints.Update(_run, Phase);
                _backgrounds.Update();
                return;
            }

            undoRequested = (undoRequested || _input.UndoPressed) && CanUndo && restartRequested == false && _input.RestartPressed == false && _input.NextLevelPressed == false;
            
            if (hintRequested && CanHint && restartRequested == false && _input.RestartPressed == false && _input.NextLevelPressed == false)
            {
                _hints.Start(_run, Phase);
            }
            
            HandleSkinInput(nextSkinRequested);
            
            if (TickState(deltaTime, undoRequested) == TickResult.ProcessCommands && Phase != GameplayPhase.Loading)
            {
                HandleCommands(restartRequested);
            }
            
            _hints.Update(_run, Phase);
            _backgrounds.Update();
        }

        private void HandleSkinInput(bool requested)
        {
            if (_run != null && requested)
            {
                _playerSkin = SkinCycle.Next(_playerSkin);
                _run.SetPlayerSkin(_playerSkin);
            }
        }

        private void TransitionTo(GameplayPhase next)
        {
            Phase = next;
            
            if (next != GameplayPhase.Playing)
            {
                _hints.Invalidate();
            }

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
                    if (Phase == GameplayPhase.SelectingCharacter)
                    {
                        return TickResult.SkipCommands;
                    }
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
                ViewStatus.Ready => _run.IsPlayerDeferred ? GameplayPhase.SelectingCharacter : GameplayPhase.Playing,
                ViewStatus.Failed => Phase == GameplayPhase.BindingViews
                                     && _run.HasPlayerFailure
                                     && _run.HasBoardFailure == false
                    ? GameplayPhase.CharacterFailed
                    : GameplayPhase.ViewsFailed,
                _ => GameplayPhase.BindingViews
            });
        }

        private void TickCharacter(float deltaTime)
        {
            if (Phase != GameplayPhase.BindingCharacter)
            {
                SkinId previous = _characters.Selected;
                if (_characters.ConsumeConfirmation(out SkinId skin))
                {
                    _playerSkin = skin;
                    _hasConfirmedCharacter = true;
                    _run.SetPlayerSkin(skin);
                    TransitionTo(GameplayPhase.BindingCharacter);
                }
                else if (Phase == GameplayPhase.CharacterFailed && skin != previous)
                {
                    TransitionTo(GameplayPhase.SelectingCharacter);
                }
            }

            _run.TickView(deltaTime);
            if (_run.HasBoardFailure)
            {
                TransitionTo(GameplayPhase.ViewsFailed);
                return;
            }

            if (Phase == GameplayPhase.BindingCharacter)
            {
                if (_run.ViewStatus == ViewStatus.Failed)
                {
                    TransitionTo(GameplayPhase.CharacterFailed);
                }
                else if (_run.ViewStatus == ViewStatus.Ready && _input.IsNeutral)
                {
                    // The selection's key/click must be released before gameplay can sample input.
                    TransitionTo(GameplayPhase.Playing);
                }
            }
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
                LoadLevelAsync(_levelIndex, _cancellation.Token, _hasConfirmedCharacter == false).Forget();
            }
            else
            {
                ReleaseRun();
                StartRun(_hasConfirmedCharacter == false);
            }
        }

        private async UniTask LoadLevelAsync(int index, CancellationToken ct, bool chooseCharacter = true)
        {
            TransitionTo(GameplayPhase.Loading);
            _backgrounds.BeginLevelEntry();
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
                
                StartRun(chooseCharacter);
                if (Phase == GameplayPhase.BindingViews)
                {
                    _backgrounds.EnterLevel();
                }
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

        private void StartRun(bool chooseCharacter)
        {
            try
            {
                _characters.Begin(_staticData.GetViewCatalog(), _playerSkin);

                GameplaySettings settings = _staticData.GetGameplaySettings();
                _run = _runFactory.Create(_level, new RunOptions(_playerSkin, settings.LevelTimeLimitSeconds, settings.MoveDurationSeconds, chooseCharacter));
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
            _characters.ClearRequests();
            _hints.Invalidate();
            
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

    }
}
