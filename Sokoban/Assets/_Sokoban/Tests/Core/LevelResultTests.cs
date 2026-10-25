using System.Linq;
using NUnit.Framework;
using Sokoban.Core;
using Sokoban.Core.Commands;
using Sokoban.Core.Components;

namespace Sokoban.CoreTests
{
    public sealed class LevelResultTests
    {
        [Test]
        public void WalkAndPush_CountOnceButWallAndBlockedPushDoNotCount()
        {
            using var simulation = new Simulation(LevelParser.Parse("#######\n#@-$-.#\n#######"), new LevelPopulator(), 60f);
            Step(simulation, MoveCommand.Left);
            Assert.That(simulation.Result.Moves, Is.Zero);
            Step(simulation, MoveCommand.Right);
            Assert.That(simulation.Result.Moves, Is.EqualTo(1));
            Step(simulation, MoveCommand.Right);
            Assert.That(simulation.Result.Moves, Is.EqualTo(2));
            using var blocked = new Simulation(LevelParser.Parse("########\n#@$$-..#\n########"), new LevelPopulator(), 60f);
            Step(blocked, MoveCommand.Right);
            Assert.That(blocked.Result.Moves, Is.Zero);
        }

        [Test]
        public void WinningMove_IsIncludedAndTheResultStaysFrozen()
        {
            using var simulation = new Simulation(LevelParser.Parse("#####\n#@$.#\n#####"), new LevelPopulator(), 60f);
            simulation.Tick(0.5f);
            Step(simulation, MoveCommand.Right);
            Assert.That(simulation.IsCompleted, Is.True);
            LevelResult result = simulation.Result;
            Assert.That(result.Moves, Is.EqualTo(1));
            Assert.That(result.ElapsedSeconds, Is.EqualTo(0.5f));
            simulation.Send(MoveCommand.Left);
            simulation.Tick(10f);
            simulation.Tick(0f);
            Assert.That(simulation.Result.Moves, Is.EqualTo(result.Moves));
            Assert.That(simulation.Result.ElapsedSeconds, Is.EqualTo(result.ElapsedSeconds));
            Assert.That(simulation.Result.RemainingSeconds, Is.EqualTo(result.RemainingSeconds));
        }

        [Test]
        public void Elapsed_IsTheTimeLimitMinusRemainingTime()
        {
            using var simulation = new Simulation(LevelParser.Parse("#######\n#@-$-.#\n#######"), new LevelPopulator(), 60f);
            simulation.Tick(0.5f);
            simulation.Tick(0.5f);
            Assert.That(simulation.Result.ElapsedSeconds, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(simulation.Result.ElapsedSeconds, Is.EqualTo(60f - simulation.Result.RemainingSeconds).Within(0.0001f));
            Assert.That(simulation.Result.Moves, Is.Zero);
        }

        [Test]
        public void MultiplePositionChangesBeforeOneTick_CountOnce()
        {
            using var simulation = new Simulation(LevelParser.Parse("#######\n#@-$-.#\n#######"), new LevelPopulator(), 60f);
            var player = simulation.World.Single(entity => entity.Has<PlayerTag>());
            player.Set(new GridPosition(2, 1));
            player.Set(new GridPosition(4, 1));
            simulation.Tick(0f);
            Assert.That(simulation.Result.Moves, Is.EqualTo(1));
            simulation.Tick(0f);
            Assert.That(simulation.Result.Moves, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void IntegerBoundary_ValidStepCountsOnceAndOverflowDoesNotMoveAnyParticipant(bool push)
        {
            var level = new LevelDefinition(3, 3,
                new GridPosition(int.MaxValue - (push ? 2 : 1), 1),
                System.Array.Empty<GridPosition>(),
                new[] { new GridPosition(push ? int.MaxValue - 1 : 1, 1) },
                new[] { new GridPosition(0, 0) });
            using var simulation = new Simulation(level, new LevelPopulator(), 60f);
            var player = simulation.World.Single(entity => entity.Has<PlayerTag>());
            var box = simulation.World.Single(entity => entity.Has<BoxTag>());
            simulation.Send(MoveCommand.Right);
            simulation.Tick(0.25f);
            Assert.That(player.Get<GridPosition>(), Is.EqualTo(new GridPosition(push ? int.MaxValue - 1 : int.MaxValue, 1)));
            Assert.That(box.Get<GridPosition>(), Is.EqualTo(new GridPosition(push ? int.MaxValue : 1, 1)));
            Assert.That(simulation.Result.Moves, Is.EqualTo(1));
            GridPosition playerPosition = player.Get<GridPosition>();
            GridPosition boxPosition = box.Get<GridPosition>();
            simulation.Send(MoveCommand.Right);
            simulation.Tick(0.25f);
            Assert.That(player.Get<GridPosition>(), Is.EqualTo(playerPosition));
            Assert.That(box.Get<GridPosition>(), Is.EqualTo(boxPosition));
            Assert.That(simulation.Result.Moves, Is.EqualTo(1));
            Assert.That(simulation.RemainingSeconds, Is.EqualTo(59.5f));
            Assert.That(player.Has<MoveRequest>() || player.Has<MovementPlan>(), Is.False);
            Assert.That(simulation.IsCompleted || simulation.IsTimedOut, Is.False);
            simulation.Tick(0f);
            Assert.That(simulation.Result.Moves, Is.EqualTo(1));
            Assert.That(player.Get<GridPosition>(), Is.EqualTo(playerPosition));
            Assert.That(box.Get<GridPosition>(), Is.EqualTo(boxPosition));
        }

        private static void Step(Simulation simulation, MoveCommand command)
        {
            simulation.Send(command);
            simulation.Tick(0f);
        }
    }
}
