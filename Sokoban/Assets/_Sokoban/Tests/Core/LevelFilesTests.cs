using System.Collections.Generic;
using System.IO;
using System.Linq;
using DefaultEcs;
using NUnit.Framework;
using Sokoban.Core;
using Sokoban.Core.Commands;
using Sokoban.Core.Components;

namespace Sokoban.CoreTests
{
    public sealed class LevelFilesTests
    {
        public static IEnumerable<TestCaseData> Levels() => Directory
            .GetFiles(LevelFiles.DirectoryPath, "*.txt")
            .OrderBy(path => path)
            .Select(path => new TestCaseData(path).SetName($"Level_{Path.GetFileNameWithoutExtension(path)}_SolvesAndReplays"));

        [TestCaseSource(nameof(Levels))]
        public void EveryLevel_ParsesSolvesAndReplaysThroughSimulation(string path)
        {
            LevelDefinition level = LevelParser.Parse(File.ReadAllText(path));
            var solution = LevelSolver.Solve(level);
            IReadOnlyList<MoveCommand> moves = solution.Moves;
            TestContext.WriteLine($"{Path.GetFileName(path)}: shortest solution = {moves.Count} moves.");

            using var simulation = new Simulation(level, new LevelPopulator(), 60f, new SimulationPipelineFactory());
            Assert.That(simulation.IsCompleted, Is.False);
            Entity player = simulation.World.Single(entity => entity.Has<PlayerTag>());
            var snapshots = new List<GameplaySnapshot>();
            for (int index = 0; index < moves.Count; index++)
            {
                simulation.Send(moves[index]);
                simulation.Tick(0f);
                snapshots.Add(simulation.CaptureSnapshot());
                Assert.That(player.Get<GridPosition>(), Is.EqualTo(solution.PlayerPositions[index]), $"Player position after move {index + 1}.");
            }

            var boxes = new HashSet<GridPosition>();
            foreach (Entity entity in simulation.World)
            {
                if (entity.Has<BoxTag>())
                {
                    boxes.Add(entity.Get<GridPosition>());
                }
            }

            Assert.That(boxes, Is.EquivalentTo(level.Goals));
            Assert.That(simulation.IsCompleted, Is.True);
            Assert.That(simulation.Result.Moves, Is.EqualTo(moves.Count));
            SimulationRecording recording = simulation.ExportRecording();
            using var replay = new SimulationReplay(recording);
            int tick = 0;
            foreach (SimulationInput input in recording.Inputs)
            {
                Assert.That(replay.Step(), Is.True);
                if (input.Kind == SimulationInputKind.Tick)
                {
                    Assert.That(replay.Simulation.CaptureSnapshot(), Is.EqualTo(snapshots[tick++]));
                }
            }
            Assert.That(tick, Is.EqualTo(moves.Count));
            Assert.That(replay.Simulation.CaptureSnapshot(), Is.EqualTo(simulation.CaptureSnapshot()));
        }
    }
}
