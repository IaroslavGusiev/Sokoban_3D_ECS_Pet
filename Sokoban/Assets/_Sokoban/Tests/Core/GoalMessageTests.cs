using System;
using System.Collections.Generic;
using System.Linq;
using DefaultEcs;
using NUnit.Framework;
using Sokoban.Core;
using Sokoban.Core.Commands;
using Sokoban.Core.Components;
using Sokoban.Core.Messages;

namespace Sokoban.CoreTests
{
    public sealed class GoalMessageTests
    {
        [Test]
        public void Arrival_PublishesOnceAfterBothMovementParticipantsCommit()
        {
            using var simulation = CreateSimulation();
            Entity player = simulation.World.Single(e => e.Has<PlayerTag>());
            var messages = new List<BoxEnteredGoal>();
            using var subscription = simulation.World.Subscribe<BoxEnteredGoal>((in BoxEnteredGoal message) =>
            {
                Assert.That(player.Get<GridPosition>(), Is.EqualTo(new GridPosition(2, 1)));
                Assert.That(message.Box.Get<GridPosition>(), Is.EqualTo(message.Goal));
                Assert.That(simulation.Result.Moves, Is.EqualTo(1));
                messages.Add(message);
            });
            simulation.Send(MoveCommand.Right);
            simulation.Tick(0f);
            simulation.Tick(0f);
            simulation.Tick(0.1f);
            Assert.That(messages.Count, Is.EqualTo(1));
            Assert.That(messages[0].Goal, Is.EqualTo(new GridPosition(3, 1)));
        }

        [Test]
        public void InitialBoxOnGoal_DoesNotPublishUntilItMovesToAnotherGoal()
        {
            using var simulation = CreateSimulation(initiallyOnGoal: true);
            var messages = new List<BoxEnteredGoal>();
            using var subscription = simulation.World.Subscribe<BoxEnteredGoal>((in BoxEnteredGoal message) => messages.Add(message));
            simulation.Tick(0f);
            Assert.That(messages, Is.Empty);
            simulation.Send(MoveCommand.Right);
            simulation.Tick(0f);
            Assert.That(messages.Single().Goal, Is.EqualTo(new GridPosition(3, 1)));
        }

        [Test]
        public void AdjacentGoalsAndReentry_PublishEachArrivalWithoutPublishingExit()
        {
            using var simulation = CreateSimulation();
            var goals = new List<GridPosition>();
            using var subscription = simulation.World.Subscribe<BoxEnteredGoal>((in BoxEnteredGoal message) => goals.Add(message.Goal));
            foreach (MoveCommand move in new[]
            {
                MoveCommand.Right, MoveCommand.Right, MoveCommand.Right,
                MoveCommand.Up, MoveCommand.Right, MoveCommand.Right, MoveCommand.Down, MoveCommand.Left
            })
            {
                simulation.Send(move);
                simulation.Tick(0f);
            }
            Assert.That(goals, Is.EqualTo(new[] { new GridPosition(3, 1), new GridPosition(4, 1), new GridPosition(4, 1) }));
        }

        [Test]
        public void BlockedPush_DoesNotPublish()
        {
            var level = new LevelDefinition(5, 3, new GridPosition(1, 1),
                new[] { new GridPosition(3, 1) }, new[] { new GridPosition(2, 1) }, new[] { new GridPosition(3, 1) });
            using var simulation = new Simulation(level, new LevelPopulator(), 60f);
            int messages = 0;
            using var subscription = simulation.World.Subscribe<BoxEnteredGoal>((in BoxEnteredGoal _) => messages++);
            simulation.Send(MoveCommand.Right);
            simulation.Tick(0f);
            Assert.That(messages, Is.Zero);
        }

        [Test]
        public void WinningArrival_OnExpiryTickStillPublishesAndWins()
        {
            using var simulation = new Simulation(LevelParser.Parse("#####\n#@$.#\n#####"), new LevelPopulator(), 1f);
            int messages = 0;
            using var subscription = simulation.World.Subscribe<BoxEnteredGoal>((in BoxEnteredGoal _) => messages++);
            simulation.Send(MoveCommand.Right);
            simulation.Tick(1f);
            Assert.That(messages, Is.EqualTo(1));
            Assert.That(simulation.IsCompleted, Is.True);
            Assert.That(simulation.IsTimedOut, Is.False);
        }

        private static Simulation CreateSimulation(bool initiallyOnGoal = false) =>
            new(new LevelDefinition(8, 6, new GridPosition(1, 1), Array.Empty<GridPosition>(),
                new[] { new GridPosition(2, 1), new GridPosition(0, 4), new GridPosition(1, 4) },
                new[] { new GridPosition(initiallyOnGoal ? 2 : 3, 1), new GridPosition(initiallyOnGoal ? 3 : 4, 1), new GridPosition(6, 3) }),
                new LevelPopulator(), 60f);
    }
}
