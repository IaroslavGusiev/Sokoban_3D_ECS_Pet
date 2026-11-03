using System;
using Zenject;
using UnityEngine;
using Sokoban.Core;
using System.Threading;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;

namespace Sokoban.Presentation
{
    public sealed class GameplayController : IInitializable, ITickable, IDisposable
    {
        private enum TickResult { ProcessCommands, SkipCommands }

        private readonly IPlayerInput _input;
        private readonly ILevelSource _source;
        private readonly IStaticDataProvider _staticData;
        private readonly LevelRunFactory _runFactory;
        private readonly GameplayStatus _status;
        private readonly GameplayCommandInbox _commands;
        private readonly HintCoordinator _hints;
        private readonly BackgroundCoordinator _backgrounds;
        
        private readonly List<LevelResult> _results = new();
        private readonly CharacterSelection _characters = new();
        private readonly CancellationTokenSource _cancellation = new();

        private LevelRun _run;
        private ReplaySession _replay;
        private LevelDefinition _level;
        private SimulationRecording _completedRecording;

        private RunOptions _runOptions;
        private RunOptions _completedOptions;
        private GameplayPhase _returnPhase;
        private int _levelIndex;
        private SkinId _playerSkin;
        private bool _hasConfirmedCharacter;
        private bool _choosingCharacter;
        private bool _nextSkinRequested;

        private int LevelCount { get; set; }
        private GameplayPhase Phase { get; set; }

        private bool CanRestart =>
            Phase is not (GameplayPhase.Loading or GameplayPhase.Completed)
            && CanBackFromReplay == false;

        private bool CanNextLevel =>
            Phase == GameplayPhase.Completed && _levelIndex + 1 < LevelCount;

        private bool CanWatchReplay =>
            (Phase is GameplayPhase.Completed or GameplayPhase.Finished) && _completedRecording != null;

        private bool CanToggleReplayPause =>
            Phase is GameplayPhase.Replaying or GameplayPhase.ReplayPaused;

        private bool CanBackFromReplay =>
            Phase is GameplayPhase.ReplayLoading or GameplayPhase.Replaying
            or GameplayPhase.ReplayPaused or GameplayPhase.ReplayFinished or GameplayPhase.ReplayFailed;

        internal bool CanSwitchSkin =>
            (Phase is GameplayPhase.Playing or GameplayPhase.ViewsFailed)
            && _run is { IsPlayerDeferred: false };

        private bool CanChooseCharacter =>
            _choosingCharacter && (Phase is GameplayPhase.SelectingCharacter or GameplayPhase.CharacterFailed);

        private bool CanConfirmCharacter =>
            (CanChooseCharacter || Phase == GameplayPhase.CharacterFailed) && _characters.Choices.Count > 0;

        private bool CanUndo =>
            Phase == GameplayPhase.Playing && _run is { CanUndo: true };

        private bool CanNextBackground =>
            Phase != GameplayPhase.Loading
            && CanBackFromReplay == false
            && _backgrounds.CanPreview;

        private bool CanHint =>
            _hints.CanRequest(_run, Phase);

        internal GameplayController(
            IPlayerInput input,
            ILevelSource source,
            HintCoordinator hints,
            BackgroundCoordinator backgrounds,
            LevelRunFactory runFactory,
            IStaticDataProvider staticData,
            GameplayStatus status,
            GameplayCommandInbox commands)
        {
            _input = input;
            _hints = hints;
            _source = source;
            _status = status;
            _commands = commands;
            _runFactory = runFactory;
            _staticData = staticData;
            _backgrounds = backgrounds;
            PublishStatus();
        }

        public void Tick() =>
            Tick(Time.deltaTime);

        public void Initialize()
        {
            LoadLevelAsync(0, _cancellation.Token)
                .Forget();
        }

        public void Dispose()
        {
            _commands.Close();
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
                try
                {
                    _backgrounds.Dispose();
                }
                finally
                {
                    PublishStatus();
                }
            }
        }

        internal void RequestNextSkin()
        {
            if (CanSwitchSkin)
            {
                _nextSkinRequested = true;
            }
        }

        internal void Tick(float deltaTime)
        {
            try
            {
                GameplayRequests requests = ConsumeRequests(out SkinId? requestedCharacter, out bool nextSkinRequested);
                TickApplicationPhase(deltaTime, requests, requestedCharacter, nextSkinRequested);
                _backgrounds.Update();
            }
            finally
            {
                PublishStatus();
            }
        }

