using Zenject;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sokoban.Presentation
{
    public sealed class GameplayInstaller : MonoInstaller
    {
        [SerializeField] private Transform levelRoot;
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private Volume backgroundVolume;
        [SerializeField] private Shader backgroundShader;

        public override void InstallBindings()
        {
            Container
                .Bind<LevelRoot>()
                .FromInstance(new LevelRoot(levelRoot))
                .AsSingle();

            Container
                .Bind<BackgroundDisplay>()
                .AsSingle()
                .WithArguments(gameplayCamera, backgroundVolume, backgroundShader);

            Container
                .Bind<BackgroundCoordinator>()
                .AsSingle();

            Container
                .BindInterfacesAndSelfTo<GameplayStatus>()
                .AsSingle();

            Container
                .BindInterfacesAndSelfTo<GameplayCommandInbox>()
                .AsSingle();

            Container
                .Bind(typeof(GameplayController), typeof(IInitializable), typeof(ITickable))
                .To<GameplayController>()
                .AsSingle();

            Container
                .BindInterfacesAndSelfTo<GameplayLifetime>()
                .AsSingle();
        }
    }
}
