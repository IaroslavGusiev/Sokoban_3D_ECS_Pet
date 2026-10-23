using Zenject;

namespace Sokoban.Presentation
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
