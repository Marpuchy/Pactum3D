using SaveSystem;
using Zenject;

namespace Injections
{
    public class GameInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            ItemFactory.SetInstantiator(Container);

            Container.BindInterfacesTo<PlayerTransformRegistry>()
                .AsSingle()
                .IfNotBound();

            Container.Bind<IPlayerDataService>()
                .To<PlayerDataService>()
                .AsSingle()
                .IfNotBound();

            Container.Bind<ISaveSystem>()
                .To<SaveSystem.SaveSystem>()
                .AsSingle()
                .IfNotBound();

            Container.Bind<IGameSaveService>()
                .To<GameSaveService>()
                .AsSingle()
                .IfNotBound();
        }
        
    }
}