        private GameplayRequests ConsumeRequests(out SkinId? requestedCharacter, out bool nextSkinRequested)
        {
            GameplayRequests requests = _commands.Consume(out requestedCharacter);
            
            if ((requests & GameplayRequests.Restart) != 0 && CanRestart == false)
            {
                requests &= ~GameplayRequests.Restart;
            }

            if ((requests & GameplayRequests.NextLevel) != 0 && CanNextLevel == false)
            {
                requests &= ~GameplayRequests.NextLevel;
            }

            if ((requests & GameplayRequests.WatchReplay) != 0 && CanWatchReplay == false)
            {
                requests &= ~GameplayRequests.WatchReplay;
            }

            if ((requests & GameplayRequests.ToggleReplayPause) != 0 && CanToggleReplayPause == false)
            {
                requests &= ~GameplayRequests.ToggleReplayPause;
            }

            if ((requests & GameplayRequests.BackFromReplay) != 0 && CanBackFromReplay == false)
            {
                requests &= ~GameplayRequests.BackFromReplay;
            }

            if ((requests & GameplayRequests.Undo) != 0 && CanUndo == false)
            {
                requests &= ~GameplayRequests.Undo;
            }

            if ((requests & GameplayRequests.Hint) != 0 && CanHint == false)
            {
                requests &= ~GameplayRequests.Hint;
            }

            if ((requests & GameplayRequests.ConfirmCharacter) != 0 && CanConfirmCharacter == false)
            {
                requests &= ~GameplayRequests.ConfirmCharacter;
            }

            nextSkinRequested = _nextSkinRequested;
            _nextSkinRequested = false;

            if (CanChooseCharacter == false)
            {
                requestedCharacter = null;
            }

            if ((requests & GameplayRequests.NextBackground) != 0 && CanNextBackground)
            {
                _backgrounds.RequestNext();
            }
            
            return requests & ~GameplayRequests.NextBackground;
        }

        private void TickApplicationPhase(float deltaTime, GameplayRequests requests,
            SkinId? requestedCharacter, bool nextSkinRequested)
        {
            if (Phase == GameplayPhase.Loading)
            {
                return;
            }

            if (CanBackFromReplay)
            {
                TickReplay(deltaTime,
                    (requests & GameplayRequests.BackFromReplay) != 0,
                    (requests & GameplayRequests.ToggleReplayPause) != 0);
                return;
            }

            if (Phase is GameplayPhase.Completed or GameplayPhase.Finished)
            {
                TickCompletionScreen(deltaTime,
                    (requests & GameplayRequests.NextLevel) != 0,
                    (requests & GameplayRequests.WatchReplay) != 0,
                    (requests & GameplayRequests.Restart) != 0);
                return;
            }

            if (Phase is GameplayPhase.SelectingCharacter or GameplayPhase.BindingCharacter or GameplayPhase.CharacterFailed)
            {
                if ((requests & GameplayRequests.Restart) != 0)
                {
                    Restart();
                }
                else
                {
                    TickCharacter(deltaTime, requestedCharacter,
                        (requests & GameplayRequests.ConfirmCharacter) != 0);
                }

                _hints.Update(_run, Phase);
                return;
            }

            TickAttempt(deltaTime, requests, nextSkinRequested);
        }

        private void TickAttempt(float deltaTime, GameplayRequests requests, bool nextSkinRequested)
        {
            bool restartRequested = (requests & GameplayRequests.Restart) != 0;
            
            bool undoRequested = ((requests & GameplayRequests.Undo) != 0 || _input.UndoPressed)
                                 && CanUndo
                                 && restartRequested == false
                                 && _input.RestartPressed == false
                                 && _input.NextLevelPressed == false;

            if ((requests & GameplayRequests.Hint) != 0
                && CanHint
                && restartRequested == false
                && _input.RestartPressed == false
                && _input.NextLevelPressed == false)
            {
                _hints.Start(_run, Phase);
            }

            HandleSkinInput(nextSkinRequested);

            if (TickAttemptPhase(deltaTime, undoRequested) == TickResult.ProcessCommands && Phase != GameplayPhase.Loading)
            {
                HandleAttemptCommands(restartRequested);
            }

            _hints.Update(_run, Phase);
        }

