using Sokoban.Core;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Sokoban.Presentation
{
    public interface ILevelSource
    {
        int Count { get; }
        UniTask<LevelDefinition> LoadAsync(int index, CancellationToken ct);
    }
}
