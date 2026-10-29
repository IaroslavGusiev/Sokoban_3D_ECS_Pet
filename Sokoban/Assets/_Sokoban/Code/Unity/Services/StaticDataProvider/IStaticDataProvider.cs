using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Sokoban.Presentation
{
    public interface IStaticDataProvider : IDisposable
    {
        int LevelCount { get; }
        
        UniTask LoadAsync(CancellationToken cancellationToken);

        BackgroundCatalog GetBackgroundCatalog();
        ViewCatalog GetViewCatalog();
        GameplaySettings GetGameplaySettings();
        LevelConfig GetLevelConfig(int levelIndex);
    }
}
