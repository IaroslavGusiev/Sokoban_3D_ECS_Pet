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
        private readonly HintArrow _hintArrowPrefab;

        public LevelRunFactory(LevelRoot root, IViewProvider provider, LevelPopulator populator,
            SimulationPipelineFactory simulationPipelineFactory, ViewPipelineFactory viewPipelineFactory, HintArrow hintArrowPrefab = null)
        {
            _root = root;
            _provider = provider;
            _populator = populator;
            _simulationPipelineFactory = simulationPipelineFactory;
            _viewPipelineFactory = viewPipelineFactory;
            _hintArrowPrefab = hintArrowPrefab;
        }

        public LevelRun Create(LevelDefinition level, RunOptions options) =>
            new(_root, level, _provider, _populator, _simulationPipelineFactory, _viewPipelineFactory, options, _hintArrowPrefab);
    }
}
