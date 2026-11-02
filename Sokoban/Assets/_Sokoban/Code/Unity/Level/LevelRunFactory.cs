using Sokoban.Core;

namespace Sokoban.Presentation
{
    internal sealed class LevelRunFactory
    {
        private readonly LevelRoot _root;
        private readonly IViewProvider _provider;
        private readonly LevelPopulator _populator;
        private readonly ViewPipelineFactory _viewPipelineFactory;
        private readonly SimulationPipelineFactory _simulationPipelineFactory;

        public LevelRunFactory(
            LevelRoot root, 
            IViewProvider provider, 
            LevelPopulator populator,
            ViewPipelineFactory viewPipelineFactory, 
            SimulationPipelineFactory simulationPipelineFactory)
        {
            _root = root;
            _provider = provider;
            _populator = populator;
            _viewPipelineFactory = viewPipelineFactory;
            _simulationPipelineFactory = simulationPipelineFactory;
        }

        public ReplaySession CreateReplay(SimulationRecording recording, RunOptions options) =>
            new(recording, _root, _provider, _viewPipelineFactory, options);

        public LevelRun Create(LevelDefinition level, RunOptions options) =>
            new(_root, level, _provider, _populator, _simulationPipelineFactory, _viewPipelineFactory, options);
    }
}
