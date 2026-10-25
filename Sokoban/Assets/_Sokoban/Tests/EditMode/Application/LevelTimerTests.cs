using System;
using System.Linq;
using DefaultEcs;
using NUnit.Framework;
using Sokoban.Core;
using Sokoban.Core.Commands;
using Sokoban.Core.Components;

namespace Sokoban.Tests.App
{
    public sealed class LevelTimerTests
    {
        [Test]
        public void Tick_ConsumesOnlySuppliedGameplayTime()
        {
            using var simulation = Create(1f);
            Assert.That(simulation.RemainingSeconds, Is.EqualTo(1f));
            simulation.Tick(0f);
            Assert.That(simulation.RemainingSeconds, Is.EqualTo(1f));
            simulation.Tick(0.25f);
            simulation.Tick(0.25f);
            Assert.That(simulation.RemainingSeconds, Is.EqualTo(0.5f));
            Assert.That(simulation.IsTimedOut, Is.False);
        }

        [TestCase(1f)]
        [TestCase(2f)]
        public void Deadline_WinningMoveSucceedsOnFinalTick(float deltaTime)
        {
            using var simulation = Create(1f);
            simulation.Send(MoveCommand.Right);
            simulation.Tick(deltaTime);
            Assert.That(simulation.RemainingSeconds, Is.EqualTo(1f));
            Assert.That(simulation.IsCompleted, Is.True);
            Assert.That(simulation.IsTimedOut, Is.False);
            var box = simulation.World.Single(entity => entity.Has<BoxTag>());
            var goal = simulation.World.Single(entity => entity.Has<GoalTag>());
            Assert.That(box.Get<GridPosition>(), Is.EqualTo(goal.Get<GridPosition>()));
            simulation.Tick(10f);
            Assert.That(simulation.IsCompleted, Is.True);
            Assert.That(simulation.IsTimedOut, Is.False);
            Assert.That(simulation.RemainingSeconds, Is.EqualTo(1f));
        }

        [TestCase(1f)]
        [TestCase(2f)]
        public void Deadline_UnsolvedLevelLatchesTimeout(float deltaTime)
        {
            using var simulation = Create(1f);
            var player = simulation.World.Single(entity => entity.Has<PlayerTag>());
            var box = simulation.World.Single(entity => entity.Has<BoxTag>());
            GridPosition playerStart = player.Get<GridPosition>();
            GridPosition boxStart = box.Get<GridPosition>();
            simulation.Send(MoveCommand.Left);
            simulation.Tick(deltaTime);
            Assert.That(simulation.RemainingSeconds, Is.Zero);
            Assert.That(simulation.IsTimedOut, Is.True);
            Assert.That(simulation.IsCompleted, Is.False);
            Assert.That(player.Has<MoveRequest>(), Is.False);
            Assert.That(player.Has<MovementPlan>(), Is.False);
            Assert.That(player.Get<GridPosition>(), Is.EqualTo(playerStart));
            Assert.That(box.Get<GridPosition>(), Is.EqualTo(boxStart));
            simulation.Send(MoveCommand.Right);
            simulation.Tick(10f);
            Assert.That(player.Has<MoveRequest>(), Is.False);
            Assert.That(player.Get<GridPosition>(), Is.EqualTo(playerStart));
            Assert.That(simulation.RemainingSeconds, Is.Zero);
            Assert.That(simulation.IsCompleted, Is.False);
        }

        [Test]
        public void CompletionBeforeDeadline_FreezesRemainingTime()
        {
            using var simulation = Create(1f);
            simulation.Send(MoveCommand.Right);
            simulation.Tick(0.75f);
            Assert.That(simulation.IsCompleted, Is.True);
            Assert.That(simulation.RemainingSeconds, Is.EqualTo(1f));
            simulation.Tick(20f);
            Assert.That(simulation.IsTimedOut, Is.False);
            Assert.That(simulation.RemainingSeconds, Is.EqualTo(1f));
        }

        [Test]
        public void ZeroDelta_DoesNotPublishTimerChanges()
        {
            using var simulation = Create(1f);
            using EntitySet changed = simulation.World.GetEntities().WhenChanged<LevelTimer>().AsSet();
            simulation.Tick(0f);
            Assert.That(changed.Count, Is.Zero);
            simulation.Tick(0.25f);
            Assert.That(changed.Count, Is.EqualTo(1));
            changed.Complete();
            simulation.Tick(0f);
            Assert.That(changed.Count, Is.Zero);
        }

        [Test]
        public void Completion_RejectsLaterMovementCommands()
        {
            using var simulation = Create(1f);
            simulation.Send(MoveCommand.Right);
            simulation.Tick(0.25f);
            Entity player = simulation.World.Single(entity => entity.Has<PlayerTag>());
            GridPosition position = player.Get<GridPosition>();
            simulation.Send(MoveCommand.Left);
            Assert.That(player.Has<MoveRequest>(), Is.False);
            simulation.Tick(10f);
            Assert.That(player.Get<GridPosition>(), Is.EqualTo(position));
            Assert.That(simulation.RemainingSeconds, Is.EqualTo(1f));
        }

        [Test]
        public void Expiration_RemovesRequestsAndPlansStillPendingAtTimerUpdate()
        {
            using var simulation = Create(1f);
            Entity player = simulation.World.Single(entity => entity.Has<PlayerTag>());
            using var subscription = simulation.World.SubscribeComponentChanged<LevelTimer>(
                (in Entity entity, in LevelTimer previous, in LevelTimer current) =>
                {
                    entity.Set(new MoveRequest(MoveCommand.Left));
                    entity.Set(default(MovementPlan));
                });
            simulation.Tick(1f);
            Assert.That(simulation.IsTimedOut, Is.True);
            Assert.That(simulation.RemainingSeconds, Is.Zero);
            Assert.That(player.Has<MoveRequest>(), Is.False);
            Assert.That(player.Has<MovementPlan>(), Is.False);
        }

        [Test]
        public void NewSimulation_ResetsTimerWithoutChangingPreviousOutcome()
        {
            var populator = new LevelPopulator();
            LevelDefinition level = LevelParser.Parse("#####\n#@$.#\n#####");
            using var expired = new Simulation(level, populator, 1f);
            expired.Tick(1f);
            using var fresh = new Simulation(level, populator, 3f);
            fresh.Tick(0.5f);
            Assert.That(fresh.World, Is.Not.SameAs(expired.World));
            Assert.That(fresh.RemainingSeconds, Is.EqualTo(2.5f));
            Assert.That(fresh.IsTimedOut, Is.False);
            Assert.That(expired.IsTimedOut, Is.True);
            Assert.That(expired.RemainingSeconds, Is.Zero);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidDuration_IsRejected(float duration)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(duration));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidTick_DoesNotChangeTimer(float deltaTime)
        {
            using var simulation = Create(1f);
            Assert.Throws<ArgumentOutOfRangeException>(() => simulation.Tick(deltaTime));
            Assert.That(simulation.RemainingSeconds, Is.EqualTo(1f));
            Assert.That(simulation.IsTimedOut, Is.False);
        }

        private static Simulation Create(float duration) =>
            new(LevelParser.Parse("#####\n#@$.#\n#####"), new LevelPopulator(), duration);
    }
}
