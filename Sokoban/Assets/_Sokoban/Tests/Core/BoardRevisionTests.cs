using System;
using NUnit.Framework;
using Sokoban.Core;
using Sokoban.Core.Commands;
using Sokoban.Core.Components;

namespace Sokoban.CoreTests
{
    public sealed class BoardRevisionTests
    {
        [Test]
        public void WalkPushAndUndo_AdvanceRevisionEvenWhenReturningToIdenticalPositions()
        {
            using var simulation = new Simulation(LevelParser.Parse("#######\n#@-$-.#\n#######"), new LevelPopulator(), 60f);
            GameplaySnapshot initial = simulation.CaptureSnapshot();
            Assert.That(simulation.BoardRevision, Is.Zero);
            simulation.Send(MoveCommand.Right);
            simulation.Tick(0f);
            Assert.That(simulation.BoardRevision, Is.EqualTo(1));
            simulation.Send(MoveCommand.Right);
            simulation.Tick(0f);
            Assert.That(simulation.BoardRevision, Is.EqualTo(2));
            simulation.RequestUndo();
            simulation.Tick(0f);
            simulation.RequestUndo();
            simulation.Tick(0f);
            GameplaySnapshot returned = simulation.CaptureSnapshot();
            Assert.That(returned.PlayerPosition, Is.EqualTo(initial.PlayerPosition));
            Assert.That(returned.BoxPositions, Is.EqualTo(initial.BoxPositions));
            Assert.That(returned.Moves, Is.EqualTo(initial.Moves));
            Assert.That(simulation.BoardRevision, Is.EqualTo(4));
            Assert.That(returned.BoardRevision, Is.EqualTo(4));
            using var replay = new SimulationReplay(simulation.ExportRecording());
            replay.Run();
            Assert.That(replay.Simulation.CaptureSnapshot(), Is.EqualTo(returned));
        }

        [Test]
        public void RejectedOverflowInvalidAndIdleInputs_DoNotAdvanceRevision()
        {
            var level = new LevelDefinition(3, 3, new GridPosition(int.MaxValue, 1), Array.Empty<GridPosition>(),
                new[] { new GridPosition(1, 1) }, new[] { new GridPosition(0, 0) });
            using var simulation = new Simulation(level, new LevelPopulator(), 60f);
            simulation.Send(MoveCommand.Right);
            simulation.Tick(0f);
            simulation.Send((MoveCommand)99);
            simulation.RequestUndo();
            simulation.Tick(0.25f);
            simulation.Tick(0f);
            Assert.That(simulation.BoardRevision, Is.Zero);
            Assert.That(simulation.RemainingSeconds, Is.EqualTo(59.75f));
        }
    }
}
