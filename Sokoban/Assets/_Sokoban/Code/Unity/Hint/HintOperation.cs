using System;
using UnityEngine;
using Sokoban.Core;
using System.Threading;
using System.Threading.Tasks;

namespace Sokoban.Presentation
{
    internal sealed class HintOperation
    {
        private readonly CancellationOwner _cancellation = new();
        private CancellationOwner _arrowCancellation;

        public Task<Completion> Task { get; }
        public Task<ArrowCompletion> ArrowTask { get; private set; }

        public HintOperation(IHintService service, LevelDefinition board) =>
            Task = ObserveAsync(service, board, _cancellation);

        public void Cancel()
        {
            _cancellation.Cancel();
            _arrowCancellation?.Cancel();
        }

        public void LoadArrow(IViewProvider provider)
        {
            _arrowCancellation = new CancellationOwner();
            ArrowTask = ObserveArrowAsync(provider, _arrowCancellation);
        }

        private static async Task<ArrowCompletion> ObserveArrowAsync(IViewProvider provider, CancellationOwner cancellation)
        {
            try
            {
                GameObject prefab = await provider.LoadAsync(new ViewKey(ViewKind.HintArrow, SkinId.Default), cancellation.Token);
                return new ArrowCompletion(prefab, null, false);
            }
            catch (OperationCanceledException)
            {
                return new ArrowCompletion(null, null, true);
            }
            catch (Exception exception)
            {
                return new ArrowCompletion(null, exception, false);
            }
            finally
            {
                cancellation.Dispose();
            }
        }

        private static async Task<Completion> ObserveAsync(IHintService service, LevelDefinition board, CancellationOwner cancellation)
        {
            try
            {
                HintResult result = await service.CalculateAsync(board, cancellation.Token).ConfigureAwait(false);
                return new Completion(result, null, false);
            }
            catch (OperationCanceledException)
            {
                return new Completion(default, null, true);
            }
            catch (Exception exception)
            {
                return new Completion(default, exception, false);
            }
            finally
            {
                cancellation.Dispose();
            }
        }

        internal readonly struct Completion
        {
            public HintResult Result { get; }
            public Exception Error { get; }
            public bool IsCanceled { get; }

            public Completion(HintResult result, Exception error, bool isCanceled)
            {
                Result = result;
                Error = error;
                IsCanceled = isCanceled;
            }
        }

        internal readonly struct ArrowCompletion
        {
            public GameObject Prefab { get; }
            public Exception Error { get; }
            public bool IsCanceled { get; }

            public ArrowCompletion(GameObject prefab, Exception error, bool isCanceled)
            {
                Prefab = prefab;
                Error = error;
                IsCanceled = isCanceled;
            }
        }

        private sealed class CancellationOwner : IDisposable
        {
            private readonly CancellationTokenSource _source = new();
            private readonly object _gate = new();
            private bool _disposed;

            public CancellationToken Token => _source.Token;

            public void Cancel()
            {
                lock (_gate)
                {
                    if (_disposed == false)
                    {
                        _source.Cancel();
                    }
                }
            }

            public void Dispose()
            {
                lock (_gate)
                {
                    if (_disposed)
                    {
                        return;
                    }
                    
                    _disposed = true;
                    _source.Dispose();
                }
            }
        }
    }
}
