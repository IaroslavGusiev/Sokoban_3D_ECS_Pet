using System.Linq;
using System.Collections.Generic;

namespace Sokoban.Core
{
    public sealed class SimulationRecording
    {
        public LevelDefinition InitialLevel { get; }
        public float TimeLimitSeconds { get; }
        public IReadOnlyList<SimulationInput> Inputs { get; }

        internal SimulationRecording(LevelDefinition level, float timeLimitSeconds, IEnumerable<SimulationInput> inputs)
        {
            InitialLevel = new LevelDefinition(level.Width, level.Height, level.PlayerStart, level.Walls, level.Boxes, level.Goals);
            TimeLimitSeconds = timeLimitSeconds;
            Inputs = System.Array.AsReadOnly(inputs.ToArray());
        }
    }
}
