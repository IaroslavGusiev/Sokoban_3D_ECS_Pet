using System;
using Sokoban.Core;
using UnityEngine;

namespace Sokoban.Presentation
{
    internal sealed class ReplaySession : IDisposable
    {
        private const int MaxInputsPerUpdate = 256;

        private readonly SimulationRecording _recording;

        private SimulationReplay _replay;
        private ViewLayer _views;

        private int _nextInput;
        private double _credit;
        private double _untilTick;
        private bool _waitingForTick;
        private bool _disposed;

        public ReplayStatus Status { get; private set; } = ReplayStatus.Binding;

        public ReplaySession(
            SimulationRecording recording, 
            LevelRoot root, 
            IViewProvider provider, 
            ViewPipelineFactory pipelineFactory, 
            RunOptions options)
        {
            _recording = recording;
            try
            {
                _replay = new SimulationReplay(recording);
                _views = new ViewLayer(root, _replay.Simulation.World, provider, pipelineFactory, options);
                _views.Tick(0f);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Tick(float deltaTime)
        {
            if (_disposed || Status is ReplayStatus.Failed or ReplayStatus.Paused)
            {
                return;
            }

            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            }

            try
            {
                if (Status == ReplayStatus.Binding)
                {
                    _views.Tick(0f);

                    if (_views.Status == ViewStatus.Failed)
                    {
                        throw new InvalidOperationException("Replay views could not be loaded.");
                    }

                    if (_views.Status == ViewStatus.Ready)
                    {
                        Status = ReplayStatus.Playing;
                    }

                    // Loading time never becomes playback credit.
                    return;
                }

                if (Status == ReplayStatus.Finished)
                {
                    _views.Tick(deltaTime);
                    return;
                }

                _credit += deltaTime;
                Advance();
            }
            catch (Exception exception)
            {
                Status = ReplayStatus.Failed;
                try
                {
                    ReleasePlayback();
                }
                finally
                {
                    Debug.LogException(exception);
                }
            }
        }

        public void TogglePause()
        {
            if (_disposed || Status is not (ReplayStatus.Playing or ReplayStatus.Paused))
            {
                return;
            }

            bool paused = Status == ReplayStatus.Playing;
            _views.SetAnimationPaused(paused);
            Status = paused ? ReplayStatus.Paused : ReplayStatus.Playing;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            ReleasePlayback();
        }

        private void Advance()
        {
            int budget = MaxInputsPerUpdate;

            while (_replay.IsFinished == false && budget > 0)
            {
                SimulationInput input = _recording.Inputs[_nextInput];

                if (input.Kind == SimulationInputKind.Tick)
                {
                    if (_waitingForTick == false)
                    {
                        _untilTick = input.DeltaTime;
                        _waitingForTick = true;
                    }

                    if (_credit < _untilTick)
                    {
                        _views.Tick((float)_credit);
                        _untilTick -= _credit;
                        _credit = 0d;
                        return;
                    }

                    _views.Tick((float)_untilTick);
                    _credit -= _untilTick;
                    _waitingForTick = false;
                }

                // Step consumes one entry. It must receive its own recorded delta, even zero.
                // It can dispose the world on failure; the catch must not inspect that world.
                _replay.Step();
                _nextInput++;
                budget--;
                if (input.Kind == SimulationInputKind.Tick)
                {
                    _views.Tick(0f);
                }
            }

            if (_replay.IsFinished)
            {
                Status = ReplayStatus.Finished;
                _views.Tick((float)_credit);
                _credit = 0d;
            }

            // A slow frame retains its remaining credit instead of dropping or merging inputs.
        }

        private void ReleasePlayback()
        {
            ViewLayer views = _views;
            _views = null;
            try
            {
                views?.Dispose();
            }
            finally
            {
                SimulationReplay replay = _replay;
                _replay = null;
                replay?.Dispose();
            }
        }
    }
}
