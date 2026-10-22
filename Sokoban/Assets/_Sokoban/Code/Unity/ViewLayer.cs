using DefaultEcs;
using Sokoban.Core;
using System.Collections.Generic;

namespace Sokoban.Unity
{
    public sealed class ViewLayer : IWorldLayer
    {
        private readonly LevelView _view;
        private readonly SystemPipeline _pipeline;

        public ViewLayer(
            LevelRoot root, 
            LevelDefinition level, 
            World world, 
            ViewCatalog catalog,
            IReadOnlyList<IFeature> features)
        {
            _view = new LevelView(root.Transform);
            
            try
            {
                _view.Build(level, world, catalog);
                _pipeline = new SystemPipeline(world, features);
            }
            catch
            {
                _view.Dispose();
                throw;
            }
        }

        public void Tick(float deltaTime) => 
            _pipeline.Tick(deltaTime);

        public void Dispose()
        {
            try
            {
                _view.Dispose();
            }
            finally
            {
                _pipeline.Dispose();
            }
        }
    }
}
