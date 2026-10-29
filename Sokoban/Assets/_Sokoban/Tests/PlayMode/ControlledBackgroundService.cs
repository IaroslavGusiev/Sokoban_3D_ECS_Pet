using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sokoban.Presentation;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Sokoban.Tests.PlayMode
{
    internal sealed class ControlledBackgroundService : IAddressablesService
    {
        public readonly List<Request> Requests = new();
        public bool IgnoreCancellation = true;
        public bool ThrowOnLoad;
        public Action<Request> OnRelease;

        public AsyncOperationHandle<T> Load<T>(string address)
        {
            if (ThrowOnLoad)
            {
                throw new InvalidOperationException("planned background start failure");
            }
            if (typeof(T) != typeof(Cubemap))
            {
                throw new InvalidOperationException("Only cubemaps expected.");
            }
            var request = new Request(address);
            Requests.Add(request);
            return ((AsyncOperationHandle)request.Handle).Convert<T>();
        }

        public UniTask<T> AwaitAsync<T>(AsyncOperationHandle<T> handle, CancellationToken cancellationToken)
        {
            Request request = Requests.Single(item => item.Handle.Equals(handle));
            request.Token = cancellationToken;
            return Await<T>(request, cancellationToken);
        }

        public AsyncOperationHandle<IList<T>> LoadByLabel<T>(string label) =>
            throw new NotSupportedException();

        public void Release<T>(AsyncOperationHandle<T> handle)
        {
            Request request = Requests.Single(item => item.Handle.Equals(handle));
            request.ReleaseCalls++;
            OnRelease?.Invoke(request);
            Addressables.Release(handle);
        }

        private async UniTask<T> Await<T>(Request request, CancellationToken token)
        {
            Cubemap result = IgnoreCancellation
                ? await request.Completion.Task
                : await request.Completion.Task.AttachExternalCancellation(token);
            return (T)(object)result;
        }

        internal sealed class Request
        {
            public readonly string Address;
            public readonly TestOperation<Cubemap> Operation = new();
            public readonly AsyncOperationHandle<Cubemap> Handle;
            public readonly UniTaskCompletionSource<Cubemap> Completion = new();
            public CancellationToken Token;
            public Cubemap Value;
            public int ReleaseCalls;
            public bool Finished;

            public Request(string address)
            {
                Address = address;
                Handle = Addressables.ResourceManager.StartOperation(Operation, default);
            }

            public void Finish(Cubemap cubemap, bool fail = false)
            {
                if (Finished)
                {
                    return;
                }
                Finished = true;
                Value = cubemap;
                Operation.Finish(cubemap);
                if (fail)
                {
                    Completion.TrySetException(new InvalidOperationException("planned background failure"));
                }
                else
                {
                    Completion.TrySetResult(cubemap);
                }
            }
        }
    }
}
