using Zenject;
using UnityEngine;
using Sokoban.Core;

namespace Sokoban.Presentation
{
    public class ServicesInstaller : MonoInstaller
    {
        [SerializeField] private Transform levelRoot;
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private UnityEngine.Rendering.Volume backgroundVolume;
        [SerializeField] private Shader backgroundShader;

        public override void InstallBindings()
        {
            Container
                .Bind<System.Random>()
                .AsSingle();

            Container
                .Bind<BackgroundDisplay>()
                .FromMethod(_ => new BackgroundDisplay(gameplayCamera, backgroundVolume, backgroundShader))
                .AsSingle();

            Container
                .Bind<BackgroundCoordinator>()
                .AsSingle();

            Container
                .Bind<HintSolver>()
                .AsSingle();

            Container
                .Bind<IHintService>()
                .To<HintService>()
                .AsSingle();

            Container
                .Bind<HintCoordinator>()
                .AsSingle();

            Container
                .Bind<IAddressablesService>()
                .To<AddressablesService>()
                .AsSingle();

            Container
                .Bind<IStaticDataProvider>()
                .To<StaticDataProvider>()
                .AsSingle();

            Container
                .Bind<LevelRunFactory>()
                .AsSingle();

            Container
                .Bind<LevelPopulator>()
                .AsSingle();

            Container
                .Bind<SimulationPipelineFactory>()
                .AsSingle();

            Container
                .Bind<ViewPipelineFactory>()
                .AsSingle();

            Container
                .Bind(typeof(ViewAssets), typeof(IViewProvider))
                .To<ViewAssets>()
                .AsSingle();

            Container
                .Bind<ILevelSource>()
                .To<AddressableLevelSource>()
                .AsSingle();

            Container
                .Bind<IPlayerInput>()
                .To<KeyboardMoveInput>()
                .AsSingle();

            Container
                .Bind<LevelRoot>()
                .FromInstance(new LevelRoot(levelRoot))
                .AsSingle();
        }
    }
}
