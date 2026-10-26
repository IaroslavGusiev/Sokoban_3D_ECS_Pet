using Sokoban.Core;

namespace Sokoban.Presentation
{
    internal sealed class LevelRunFactory
    {
        private readonly LevelRoot _root;
        private readonly IViewProvider _provider;
        private readonly ViewFeatures _features;
        private readonly LevelPopulator _populator;
        private readonly HintArrow _hintArrowPrefab;

        public LevelRunFactory(LevelRoot root, IViewProvider provider, ViewFeatures features, LevelPopulator populator, HintArrow hintArrowPrefab = null)
        {
            _root = root;
            _provider = provider;
            _features = features;
            _populator = populator;
            _hintArrowPrefab = hintArrowPrefab;
        }

        public LevelRun Create(LevelDefinition level, RunOptions options) =>
            new(_root, level, _provider, _features, _populator, options, _hintArrowPrefab);
    }
}
