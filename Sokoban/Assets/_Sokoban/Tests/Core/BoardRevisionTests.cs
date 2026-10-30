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
            using var simulation = new Simulation(LevelParser.Parse("#######\n#@-$-.#\n#######"), new LevelPopulator(), 60f, new SimulationPipelineFactory());
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
        public void PlayerPosition_TracksCommittedMovesAndUndoWithoutChangingStateOrRecording()
        {
            using var simulation = new Simulation(LevelParser.Parse("#######\n#@-$-.#\n#######"), new LevelPopulator(), 60f, new SimulationPipelineFactory());
            AssertPosition(new GridPosition(1, 1));
            simulation.Send(MoveCommand.Right);
            AssertPosition(new GridPosition(1, 1));
            simulation.Tick(0f);
            AssertPosition(new GridPosition(2, 1));
            simulation.Send(MoveCommand.Right);
            simulation.Tick(0f);
            AssertPosition(new GridPosition(3, 1));
            Assert.That(simulation.RequestUndo(), Is.True);
            AssertPosition(new GridPosition(3, 1));
            simulation.Tick(0f);
            AssertPosition(new GridPosition(2, 1));
            Assert.That(simulation.RequestUndo(), Is.True);
            simulation.Tick(0f);
            AssertPosition(new GridPosition(1, 1));

            void AssertPosition(GridPosition expected)
            {
                GameplaySnapshot before = simulation.CaptureSnapshot();
                SimulationRecording recording = simulation.ExportRecording();
                Assert.That(simulation.PlayerPosition, Is.EqualTo(expected));
                Assert.That(simulation.PlayerPosition, Is.EqualTo(before.PlayerPosition));
                Assert.That(simulation.CaptureSnapshot(), Is.EqualTo(before));
                Assert.That(simulation.ExportRecording().Inputs, Is.EqualTo(recording.Inputs));
            }
        }

        [Test]
        public void RejectedOverflowInvalidAndIdleInputs_DoNotAdvanceRevision()
        {
            var level = new LevelDefinition(3, 3, new GridPosition(int.MaxValue, 1), Array.Empty<GridPosition>(),
                new[] { new GridPosition(1, 1) }, new[] { new GridPosition(0, 0) });
            using var simulation = new Simulation(level, new LevelPopulator(), 60f, new SimulationPipelineFactory());
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
