using Sokoban.Core;

namespace Sokoban.Presentation
{
    internal sealed class LevelRunFactory
    {
        private readonly LevelRoot _root;
        private readonly IViewProvider _provider;
        private readonly LevelPopulator _populator;
        private readonly SimulationPipelineFactory _simulationPipelineFactory;
        private readonly ViewPipelineFactory _viewPipelineFactory;

        public LevelRunFactory(
            LevelRoot root, 
            IViewProvider provider, 
            LevelPopulator populator,
            SimulationPipelineFactory simulationPipelineFactory, 
            ViewPipelineFactory viewPipelineFactory)
        {
            _root = root;
            _provider = provider;
            _populator = populator;
            _viewPipelineFactory = viewPipelineFactory;
            _simulationPipelineFactory = simulationPipelineFactory;
        }

        public LevelRun Create(LevelDefinition level, RunOptions options) =>
            new(_root, level, _provider, _populator, _simulationPipelineFactory, _viewPipelineFactory, options);
    }
}
