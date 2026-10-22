using DefaultEcs;
using DefaultEcs.System;

namespace Sokoban.Core
{
    public interface IFeature
    {
        ISystem<float> Create(World world);
    }
}
