using System;
using Zenject;
using UnityEngine;
using Sokoban.Core;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Sokoban.Presentation
{
    public sealed class GameplayController : IInitializable, ITickable, IDisposable
    {
        private enum State { Loading, BindingViews, Playing, Completed, ViewsFailed, LevelFailed }

        private const float CompletionDelaySeconds = 0.75f;

        private readonly LevelRoot _root;
        private readonly IPlayerInput _input;
        private readonly ILevelSource _source;
        private readonly ViewFeatures _viewFeatures;
        private readonly IViewProvider _provider;
        private readonly CancellationTokenSource _cancellation = new();
        private LevelDefinition _level;
        private LevelRun _run;
        private State _state;
        private int _levelIndex;
        private float _completionTimer;
        private SkinId _playerSkin;

        public GameplayController(
            IPlayerInput input, 
            ILevelSource source,
            ViewFeatures viewFeatures, 
            LevelRoot root,
            IViewProvider provider)
        {
            _root = root;
            _input = input;
            _source = source;
            _viewFeatures = viewFeatures;
            _provider = provider;
        }

        public void Initialize() => 
            LoadLevelAsync(0, _cancellation.Token).Forget();

        public void Dispose()
        {
            _state = State.Loading;
            _cancellation.Cancel();
            _cancellation.Dispose();
            ReleaseRun();
        }

        public void Tick()
        {
            if (_state == State.Loading)
            {
                return;
            }

            HandleSkinInput();
            
            if (TickState(Time.deltaTime) && _state != State.Loading)
            {
                HandleCommands();
            }
        }

        private void HandleSkinInput()
        {
            if (_run != null && _input.NextSkinPressed)
            {
                _playerSkin = SkinCycle.Next(_playerSkin);
                _run.SetPlayerSkin(_playerSkin);
            }
        }

        private bool TickState(float deltaTime)
        {
            switch (_state)
            {
                case State.BindingViews:
                case State.ViewsFailed:
                    TickBinding(deltaTime);
                    break;
                
                case State.Playing:
                    TickPlaying(deltaTime);
                    break;
                
                case State.Completed:
                    return TickCompleted(deltaTime);
                
                case State.LevelFailed:
                    TickLevelFailed(deltaTime);
                    break;
            }

            return true;
        }

        private void TickBinding(float deltaTime)
        {
            _run.Tick(deltaTime);
            
            _state = _run.ViewStatus switch
            {
                ViewStatus.Ready => State.Playing,
                ViewStatus.Failed => State.ViewsFailed,
                _ => State.BindingViews
            };
        }

        private void TickPlaying(float deltaTime)
        {
            _run.Send(_input.ReadMove());
            _run.Tick(deltaTime);
            
            if (_run.ViewStatus == ViewStatus.Failed)
            {
                _state = State.ViewsFailed;
            }
            else if (_run.IsCompleted)
            {
                _completionTimer = 0f;
                _state = State.Completed;
            }
        }

        private bool TickCompleted(float deltaTime)
        {
            _run.Tick(deltaTime);
            _completionTimer += deltaTime;
            
            if (_completionTimer >= CompletionDelaySeconds)
            {
                LoadLevelAsync((_levelIndex + 1) % _source.Count, _cancellation.Token).Forget();
                return false;
            }

            return true;
        }

        private void HandleCommands()
        {
            if (_input.NextLevelPressed)
            {
                LoadLevelAsync((_levelIndex + 1) % _source.Count, _cancellation.Token).Forget();
            }
            else if (_input.RestartPressed)
            {
                Restart();
            }
        }

        private void Restart()
        {
            if (_state == State.LevelFailed)
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
            _state = State.Loading;
            _levelIndex = index;

            try
            {
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
                _state = State.LevelFailed;
                Debug.LogException(exception);
            }
        }

        private void StartRun()
        {
            try
            {
                _run = new LevelRun(_root, _level, _provider, _viewFeatures, _playerSkin);
            }
            catch (Exception exception)
            {
                _state = State.LevelFailed;
                Debug.LogException(exception);
                return;
            }

            _state = State.BindingViews;
        }

        private void ReleaseRun()
        {
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
            _run?.Tick(deltaTime);
    }
}
