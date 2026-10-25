using DefaultEcs;
using DefaultEcs.System;
using Sokoban.Core.Systems;

namespace Sokoban.Core.Features
{
    internal sealed class UndoFeature : IFeature
    {
        public ISystem<float> Create(World world) =>
            new UndoSystem(world);
    }
}
