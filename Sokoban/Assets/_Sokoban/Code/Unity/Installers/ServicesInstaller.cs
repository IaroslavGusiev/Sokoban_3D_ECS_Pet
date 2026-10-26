using Zenject;
using Sokoban.Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace Sokoban.Presentation
{
    public class ServicesInstaller : MonoInstaller
    {
        [FormerlySerializedAs("_levelRoot")]
        [SerializeField] private Transform levelRoot;
        [SerializeField] private HintArrow hintArrowPrefab;

        public override void InstallBindings()
        {
            Container
                .Bind<HintSolver>()
                .AsSingle();

            Container
                .Bind<IHintService>()
                .To<HintService>()
                .AsSingle();

            Container
                .Bind<HintArrow>()
                .FromInstance(hintArrowPrefab)
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
