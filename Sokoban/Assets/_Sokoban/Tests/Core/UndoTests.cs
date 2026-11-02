using System;
using System.Linq;
using System.Collections.Generic;
using DefaultEcs;
using NUnit.Framework;
using Sokoban.Core;
using Sokoban.Core.Commands;
using Sokoban.Core.Components;
using Sokoban.Core.Messages;

namespace Sokoban.CoreTests
{
    public sealed class UndoTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void WalkAndPush_RestoreParticipantsAndCountWithoutReplacingEntities(bool push)
        {
            using var simulation = Create();
            Entity player = Player(simulation);
            Entity box = Box(simulation);
            Entity[] entities = simulation.World.ToArray();
            Step(simulation, MoveCommand.Right);
            if (push)
            {
                Step(simulation, MoveCommand.Right);
            }
            Assert.That(simulation.CanUndo, Is.True);
            Assert.That(simulation.RequestUndo(), Is.True);
            simulation.Tick(0.25f);
            Assert.That(player.Get<GridPosition>(), Is.EqualTo(new GridPosition(push ? 2 : 1, 1)));
            Assert.That(box.Get<GridPosition>(), Is.EqualTo(new GridPosition(3, 1)));
            Assert.That(simulation.Result.Moves, Is.EqualTo(push ? 1 : 0));
            Assert.That(simulation.World.ToArray(), Is.EqualTo(entities));
            Step(simulation, MoveCommand.Right);
            Assert.That(simulation.Result.Moves, Is.EqualTo(push ? 2 : 1));
        }

        [Test]
        public void RepeatedUndo_ConsumesOneSuccessfulMoveAtATimeAndEmptyRequestsAreDiscarded()
        {
            using var simulation = Create();
            Assert.That(simulation.RequestUndo(), Is.False);
            Step(simulation, MoveCommand.Right);
            Step(simulation, MoveCommand.Right);
            Step(simulation, MoveCommand.Right);
            for (int count = 2; count >= 0; count--)
            {
                Assert.That(simulation.RequestUndo(), Is.True);
                Assert.That(simulation.RequestUndo(), Is.True);
                simulation.Tick(0f);
                Assert.That(simulation.Result.Moves, Is.EqualTo(count));
            }
            Assert.That(Player(simulation).Get<GridPosition>(), Is.EqualTo(new GridPosition(1, 1)));
            Assert.That(Box(simulation).Get<GridPosition>(), Is.EqualTo(new GridPosition(3, 1)));
            Assert.That(simulation.CanUndo, Is.False);
            Assert.That(simulation.RequestUndo(), Is.False);
            Step(simulation, MoveCommand.Right);
            Assert.That(simulation.Result.Moves, Is.EqualTo(1));
            Assert.That(simulation.CanUndo, Is.True);
        }

        [Test]
        public void BlockedAndInvalidMoves_DoNotReplaceTheLatestSuccessfulHistoryEntry()
        {
            using var simulation = new Simulation(LevelParser.Parse("########\n#@-$#-.#\n########"), new LevelPopulator(), 60f, new SimulationPipelineFactory());
            Step(simulation, MoveCommand.Left);
            Step(simulation, (MoveCommand)99);
            Assert.That(simulation.CanUndo, Is.False);
            Step(simulation, MoveCommand.Right);
            Step(simulation, MoveCommand.Right);
            Assert.That(simulation.Result.Moves, Is.EqualTo(1));
            simulation.RequestUndo();
            simulation.Tick(0f);
            Assert.That(Player(simulation).Get<GridPosition>(), Is.EqualTo(new GridPosition(1, 1)));
            Assert.That(simulation.CanUndo, Is.False);
        }

