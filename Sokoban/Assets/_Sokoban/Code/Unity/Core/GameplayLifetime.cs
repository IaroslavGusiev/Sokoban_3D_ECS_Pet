using System;

namespace Sokoban.Presentation
{
    internal sealed class GameplayLifetime : IDisposable
    {
        private readonly ViewAssets _views;
        private readonly GameplayController _controller;
        private readonly IStaticDataProvider _staticData;

        public GameplayLifetime(GameplayController controller, ViewAssets views, IStaticDataProvider staticData)
        {
            _views = views;
            _staticData = staticData;
            _controller = controller;
        }

        public void Dispose()
        {
            try
            {
                _controller.Dispose();
            }
            finally
            {
                try
                {
                    _views.Dispose();
                }
                finally
                {
                    _staticData.Dispose();
                }
            }
        }
    }
}
