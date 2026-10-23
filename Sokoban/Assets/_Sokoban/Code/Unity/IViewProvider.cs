using UnityEngine;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Sokoban.Presentation
{
    public interface IViewProvider
    {
        UniTask<GameObject> LoadAsync(ViewKey key, CancellationToken ct);
    }
}