        private void PublishStatus()
        {
            bool stopped = _cancellation.IsCancellationRequested;
            
            GameplayAvailability availability = stopped
                ? default
                : new GameplayAvailability(CanRestart, CanNextLevel, CanWatchReplay, CanToggleReplayPause,
                    CanBackFromReplay, CanChooseCharacter, CanConfirmCharacter, CanUndo, CanHint, CanNextBackground);
          
            float remaining = stopped
                ? _status.RemainingSeconds
                : _run?.RemainingSeconds
                  ?? (LevelCount > 0 
                      ? _staticData.GetGameplaySettings().LevelTimeLimitSeconds 
                      : 0f);
            
            var snapshot = new GameplayStatusSnapshot(
                phase: Phase,
                levelCount: LevelCount,
                levelNumber: _levelIndex + 1,
                moves: stopped ? 0 : _run?.Result.Moves ?? 0,
                remainingSeconds: remaining,
                selectedCharacter: _choosingCharacter ? _characters.Selected : _playerSkin,
                hintStatus: _hints.Status,
                hintMove: _hints.Move,
                backgroundStatus: _backgrounds.Status,
                backgroundName: stopped ? string.Empty : _backgrounds.SelectedName,
                availability: availability);
            
            _status.Publish(snapshot, _characters.Choices, _results);
            _commands.AcceptPublication(availability);
        }

        private void TickReplay(float deltaTime, bool backFromReplayRequested, bool pauseReplayRequested)
        {
            if (backFromReplayRequested)
            {
                ReleaseReplay();
                
                _run.SetVisible(true);
                _run.TickView(0f);
                
                TransitionTo(_returnPhase);
                return;
            }

            if (pauseReplayRequested)
            {
                _replay?.TogglePause();
            }

            _replay?.Tick(deltaTime);
            
            if (_replay != null)
            {
                GameplayPhase nextPhase = _replay.Status switch
                {
                    ReplayStatus.Binding => GameplayPhase.ReplayLoading,
                    ReplayStatus.Playing => GameplayPhase.Replaying,
                    ReplayStatus.Paused => GameplayPhase.ReplayPaused,
                    ReplayStatus.Finished => GameplayPhase.ReplayFinished,
                    ReplayStatus.Failed => GameplayPhase.ReplayFailed,
                    _ => throw new InvalidOperationException("Unsupported replay status.")
                };

                TransitionTo(nextPhase);
            }
        }

        private void TickCompletionScreen(float deltaTime, bool nextLevelRequested, bool watchReplayRequested, bool restartRequested)
        {
            _run.TickView(deltaTime);
            
            if (nextLevelRequested && CanNextLevel)
            {
                LoadLevelAsync(_levelIndex + 1, _cancellation.Token).Forget();
            }
            else if (watchReplayRequested && CanWatchReplay)
            {
                BeginReplay();
            }
            else if (Phase == GameplayPhase.Finished && (restartRequested || _input.RestartPressed))
            {
                _results.Clear();
                _hasConfirmedCharacter = false;
                LoadLevelAsync(0, _cancellation.Token).Forget();
            }
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
        }

        private TickResult TickAttemptPhase(float deltaTime, bool undoRequested)
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
            
            GameplayPhase nextPhase = _run.ViewStatus switch
            {
                ViewStatus.Ready when _run.IsPlayerDeferred =>
                    GameplayPhase.SelectingCharacter,

                ViewStatus.Ready when _input.IsNeutral =>
                    GameplayPhase.Playing,

                ViewStatus.Failed when Phase == GameplayPhase.BindingViews
                                      && _run.HasPlayerFailure
                                      && _run.HasBoardFailure == false =>
                    GameplayPhase.CharacterFailed,

                ViewStatus.Failed =>
                    GameplayPhase.ViewsFailed,

                _ =>
                    GameplayPhase.BindingViews
            };

