using Zenject;

namespace Sokoban.Presentation
{
    public sealed class GameplayInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Container
                .BindInterfacesAndSelfTo<GameplayController>()
                .AsSingle();
        }
    }
}
