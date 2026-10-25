using System.Threading;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Sokoban.Presentation
{
    public sealed class AddressablesService : IAddressablesService
    {
        public AsyncOperationHandle<T> Load<T>(string address) => 
            Addressables.LoadAssetAsync<T>(address);

        public AsyncOperationHandle<IList<T>> LoadByLabel<T>(string label) =>
            Addressables.LoadAssetsAsync<T>(label, null, true);

        public UniTask<T> AwaitAsync<T>(AsyncOperationHandle<T> handle, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            return handle.ToUniTask(
                cancellationToken: cancellationToken,
                cancelImmediately: true, 
                autoReleaseWhenCanceled: false);
        }

        public void Release<T>(AsyncOperationHandle<T> handle)
        {
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
        }
    }
}
