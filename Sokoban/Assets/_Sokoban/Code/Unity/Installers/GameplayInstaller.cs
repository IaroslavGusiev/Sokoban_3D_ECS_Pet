using Zenject;

namespace Sokoban.Unity
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
