using Zenject;
using UnityEngine;
using UnityEngine.Serialization;

namespace Sokoban.Presentation
{
    public class ServicesInstaller : MonoInstaller
    {
        [FormerlySerializedAs("_levelRoot")]
        [SerializeField] private Transform levelRoot;
        [FormerlySerializedAs("_viewCatalog")]
        [SerializeField] private ViewCatalog viewCatalog;
        [SerializeField] private LevelList levelList;

        public override void InstallBindings()
        {
            viewCatalog.Validate();
            levelList.Validate();

            Container
                .BindInterfacesAndSelfTo<ViewAssets>()
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
                .Bind<LevelList>()
                .FromInstance(levelList)
                .AsSingle();

            Container
                .Bind<ViewCatalog>()
                .FromInstance(viewCatalog)
                .AsSingle();
            
            Container
                .Bind<LevelRoot>()
                .FromInstance(new LevelRoot(levelRoot))
                .AsSingle();
        }
    }
}
