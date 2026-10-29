using System;
using UnityEngine;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Sokoban.Presentation
{
    internal sealed class BackgroundLoadOperation : IDisposable
    {
        private readonly IAddressablesService _addressables;
        private readonly AsyncOperationHandle<Cubemap> _handle;
        private readonly CancellationTokenSource _cancellation = new();
        
        private readonly object _gate = new();

        private bool _released;
        private bool _completed;
        private bool _releaseRequested;

        public object Entry { get; }
        public Task<Completion> Task { get; }
        public BackgroundPreset Preset { get; }

        public BackgroundLoadOperation(IAddressablesService addressables, BackgroundPreset preset, object entry)
        {
            _addressables = addressables;
            Preset = preset;
            Entry = entry;

            try
            {
                _handle = addressables.Load<Cubemap>(preset.Address);
                Task = ObserveAsync();
            }
            catch
            {
                _cancellation.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_releaseRequested)
                {
                    return;
                }
                _releaseRequested = true;

                if (_completed == false)
                {
                    _cancellation.Cancel();
                }
                else
                {
                    Release();
                }
            }
        }
        
        private async Task<Completion> ObserveAsync()
        {
            Completion completion;
            
            try
            {
                Cubemap cubemap = await _addressables.AwaitAsync(_handle, _cancellation.Token);
                completion = new Completion(cubemap, null);
            }
            catch (OperationCanceledException)
            {
                completion = new Completion(null, null);
            }
            catch (Exception exception)
            {
                completion = new Completion(null, exception);
            }

            await UniTask.SwitchToMainThread();
            
            lock (_gate)
            {
                _completed = true;
                
                _cancellation.Dispose();
                if (_releaseRequested)
                {
                    Release();
                }
            }
            return completion;
        }

        private void Release()
        {
            if (_released)
            {
                return;
            }
            _released = true;

            try
            {
                _addressables.Release(_handle);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        internal readonly struct Completion
        {
            public Cubemap Cubemap { get; }
            public Exception Error { get; }

            public Completion(Cubemap cubemap, Exception error)
            {
                Cubemap = cubemap;
                Error = error;
            }
        }
    }
}
