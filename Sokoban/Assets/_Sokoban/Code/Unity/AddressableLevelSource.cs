using UnityEngine;
using Sokoban.Core;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Sokoban.Presentation
{
    public sealed class AddressableLevelSource : ILevelSource
    {
        private readonly LevelList _levels;

        public int Count => _levels.Count;

        public AddressableLevelSource(LevelList levels)
        {
            _levels = levels;
        }

        public async UniTask<LevelDefinition> LoadAsync(int index, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            AsyncOperationHandle<TextAsset> handle = Addressables.LoadAssetAsync<TextAsset>(_levels.Levels[index]);
            try
            {
                TextAsset text = await handle.ToUniTask(cancellationToken: ct, cancelImmediately: true, autoReleaseWhenCanceled: false);
                return LevelParser.Parse(text.text);
            }
            finally
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }
        }
    }
}
