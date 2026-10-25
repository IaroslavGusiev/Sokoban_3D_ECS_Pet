using System.Threading;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Sokoban.Presentation
{
    public interface IAddressablesService
    {
        AsyncOperationHandle<T> Load<T>(string address);
        
        AsyncOperationHandle<IList<T>> LoadByLabel<T>(string label);
        
        UniTask<T> AwaitAsync<T>(AsyncOperationHandle<T> handle, CancellationToken cancellationToken);
        
        void Release<T>(AsyncOperationHandle<T> handle);
    }
}
