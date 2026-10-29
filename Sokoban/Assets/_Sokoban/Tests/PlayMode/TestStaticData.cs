using System.Threading;
using Cysharp.Threading.Tasks;
using Sokoban.Presentation;

namespace Sokoban.Tests.PlayMode
{
    internal sealed class TestStaticData : IStaticDataProvider
    {
        public GameplaySettings Gameplay;
        public ViewCatalog Views;
        public BackgroundCatalog Backgrounds;
        public LevelConfig[] Levels = { new("levels/level-01"), new("levels/level-02") };
        public UniTaskCompletionSource Pending;
        public int LoadCalls;
        public int LevelCount => Levels.Length;

        public async UniTask LoadAsync(CancellationToken cancellationToken)
        {
            LoadCalls++;
            cancellationToken.ThrowIfCancellationRequested();
            if (Pending != null) await Pending.Task.AttachExternalCancellation(cancellationToken);
        }

        public GameplaySettings GetGameplaySettings() => Gameplay;
        public BackgroundCatalog GetBackgroundCatalog() => Backgrounds;
        public ViewCatalog GetViewCatalog() => Views;
        public LevelConfig GetLevelConfig(int levelIndex) => Levels[levelIndex];

        public void Dispose() { }
    }
}