            TransitionTo(nextPhase);
        }

        private void TickCharacter(float deltaTime, SkinId? requestedCharacter, bool confirmCharacterRequested)
        {
            if (Phase != GameplayPhase.BindingCharacter)
            {
                SkinId previous = _characters.Selected;
                
                if (requestedCharacter.HasValue && CanChooseCharacter)
                {
                    _characters.Select(requestedCharacter.Value);
                }

                SkinId skin = _characters.Selected;
                
                if (confirmCharacterRequested && CanConfirmCharacter)
                {
                    if (_choosingCharacter)
                    {
                        _playerSkin = skin;
                        _hasConfirmedCharacter = true;
                    }
                    else
                    {
                        skin = _playerSkin;
                        
                        if (HasConfirmedCatalogEntry() == false)
                        {
                            _run.TickView(deltaTime);
                            return;
                        }
                    }
                    
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

            if (Phase != GameplayPhase.BindingCharacter)
            {
                return;
            }
            
            switch (_run.ViewStatus)
            {
                case ViewStatus.Failed:
                    TransitionTo(GameplayPhase.CharacterFailed);
                    break;
                    
                case ViewStatus.Ready when _input.IsNeutral:
                    TransitionTo(GameplayPhase.Playing);
                    break;
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
                CompleteAttempt();
                return TickResult.SkipCommands;
            }

            return TickResult.ProcessCommands;
        }

        private void CompleteAttempt()
        {
            _hints.Invalidate();
            
            _completedRecording = _run.ExportRecording();
            _completedOptions = new RunOptions(_playerSkin, _completedRecording.TimeLimitSeconds,
                _runOptions.MoveDurationSeconds);
            
            _results.Add(_run.Result);

            GameplayPhase nextPhase = _levelIndex + 1 == LevelCount
                ? GameplayPhase.Finished
                : GameplayPhase.Completed;

            TransitionTo(nextPhase);
        }

        private void HandleAttemptCommands(bool restartRequested)
        {
            bool restart = restartRequested || _input.RestartPressed;

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
                StartRun(_hasConfirmedCharacter == false);
            }
        }

        private async UniTask LoadLevelAsync(int index, CancellationToken ct)
        {
            _commands.BeginScope();
            
            TransitionTo(GameplayPhase.Loading);
            
            _backgrounds.BeginLevelEntry();
            _levelIndex = index;
            
            PublishStatus();

            try
            {
                await _staticData.LoadAsync(ct);

                if (ct.IsCancellationRequested)
                {
                    return;
                }

                LevelCount = _source.Count;
                PublishStatus();
                
                LevelDefinition next = await _source.LoadAsync(index, ct);

                if (ct.IsCancellationRequested)
                {
                    return;
                }

                ReleaseRun();
                _level = next;

                StartRun(_hasConfirmedCharacter == false);
                
                if (Phase is GameplayPhase.BindingViews or GameplayPhase.CharacterFailed)
                {
                    _backgrounds.EnterLevel();
                }
            }
            catch (OperationCanceledException)
            {
                // Shutdown cancellation is expected; disposal publishes the final status.
            }
            catch (Exception exception)
            {
                TransitionTo(GameplayPhase.LevelFailed);
                Debug.LogException(exception);
            }
            finally
            {
                if (ct.IsCancellationRequested == false)
                {
                    PublishStatus();
                }
            }
        }

        private void StartRun(bool chooseCharacter)
        {
            bool missingCharacter;
            
            try
            {
                _choosingCharacter = chooseCharacter;
                if (chooseCharacter)
                {
                    _characters.Begin(_staticData.GetViewCatalog(), _playerSkin);
                }

                missingCharacter = chooseCharacter == false && HasConfirmedCatalogEntry() == false;
                GameplaySettings settings = _staticData.GetGameplaySettings();
                
                _runOptions = new RunOptions(_playerSkin, settings.LevelTimeLimitSeconds,
                    settings.MoveDurationSeconds, chooseCharacter || missingCharacter);
                _run = _runFactory.Create(_level, _runOptions);
            }
            catch (Exception exception)
            {
                TransitionTo(GameplayPhase.LevelFailed);
                Debug.LogException(exception);
                return;
            }

            GameplayPhase phase = missingCharacter
                ? GameplayPhase.CharacterFailed
                : GameplayPhase.BindingViews;

            TransitionTo(phase);
        }

        private bool HasConfirmedCatalogEntry()
        {
            ViewCatalog.SkinEntry entry = _staticData
                .GetViewCatalog()
                .Resolve(new ViewKey(ViewKind.Player, _playerSkin));
            
            return entry.Skin == _playerSkin && string.IsNullOrWhiteSpace(entry.Address) == false;
        }

        private void BeginReplay()
        {
            _commands.BeginScope();
            _returnPhase = Phase;
            _run.SetVisible(false);
            TransitionTo(GameplayPhase.ReplayLoading);
            
            try
            {
                if (HasConfirmedCatalogEntry() == false)
                {
                    throw new InvalidOperationException("The recorded character is unavailable in the current catalog.");
                }
                
                _replay = _runFactory.CreateReplay(_completedRecording, _completedOptions);
            }
            catch (Exception exception)
            {
                TransitionTo(GameplayPhase.ReplayFailed);
                Debug.LogException(exception);
            }
        }

        private void ReleaseReplay()
        {
            _commands.BeginScope();
            ReplaySession replay = _replay;
            _replay = null;
            replay?.Dispose();
        }

        private void ReleaseRun()
        {
            _commands.BeginScope();
            _hints.Invalidate();

            try
            {
                try
                {
                    ReleaseReplay();
                }
                finally
                {
                    _completedRecording = null;
                    _run?.Dispose();
                }
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