        [Test]
        public void OverflowRejectedMove_CreatesNoHistory()
        {
            var level = new LevelDefinition(3, 3, new GridPosition(int.MaxValue, 1), Array.Empty<GridPosition>(),
                new[] { new GridPosition(1, 1) }, new[] { new GridPosition(0, 0) });
            using var simulation = new Simulation(level, new LevelPopulator(), 60f, new SimulationPipelineFactory());
            Step(simulation, MoveCommand.Right);
            Assert.That(simulation.CanUndo, Is.False);
            Step(simulation, MoveCommand.Left);
            Step(simulation, MoveCommand.Right);
            Step(simulation, MoveCommand.Right);
            simulation.RequestUndo();
            simulation.Tick(0f);
            Assert.That(Player(simulation).Get<GridPosition>(), Is.EqualTo(new GridPosition(int.MaxValue - 1, 1)));
            Assert.That(simulation.Result.Moves, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UndoAndForwardMovement_AreMutuallyExclusiveRegardlessOfSubmissionOrder(bool moveFirst)
        {
            using var simulation = Create();
            Step(simulation, MoveCommand.Right);
            if (moveFirst)
            {
                simulation.Send(MoveCommand.Right);
            }
            simulation.RequestUndo();
            if (!moveFirst)
            {
                simulation.Send(MoveCommand.Right);
            }
            simulation.Tick(0.25f);
            Assert.That(simulation.Result.Moves, Is.Zero);
            Assert.That(Player(simulation).Get<GridPosition>(), Is.EqualTo(new GridPosition(1, 1)));
            Assert.That(Player(simulation).Has<MoveRequest>() || Player(simulation).Has<MovementPlan>(), Is.False);
            simulation.Tick(0f);
            Assert.That(simulation.Result.Moves, Is.Zero);
            Assert.That(simulation.CanUndo, Is.False);
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidTick_PreservesPendingUndoAndTimeThenValidUndoContinuesCountdown(float delta)
        {
            using var simulation = Create();
            Step(simulation, MoveCommand.Right, 0.5f);
            simulation.RequestUndo();
            Assert.Throws<ArgumentOutOfRangeException>(() => simulation.Tick(delta));
            Entity level = simulation.World.Single(entity => entity.Has<LevelStateTag>());
            Assert.That(level.Has<UndoRequest>(), Is.True);
            Assert.That(simulation.Result.Moves, Is.EqualTo(1));
            Assert.That(Player(simulation).Get<GridPosition>(), Is.EqualTo(new GridPosition(2, 1)));
            Assert.That(simulation.RemainingSeconds, Is.EqualTo(59.5f));
            simulation.Tick(0.25f);
            Assert.That(simulation.RemainingSeconds, Is.EqualTo(59.25f));
            Assert.That(simulation.Result.ElapsedSeconds, Is.EqualTo(0.75f));
            Assert.That(simulation.Result.Moves, Is.Zero);
            Assert.That(level.Has<UndoRequest>(), Is.False);
        }

        [TestCase("player disabled")]
        [TestCase("player position")]
        [TestCase("box position")]
        [TestCase("box disabled")]
        [TestCase("position removed")]
        [TestCase("destination occupied")]
        public void ReversePreflightFailure_PreservesAllPositionsAndHistoryUntilRetry(string fault)
        {
            using var simulation = Create();
            Step(simulation, MoveCommand.Right);
            Step(simulation, MoveCommand.Right);
            Entity player = Player(simulation);
            Entity box = Box(simulation);
            if (fault == "player disabled")
            {
                player.Disable();
            }
            if (fault == "player position")
            {
                player.Set(new GridPosition(99, 99));
            }
            if (fault == "box position")
            {
                box.Set(new GridPosition(99, 99));
            }
            if (fault == "box disabled")
            {
                box.Disable();
            }
            if (fault == "position removed")
            {
                box.Remove<GridPosition>();
            }
            Entity obstruction = default;
            if (fault == "destination occupied")
            {
                obstruction = simulation.World.CreateEntity();
                obstruction.Set<WallTag>();
                obstruction.Set(new GridPosition(2, 1));
            }
            GridPosition playerBefore = player.Get<GridPosition>();
            int writes = 0;
            using var subscription = simulation.World.SubscribeComponentChanged<GridPosition>(
                (in Entity entity, in GridPosition previous, in GridPosition current) => writes++);
            simulation.RequestUndo();
            simulation.Tick(0f);
            Assert.That(writes, Is.Zero);
            Assert.That(player.Get<GridPosition>(), Is.EqualTo(playerBefore));
            Assert.That(simulation.Result.Moves, Is.EqualTo(2));
            Assert.That(simulation.CanUndo, Is.True);
            Assert.That(simulation.BoardRevision, Is.EqualTo(2));
            player.Set(new GridPosition(3, 1));
            player.Enable();
            box.Enable();
            box.Set(new GridPosition(4, 1));
            if (fault == "destination occupied")
            {
                obstruction.Dispose();
            }
            simulation.RequestUndo();
            simulation.Tick(0f);
            Assert.That(player.Get<GridPosition>(), Is.EqualTo(new GridPosition(2, 1)));
            Assert.That(box.Get<GridPosition>(), Is.EqualTo(new GridPosition(3, 1)));
            Assert.That(simulation.Result.Moves, Is.EqualTo(1));
            Assert.That(simulation.BoardRevision, Is.EqualTo(3));
        }

        [Test]
        public void UndoReturningBoxToGoal_SuppressesArrivalAndResynchronizesTheNextForwardArrival()
        {
            using var simulation = new Simulation(LevelParser.Parse("#########\n#@$..-$-#\n#########"), new LevelPopulator(), 60f, new SimulationPipelineFactory());
            var arrivals = new List<GridPosition>();
            using var subscription = simulation.World.Subscribe<BoxEnteredGoal>((in BoxEnteredGoal message) => arrivals.Add(message.Goal));
            Step(simulation, MoveCommand.Right);
            Step(simulation, MoveCommand.Right);
            simulation.RequestUndo();
            simulation.Tick(0f);
            Assert.That(arrivals, Is.EqualTo(new[] { new GridPosition(3, 1), new GridPosition(4, 1) }));
            Step(simulation, MoveCommand.Right);
            Assert.That(arrivals, Is.EqualTo(new[] { new GridPosition(3, 1), new GridPosition(4, 1), new GridPosition(4, 1) }));
            Assert.That(simulation.Result.Moves, Is.EqualTo(2));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TerminalAttempts_RejectUndo(bool completed)
        {
            using var simulation = completed
                ? new Simulation(LevelParser.Parse("#####\n#@$.#\n#####"), new LevelPopulator(), 1f, new SimulationPipelineFactory())
                : Create();
            Step(simulation, MoveCommand.Right);
            if (completed == false)
            {
                Assert.That(simulation.CanUndo, Is.True);
                simulation.Tick(60f);
            }
            Assert.That(simulation.IsCompleted, Is.EqualTo(completed));
            Assert.That(simulation.IsTimedOut, Is.EqualTo(!completed));
            Assert.That(simulation.CanUndo, Is.False);
            Assert.That(simulation.RequestUndo(), Is.False);
            Assert.That(simulation.World.Single(entity => entity.Has<LevelStateTag>()).Has<UndoRequest>(), Is.False);
        }

        [Test]
        public void ReplacementSimulation_StartsWithEmptyHistory()
        {
            var previous = Create();
            Step(previous, MoveCommand.Right);
            Assert.That(previous.CanUndo, Is.True);
            World world = previous.World;
            previous.Dispose();
            using var replacement = Create();
            Assert.That(replacement.World, Is.Not.SameAs(world));
            Assert.That(replacement.CanUndo, Is.False);
            Assert.That(replacement.RequestUndo(), Is.False);
        }

        private static Simulation Create() =>
            new(LevelParser.Parse("########\n#@-$--.#\n########"), new LevelPopulator(), 60f, new SimulationPipelineFactory());

        private static Entity Player(Simulation simulation) => simulation.World.Single(entity => entity.Has<PlayerTag>());
        private static Entity Box(Simulation simulation) => simulation.World.Single(entity => entity.Has<BoxTag>());

        private static void Step(Simulation simulation, MoveCommand command, float delta = 0f)
        {
            simulation.Send(command);
            simulation.Tick(delta);
        }
    }
}
