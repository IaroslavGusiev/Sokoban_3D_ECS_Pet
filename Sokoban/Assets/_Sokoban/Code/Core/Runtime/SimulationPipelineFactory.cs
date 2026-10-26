using DefaultEcs;
using DefaultEcs.System;
using Sokoban.Core.Systems;
using System.Collections.Generic;

namespace Sokoban.Core
{
    public sealed class SimulationPipelineFactory
    {
        public SystemPipeline Create(World world) =>
            new(CreateSystems(world));

        internal IEnumerable<ISystem<float>> CreateSystems(World world)
        {
            yield return new UndoSystem(world);
            yield return new MovementSystem(world);
            yield return new MoveCountSystem(world);
            yield return new GoalTrackingSystem(world);
            yield return new WinCheckSystem(world);
            yield return new LevelTimerSystem(world);
        }
    }
}
