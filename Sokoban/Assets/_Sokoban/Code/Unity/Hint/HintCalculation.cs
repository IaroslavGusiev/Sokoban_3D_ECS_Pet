using System;
using System.Threading;
using System.Threading.Tasks;
using Sokoban.Core;

namespace Sokoban.Presentation
{
    internal sealed class HintCalculation
    {
        private readonly CancellationOwner _cancellation = new();

        public Task<Completion> Task { get; }

        public HintCalculation(IHintService service, LevelDefinition board) =>
            Task = ObserveAsync(service, board, _cancellation);

        public void Cancel() =>
            _cancellation.Cancel();

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
                    if (_disposed == false) _source.Cancel();
                }
            }

            public void Dispose()
            {
                lock (_gate)
                {
                    if (_disposed) return;
                    _disposed = true;
                    _source.Dispose();
                }
            }
        }
    }
}
