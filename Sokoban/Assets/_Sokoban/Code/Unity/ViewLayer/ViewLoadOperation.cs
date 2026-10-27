using System;
using UnityEngine;
using System.Threading;
using System.Threading.Tasks;

namespace Sokoban.Presentation
{
    internal sealed class ViewLoadOperation
    {
        private readonly CancellationOwner _cancellation;

        public ViewKey Key { get; }
        public Task<Completion> Task { get; }

        public ViewLoadOperation(IViewProvider provider, ViewKey key, CancellationToken runToken)
        {
            Key = key;
            _cancellation = new CancellationOwner(runToken);
            Task = ObserveAsync(provider, key, _cancellation);
        }

        public void Cancel() =>
            _cancellation.Cancel();

        private static async Task<Completion> ObserveAsync(IViewProvider provider, ViewKey key, CancellationOwner cancellation)
        {
            try
            {
                GameObject prefab = await provider.LoadAsync(key, cancellation.Token);
                return new Completion(prefab, null, false);
            }
            catch (OperationCanceledException)
            {
                return new Completion(null, null, true);
            }
            catch (Exception exception)
            {
                return new Completion(null, exception, false);
            }
            finally
            {
                cancellation.Dispose();
            }
        }

        internal readonly struct Completion
        {
            public GameObject Prefab { get; }
            public Exception Error { get; }
            public bool IsCanceled { get; }

            public Completion(GameObject prefab, Exception error, bool isCanceled)
            {
                Prefab = prefab;
                Error = error;
                IsCanceled = isCanceled;
            }
        }

        private sealed class CancellationOwner : IDisposable
        {
            private readonly CancellationTokenSource _source;
            private readonly object _gate = new();
            private bool _disposed;

            public CancellationToken Token =>
                _source.Token;

            public CancellationOwner(CancellationToken token) =>
                _source = CancellationTokenSource.CreateLinkedTokenSource(token);

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
