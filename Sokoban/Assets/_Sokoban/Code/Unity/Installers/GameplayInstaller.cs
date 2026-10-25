using Zenject;

namespace Sokoban.Presentation
{
    public sealed class GameplayInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Container
                .Bind(typeof(GameplayController), typeof(IInitializable), typeof(ITickable), typeof(IGameplayStatus), typeof(IGameplayCommands))
                .To<GameplayController>()
                .AsSingle();

            Container
                .BindInterfacesAndSelfTo<GameplayLifetime>()
                .AsSingle();
        }
    }
}
