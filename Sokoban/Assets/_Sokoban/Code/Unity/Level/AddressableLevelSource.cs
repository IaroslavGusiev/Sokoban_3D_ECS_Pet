using UnityEngine;
using Sokoban.Core;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Sokoban.Presentation
{
    public sealed class AddressableLevelSource : ILevelSource
    {
        private readonly IStaticDataProvider _staticData;
        private readonly IAddressablesService _addressables;

        public int Count => _staticData.LevelCount;

        public AddressableLevelSource(IStaticDataProvider staticData, IAddressablesService addressables)
        {
            _staticData = staticData;
            _addressables = addressables;
        }

        public async UniTask<LevelDefinition> LoadAsync(int index, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            LevelConfig config = _staticData.GetLevelConfig(index);
            
            AsyncOperationHandle<TextAsset> handle = _addressables.Load<TextAsset>(config.Address);
            
            try
            {
                TextAsset text = await _addressables.AwaitAsync(handle, ct);
                return LevelParser.Parse(text.text);
            }
            finally
            {
                _addressables.Release(handle);
            }
        }
    }
}
