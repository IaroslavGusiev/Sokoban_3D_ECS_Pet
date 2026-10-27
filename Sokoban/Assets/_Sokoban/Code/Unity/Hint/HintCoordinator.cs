using System;
using UnityEngine;
using Sokoban.Core;
using Sokoban.Core.Commands;

namespace Sokoban.Presentation
{
    internal sealed class HintCoordinator : IDisposable
    {
        private readonly IViewProvider _views;
        private readonly IHintService _service;

        private LevelRun _run;
        private HintOperation _operation;

        private long _revision;
        private bool _disposed;
        private bool _requested;

        public HintStatus Status { get; private set; }
        public MoveCommand Move { get; private set; }

        public HintCoordinator(IHintService service, IViewProvider views)
        {
            _views = views;
            _service = service;
        }

        public bool CanRequest(LevelRun run, GameplayPhase phase)
        {
            return _disposed == false 
                   && phase == GameplayPhase.Playing 
                   && run is { CanHint: true }
                   && _operation == null && _requested == false;
        }

        public void Request(LevelRun run, GameplayPhase phase)
        {
            if (CanRequest(run, phase))
            {
                _requested = true;
            }
        }

        public bool ConsumeRequest()
        {
            bool requested = _requested;
            _requested = false;
            return requested;
        }

        public void Start(LevelRun run, GameplayPhase phase)
        {
            if (CanRequest(run, phase) == false)
            {
                return;
            }
            
            Invalidate()
                ;
            _run = run;
            _revision = run.BoardRevision;
            _operation = new HintOperation(_service, run.CaptureHintBoard());
            Status = HintStatus.Calculating;
        }

        public void Update(LevelRun run, GameplayPhase phase)
        {
            if (_run == null)
            {
                return;
            }
            
            if (IsCurrent(run, phase) == false)
            {
                Invalidate();
                return;
            }
            
            HintOperation request = _operation;
            
            if (request?.ArrowTask != null)
            {
                ApplyArrow(request, run, phase);
                return;
            }
            
            if (request == null || request.Task.IsCompleted == false)
            {
                return;
            }
            
            HintOperation.Completion completion = request.Task
                .GetAwaiter()
                .GetResult();
            
            if (ReferenceEquals(request, _operation) == false || IsCurrent(run, phase) == false)
            {
                return;
            }
            
            if (completion.IsCanceled)
            {
                Invalidate();
                return;
            }
            
            if (completion.Error != null)
            {
                _operation = null;
                Status = HintStatus.Failed;
                Debug.LogException(completion.Error);
                return;
            }
            
            Status = completion.Result.Outcome switch
            {
                HintOutcome.Found => HintStatus.Available,
                HintOutcome.NoSolution => HintStatus.NoSolution,
                HintOutcome.BudgetExhausted => HintStatus.BudgetExhausted,
                HintOutcome.AlreadySolved => HintStatus.AlreadySolved,
                _ => HintStatus.Failed
            };
            
            if (Status == HintStatus.Available)
            {
                Status = HintStatus.Calculating;
                
                if (run.HasHintArrow)
                {
                    ShowArrow(run, null, completion.Result.Move);
                }
                else
                {
                    request.LoadArrow(_views);
                    ApplyArrow(request, run, phase);
                }
            }
            else _operation = null;
        }
        
        public void Invalidate()
        {
            _requested = false;
            _operation?.Cancel();
            _operation = null;
            _run?.HideHint();
            _run = null;
            Status = HintStatus.None;
            Move = default;
        }

        public void Dispose()
        {
            Invalidate();
            _disposed = true;
        }

        private bool IsCurrent(LevelRun run, GameplayPhase phase)
        {
            return _disposed == false 
                   && phase == GameplayPhase.Playing 
                   && ReferenceEquals(_run, run) 
                   && run is { CanHint: true } 
                   && run.BoardRevision == _revision;
        }

        private void ApplyArrow(HintOperation request, LevelRun run, GameplayPhase phase)
        {
            if (request.ArrowTask.IsCompleted == false)
            {
                return;
            }
            
            HintOperation.ArrowCompletion completion = request.ArrowTask
                .GetAwaiter()
                .GetResult();
            
            if (ReferenceEquals(request, _operation) == false || IsCurrent(run, phase) == false)
            {
                return;
            }
            
            if (completion.IsCanceled)
            {
                Invalidate();
                return;
            }
            
            if (completion.Error != null)
            {
                _operation = null;
                Status = HintStatus.Failed;
                return;
            }
            
            ShowArrow(run, completion.Prefab, request.Task.GetAwaiter().GetResult().Result.Move);
        }

        private void ShowArrow(LevelRun run, GameObject prefab, MoveCommand move)
        {
            _operation = null;
            
            try
            {
                run.ShowHint(prefab, move);
                Move = move;
                Status = HintStatus.Available;
            }
            catch (Exception exception)
            {
                Status = HintStatus.Failed;
                run.HideHint();
                Debug.LogException(exception);
            }
        }

       
    }
}
