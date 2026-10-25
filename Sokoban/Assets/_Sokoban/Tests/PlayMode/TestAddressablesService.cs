using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sokoban.Presentation;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Sokoban.Tests.PlayMode
{
    internal sealed class TestAddressablesService : IAddressablesService
    {
        private readonly IAddressablesService _runtime = new AddressablesService();
        private readonly Func<string, Type, AsyncOperationHandle> _load;
        private readonly Func<string, Type, AsyncOperationHandle> _loadLabel;
        private readonly Action<AsyncOperationHandle> _released;

        public int ReleaseCalls { get; private set; }
        public List<AsyncOperationHandle> LoadHandles { get; } = new();
        public List<string> LoadedAddresses { get; } = new();

        public TestAddressablesService(Func<string, Type, AsyncOperationHandle> load = null,
            Func<string, Type, AsyncOperationHandle> loadLabel = null,
            Action<AsyncOperationHandle> released = null)
        {
            _load = load;
            _loadLabel = loadLabel;
            _released = released;
        }

        public AsyncOperationHandle<T> Load<T>(string address)
        {
            AsyncOperationHandle<T> handle = _load == null ? _runtime.Load<T>(address) : _load(address, typeof(T)).Convert<T>();
            LoadHandles.Add(handle);
            LoadedAddresses.Add(address);
            return handle;
        }

        public AsyncOperationHandle<IList<T>> LoadByLabel<T>(string label) =>
            _loadLabel == null ? _runtime.LoadByLabel<T>(label) : _loadLabel(label, typeof(T)).Convert<IList<T>>();

        public UniTask<T> AwaitAsync<T>(AsyncOperationHandle<T> handle, CancellationToken cancellationToken) =>
            _runtime.AwaitAsync(handle, cancellationToken);

        public void Release<T>(AsyncOperationHandle<T> handle)
        {
            ReleaseCalls++;
            _runtime.Release(handle);
            _released?.Invoke(handle);
        }
    }
}
