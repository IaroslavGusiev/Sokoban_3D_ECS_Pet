using System;
using UnityEngine;
using System.Threading;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Sokoban.Presentation
{
    public sealed class ViewAssets : IViewProvider, IDisposable
    {
        private readonly IStaticDataProvider _staticData;
        private readonly IAddressablesService _addressables;
        
        private readonly Dictionary<ViewKey, CachedLoad> _handles = new();

        public ViewAssets(IStaticDataProvider staticData, IAddressablesService addressables)
        {
            _staticData = staticData;
            _addressables = addressables;
        }

        public async UniTask<GameObject> LoadAsync(ViewKey key, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            CachedLoad load = null;
            
            try
            {
                ViewCatalog.SkinEntry entry = _staticData.GetViewCatalog().Resolve(key);
                var resolvedKey = new ViewKey(key.Kind, entry.Skin);
                
                if (_handles.TryGetValue(resolvedKey, out load)
                    && load.Handle.Status == AsyncOperationStatus.Failed
                    && load.FailureFrame < Time.frameCount)
                {
                    AsyncOperationHandle<GameObject> failed = load.Handle;
                    _handles.Remove(resolvedKey);
                    load = null;
                    _addressables.Release(failed);
                }

                if (load == null)
                {
                    load = new CachedLoad(_addressables.Load<GameObject>(entry.Address));
                    _handles.Add(resolvedKey, load);
                    
                    if (load.Handle.IsDone)
                    {
                        load.OnCompleted(load.Handle);
                    }
                    else
                    {
                        load.Handle.Completed += load.OnCompleted;
                    }
                }

                return await _addressables.AwaitAsync(load.Handle, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                if (load == null)
                {
                    Debug.LogException(exception);
                }
                else
                {
                    load.LogFailure(exception);
                }
                throw;
            }
        }

        public void Dispose()
        {
            foreach (CachedLoad load in _handles.Values)
            {
                _addressables.Release(load.Handle);
            }
        }

        private sealed class CachedLoad
        {
            public readonly AsyncOperationHandle<GameObject> Handle;
            public int FailureFrame { get; private set; }
            private bool _failureLogged;

            public CachedLoad(AsyncOperationHandle<GameObject> handle) =>
                Handle = handle;

            public void OnCompleted(AsyncOperationHandle<GameObject> handle)
            {
                if (handle.Status == AsyncOperationStatus.Failed)
                {
                    FailureFrame = Time.frameCount;
                    LogFailure(handle.OperationException);
                }
            }

            public void LogFailure(Exception exception)
            {
                if (_failureLogged)
                {
                    return;
                }

                _failureLogged = true;
                Debug.LogException(exception);
            }
        }
    }
}
