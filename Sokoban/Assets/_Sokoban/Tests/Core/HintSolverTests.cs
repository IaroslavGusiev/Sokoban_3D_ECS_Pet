using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        [Test]
        public void EqualLengthSolutionsUseTheDeclaredDirectionOrder()
        {
            var board = LevelParser.Parse("#####\n#--.#\n#-$-#\n#@--#\n#####");
            HintResult result = new HintSolver().Solve(board, 1000, CancellationToken.None);
            Assert.That(result.Outcome, Is.EqualTo(HintOutcome.Found));
            Assert.That(result.Move, Is.EqualTo(MoveCommand.Up));
        }

        [Test]
        public void BoxInputOrderDoesNotChangeTheDeterministicRecommendation()
        {
            var board = LevelParser.Parse("#######\n#--.--#\n#--$.-#\n#--$--#\n#@----#\n#######");
            var reordered = new LevelDefinition(board.Width, board.Height, board.PlayerStart,
                board.Walls, board.Boxes.Reverse(), board.Goals);
            var solver = new HintSolver();
            HintResult first = solver.Solve(board, 100_000, CancellationToken.None);
            HintResult second = solver.Solve(reordered, 100_000, CancellationToken.None);
            Assert.That(first.Outcome, Is.EqualTo(HintOutcome.Found));
            Assert.That(second.Outcome, Is.EqualTo(first.Outcome));
            Assert.That(second.Move, Is.EqualTo(first.Move));
        }

        [Test]
        public void BudgetCountsTheInitialStateBeforeAcceptingAWinningSuccessor()
        {
            var board = new LevelDefinition(3, 1, new GridPosition(0, 0), Array.Empty<GridPosition>(),
                new[] { new GridPosition(1, 0) }, new[] { new GridPosition(2, 0) });
            var solver = new HintSolver();
            Assert.That(solver.Solve(board, 1, CancellationToken.None).Outcome, Is.EqualTo(HintOutcome.BudgetExhausted));
            HintResult result = solver.Solve(board, 2, CancellationToken.None);
            Assert.That(result.Outcome, Is.EqualTo(HintOutcome.Found));
            Assert.That(result.Move, Is.EqualTo(MoveCommand.Right));
        }

        [Test]
        public async Task SharedSolverKeepsConcurrentSearchStateIndependent()
        {
            var solver = new HintSolver();
            var board = LevelParser.Parse("#####\n#--.#\n#-$-#\n#@--#\n#####");
            HintResult[] results = await Task.WhenAll(Enumerable.Range(0, 8).Select(index =>
                Task.Run(() => solver.Solve(board, index % 2 == 0 ? 1 : 1000, CancellationToken.None))));
            for (int index = 0; index < results.Length; index++)
            {
                Assert.That(results[index].Outcome, Is.EqualTo(index % 2 == 0 ? HintOutcome.BudgetExhausted : HintOutcome.Found));
                Assert.That(results[index].Move, Is.EqualTo(index % 2 == 0 ? default : MoveCommand.Up));
            }
        }
    }
}
