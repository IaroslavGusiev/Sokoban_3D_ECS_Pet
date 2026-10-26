using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using NUnit.Framework;
using Sokoban.Core;
using Sokoban.Core.Commands;
using Sokoban.Core.Components;

namespace Sokoban.CoreTests
{
    public sealed class HintSolverTests
    {
        public static IEnumerable<TestCaseData> Levels() => Directory.GetFiles(LevelFiles.DirectoryPath, "*.txt")
            .OrderBy(path => path).Select(path => new TestCaseData(path).SetName($"Hints_{Path.GetFileNameWithoutExtension(path)}"));

        [TestCaseSource(nameof(Levels))]
        public void RepeatedHints_SolveShippedBoardsInTheIndependentOraclesShortestMoveCount(string path)
        {
            LevelDefinition level = LevelParser.Parse(File.ReadAllText(path));
            int expectedMoves = LevelSolver.Solve(level).Moves.Count;
            var solver = new HintSolver();
            using var simulation = new Simulation(level, new LevelPopulator(), 60f, new SimulationPipelineFactory());
            for (var index = 0; index < expectedMoves; index++)
            {
                GameplaySnapshot snapshot = simulation.CaptureSnapshot();
                var board = new LevelDefinition(level.Width, level.Height, snapshot.PlayerPosition, level.Walls, snapshot.BoxPositions, level.Goals);
                HintResult result = solver.Solve(board, 100_000, CancellationToken.None);
                Assert.That(result.Outcome, Is.EqualTo(HintOutcome.Found));
                Assert.That(new[] { MoveCommand.Up, MoveCommand.Down, MoveCommand.Left, MoveCommand.Right }, Does.Contain(result.Move));
                simulation.Send(result.Move);
                simulation.Tick(0f);
                Assert.That(simulation.Result.Moves, Is.EqualTo(index + 1));
            }
            Assert.That(simulation.IsCompleted, Is.True);
            Assert.That(simulation.CaptureSnapshot().BoxPositions, Is.EquivalentTo(level.Goals));
        }

        [Test]
        public void DeadCorner_ExhaustsReachableStatesAndReportsNoSolution()
        {
            var board = LevelParser.Parse("#####\n#$@-#\n#--.#\n#####");
            Assert.Throws<InvalidOperationException>(() => LevelSolver.Solve(board));
            HintResult result = new HintSolver().Solve(board, 1000, CancellationToken.None);
            Assert.That(result.Outcome, Is.EqualTo(HintOutcome.NoSolution));
            Assert.That(result.Move, Is.EqualTo(default(MoveCommand)));
        }

        [Test]
        public void InsufficientBudget_IsDistinctFromNoSolution()
        {
            var board = LevelParser.Parse("#######\n#@-$-.#\n#######");
            Assert.That(new HintSolver().Solve(board, 1, CancellationToken.None).Outcome, Is.EqualTo(HintOutcome.BudgetExhausted));
            Assert.That(new HintSolver().Solve(board, 1000, CancellationToken.None).Outcome, Is.EqualTo(HintOutcome.Found));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void InvalidBudget_IsRejected(int budget)
        {
            var board = LevelParser.Parse("#####\n#@$.#\n#####");
            Assert.Throws<ArgumentOutOfRangeException>(() => new HintSolver().Solve(board, budget, CancellationToken.None));
        }

        [Test]
        public void AlreadyCancelledToken_IsRejectedBeforeSearch()
        {
            var board = LevelParser.Parse("########\n#$-----#\n#--$---#\n#--@---#\n#--$---#\n#---...#\n########");
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() => new HintSolver().Solve(board, 1000, cancellation.Token));
        }

        [Test]
        public void AlreadySolved_ReportsNoRecommendedMove()
        {
            var board = new LevelDefinition(3, 3, new GridPosition(1, 1), Array.Empty<GridPosition>(),
                new[] { new GridPosition(1, 2) }, new[] { new GridPosition(1, 2) });
            HintResult result = new HintSolver().Solve(board, 100, CancellationToken.None);
            Assert.That(result.Outcome, Is.EqualTo(HintOutcome.AlreadySolved));
            Assert.That(result.Move, Is.EqualTo(default(MoveCommand)));
        }
    }
}
