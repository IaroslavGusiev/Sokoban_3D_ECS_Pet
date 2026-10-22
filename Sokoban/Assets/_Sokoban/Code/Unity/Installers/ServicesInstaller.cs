using Zenject;
using UnityEngine;

namespace Sokoban.Unity
{
    public class ServicesInstaller : MonoInstaller
    {
        [SerializeField] private Transform levelRoot;
        [SerializeField] private ViewCatalog viewCatalog;

        public override void InstallBindings()
        {
            Container
                .Bind<ILevelProvider>()
                .To<HardcodedLevelProvider>()
                .AsSingle();
            
            Container
                .Bind<IPlayerInput>()
                .To<KeyboardMoveInput>()
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
