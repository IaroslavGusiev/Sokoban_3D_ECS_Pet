using Zenject;

namespace Sokoban.Unity
{
    public sealed class FeaturesInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Container
                .Bind<ViewFeatures>()
                .AsSingle();
        }
    }
}
