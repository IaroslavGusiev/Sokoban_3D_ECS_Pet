using System;
using System.Linq;
using System.Collections.Generic;
using DefaultEcs;
using NUnit.Framework;
using Sokoban.Core;
using Sokoban.Core.Commands;
using Sokoban.Core.Components;

namespace Sokoban.CoreTests
{
    public sealed class RecordingReplayTests
    {
        [Test]
        public void WalkPushBlockedIdleAndZeroTicks_ReplayEveryTickWithExactDeltas()
        {
            using var source = Create();
            var ticks = new List<GameplaySnapshot>();
            float negativeZero = BitConverter.ToSingle(BitConverter.GetBytes(int.MinValue), 0);
            Tick(source, ticks, negativeZero);
            source.Send(MoveCommand.Left);
            Tick(source, ticks, 1f / 60f);
            source.Send(MoveCommand.Right);
            Tick(source, ticks, 0f);
            source.Send((MoveCommand)99);
            Tick(source, ticks, 0.1234567f);
            source.Send(MoveCommand.Right);
            Tick(source, ticks, 0.03125f);
            Tick(source, ticks, 0.17f);
            Assert.That(source.Result.Moves, Is.EqualTo(2));
            Assert.That(source.CaptureSnapshot().PlayerPosition, Is.EqualTo(new GridPosition(3, 1)));
            float[] deltas = source.ExportRecording().Inputs.Where(input => input.Kind == SimulationInputKind.Tick)
                .Select(input => input.DeltaTime).ToArray();
            float[] expected = { negativeZero, 1f / 60f, 0f, 0.1234567f, 0.03125f, 0.17f };
            Assert.That(deltas.Select(Bits), Is.EqualTo(expected.Select(Bits)));
            AssertReplay(source, ticks);
        }

        [TestCase("completion")]
        [TestCase("final tick")]
        [TestCase("timeout")]
        [TestCase("undo expiry")]
        public void TerminalRulesAndRejectedTerminalRequests_ReplayExactly(string scenario)
        {
            using var source = scenario == "undo expiry" ? Create(1f)
                : new Simulation(LevelParser.Parse("#####\n#@$.#\n#####"), new LevelPopulator(), 1f);
            var ticks = new List<GameplaySnapshot>();
            Tick(source, ticks, 0.25f);
            source.Send(scenario == "timeout" ? MoveCommand.Left : MoveCommand.Right);
            if (scenario == "undo expiry")
            {
                Tick(source, ticks, 0.125f);
                Assert.That(source.RequestUndo(), Is.True);
            }
            Tick(source, ticks, scenario == "completion" ? 0.125f : 2f);
            bool completed = scenario == "completion" || scenario == "final tick";
            Assert.That(source.IsCompleted, Is.EqualTo(completed));
            Assert.That(source.IsTimedOut, Is.EqualTo(!completed));
            Assert.That(source.RequestUndo(), Is.False);
            source.Send(MoveCommand.Left);
            Tick(source, ticks, 0f);
            Tick(source, ticks, 0.5f);
            Assert.That(source.RemainingSeconds, Is.EqualTo(completed ? 0.75f : 0f));
            AssertReplay(source, ticks);
        }

        [Test]
        public void UndoBranchingAndRepeatedUndo_ReconstructHistoryThroughRules()
        {
            using var source = Create();
            var ticks = new List<GameplaySnapshot>();
            source.Send(MoveCommand.Right);
            Tick(source, ticks, 0.1f);
            source.Send(MoveCommand.Right);
            Tick(source, ticks, 0.1f);
            source.RequestUndo();
            Tick(source, ticks, 0.1f);
            source.Send(MoveCommand.Left);
            Tick(source, ticks, 0f);
            source.RequestUndo();
            Tick(source, ticks, 0f);
            source.RequestUndo();
            Tick(source, ticks, 0f);
            Assert.That(source.Result.Moves, Is.Zero);
            Assert.That(source.CanUndo, Is.False);
            Assert.That(source.RequestUndo(), Is.False);
            source.Send(MoveCommand.Right);
            Tick(source, ticks, 0.05f);
            Assert.That(source.Result.Moves, Is.EqualTo(1));
            Assert.That(source.CaptureSnapshot().BoxPositions.Single(), Is.EqualTo(new GridPosition(3, 1)));
            AssertReplay(source, ticks);
        }

        [Test]
        public void RequestsBetweenTicks_RetainOrderingAndCoalescingIncludingMoveThenUndo()
        {
            using var source = Create();
            var ticks = new List<GameplaySnapshot>();
            source.Send(MoveCommand.Right);
            Tick(source, ticks, 0f);
            source.Send(MoveCommand.Right);
            source.Send(MoveCommand.Up);
            source.RequestUndo();
            source.RequestUndo();
            source.Send(MoveCommand.Left);
            Tick(source, ticks, 0f);
            Assert.That(source.Result.Moves, Is.Zero);
            Assert.That(source.CaptureSnapshot().PlayerPosition, Is.EqualTo(new GridPosition(1, 1)));
            source.RequestUndo();
            source.Send((MoveCommand)0);
            source.Send(MoveCommand.Right);
            source.Send(MoveCommand.Up);
            Tick(source, ticks, 0f);
            Assert.That(source.Result.Moves, Is.EqualTo(1));
            Assert.That(source.ExportRecording().Inputs.Select(input => input.Kind), Is.EqualTo(new[]
            {
                SimulationInputKind.Move, SimulationInputKind.Tick,
                SimulationInputKind.Move, SimulationInputKind.Move, SimulationInputKind.Undo,
                SimulationInputKind.Undo, SimulationInputKind.Move, SimulationInputKind.Tick,
                SimulationInputKind.Undo, SimulationInputKind.Move, SimulationInputKind.Move,
                SimulationInputKind.Move, SimulationInputKind.Tick
            }));
            AssertReplay(source, ticks);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Endpoint_PreservesPendingMovementOrUndoWithoutAddingATick(bool undo)
        {
            using var source = Create();
            source.Send(MoveCommand.Right);
            source.Tick(0.25f);
            source.Send(MoveCommand.Right);
            if (undo) source.RequestUndo();
            else source.Send(MoveCommand.Up);
            SimulationRecording recording = source.ExportRecording();
            Assert.That(recording.Inputs.Last().Kind, Is.EqualTo(undo ? SimulationInputKind.Undo : SimulationInputKind.Move));
            using var replay = new SimulationReplay(recording);
            replay.Run();
            Assert.That(replay.Simulation.CaptureSnapshot(), Is.EqualTo(source.CaptureSnapshot()));
            Assert.That(replay.Simulation.Result.Moves, Is.EqualTo(1));
            source.Tick(0.125f);
            replay.Simulation.Tick(0.125f);
            Assert.That(replay.Simulation.CaptureSnapshot(), Is.EqualTo(source.CaptureSnapshot()));
            Assert.That(source.Result.Moves, Is.EqualTo(undo ? 0 : 2));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidTicks_DoNotEnterTheRecordingOrLosePendingWork(float delta)
        {
            using var source = Create();
            source.Send(MoveCommand.Right);
            source.Tick(0.25f);
            source.Send(MoveCommand.Right);
            source.RequestUndo();
            GameplaySnapshot before = source.CaptureSnapshot();
            SimulationRecording prefix = source.ExportRecording();
            Assert.Throws<ArgumentOutOfRangeException>(() => source.Tick(delta));
            Assert.That(source.CaptureSnapshot(), Is.EqualTo(before));
            Assert.That(source.ExportRecording().Inputs.Count, Is.EqualTo(prefix.Inputs.Count));
            using var replay = new SimulationReplay(prefix);
            replay.Run();
            source.Tick(0.125f);
            replay.Simulation.Tick(0.125f);
            Assert.That(replay.Simulation.CaptureSnapshot(), Is.EqualTo(source.CaptureSnapshot()));
            Assert.That(source.Result.Moves, Is.Zero);
            Assert.That(source.RemainingSeconds, Is.EqualTo(59.625f));
            Assert.That(prefix.Inputs.Count, Is.EqualTo(4));
            Assert.That(source.ExportRecording().Inputs.Count, Is.EqualTo(5));
        }

        [Test]
        public void Exports_CopyLevelAndInputsRemainImmutableAndSurviveSourceDisposal()
        {
            LevelDefinition level = LevelParser.Parse("########\n#@-$--.#\n########");
            SimulationRecording early;
            SimulationRecording later;
            GameplaySnapshot expected;
            Simulation disposed;
            using (var source = new Simulation(level, new LevelPopulator(), 60f))
            {
                disposed = source;
                source.Send(MoveCommand.Right);
                source.Tick(0.1f);
                early = source.ExportRecording();
                expected = source.CaptureSnapshot();
                source.Send(MoveCommand.Right);
                source.Tick(0.2f);
                later = source.ExportRecording();
                Assert.That(early.Inputs.Count, Is.EqualTo(2));
                Assert.That(later.Inputs.Count, Is.EqualTo(4));
                Assert.That(early.InitialLevel, Is.Not.SameAs(level));
                Assert.That(early.InitialLevel.Boxes, Is.Not.SameAs(level.Boxes));
                Assert.That(early.TimeLimitSeconds, Is.EqualTo(60f));
                Assert.Throws<NotSupportedException>(() => ((IList<SimulationInput>)early.Inputs)[0] = default);
                Assert.Throws<NotSupportedException>(() => ((IList<GridPosition>)early.InitialLevel.Boxes)[0] = default);
            }
            Assert.Throws<InvalidOperationException>(() => disposed.ExportRecording());
            using var replay = new SimulationReplay(early);
            replay.Run();
            Assert.That(replay.Simulation.CaptureSnapshot(), Is.EqualTo(expected));
            Assert.That(early.Inputs.Count, Is.EqualTo(2));
            using var laterReplay = new SimulationReplay(later);
            laterReplay.Run();
            Assert.That(laterReplay.Simulation.Result.Moves, Is.EqualTo(2));
            Assert.That(expected.Moves, Is.EqualTo(1));
        }

        [Test]
        public void EmptyRecording_PreservesInitialStateWithoutAnImplicitTick()
        {
            var level = new LevelDefinition(3, 3, new GridPosition(1, 1), Array.Empty<GridPosition>(),
                new[] { new GridPosition(1, 2) }, new[] { new GridPosition(1, 2) });
            using var source = new Simulation(level, new LevelPopulator(), 60f);
            SimulationRecording recording = source.ExportRecording();
            Assert.That(recording.Inputs, Is.Empty);
            using var replay = new SimulationReplay(recording);
            Assert.That(replay.IsFinished, Is.True);
            replay.Run();
            Assert.That(replay.Simulation.CaptureSnapshot(), Is.EqualTo(source.CaptureSnapshot()));
            Assert.That(replay.Simulation.IsCompleted, Is.False);
            source.Tick(0f);
            replay.Simulation.Tick(0f);
            Assert.That(replay.Simulation.IsCompleted, Is.True);
            Assert.That(replay.Simulation.CaptureSnapshot(), Is.EqualTo(source.CaptureSnapshot()));
        }

        [Test]
        public void MultipleReplays_AreIndependentAndDoNotRecordTheirOwnExecution()
        {
            using var source = Create();
            source.Send(MoveCommand.Right);
            source.Tick(0.125f);
            GameplaySnapshot before = source.CaptureSnapshot();
            SimulationRecording recording = source.ExportRecording();
            using var first = new SimulationReplay(recording);
            using var second = new SimulationReplay(recording);
            first.Run();
            second.Run();
            Assert.That(first.Simulation.World, Is.Not.SameAs(second.Simulation.World));
            Assert.That(first.Simulation.World, Is.Not.SameAs(source.World));
            Assert.That(first.Simulation.CaptureSnapshot(), Is.EqualTo(second.Simulation.CaptureSnapshot()));
            Assert.Throws<InvalidOperationException>(() => first.Simulation.ExportRecording());
            first.Simulation.RequestUndo();
            first.Simulation.Tick(0.25f);
            Assert.That(first.Simulation.Result.Moves, Is.Zero);
            Assert.That(second.Simulation.CaptureSnapshot(), Is.EqualTo(before));
            Assert.That(source.CaptureSnapshot(), Is.EqualTo(before));
            Assert.That(recording.Inputs.Count, Is.EqualTo(2));
            first.Dispose();
            Assert.DoesNotThrow(() => first.Dispose());
            second.Simulation.Tick(0.5f);
            Assert.That(source.CaptureSnapshot(), Is.EqualTo(before));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RejectedMoves_ReplayTheCorrectHistoryDepth(bool overflow)
        {
            var level = overflow
                ? new LevelDefinition(3, 3, new GridPosition(int.MaxValue, 1), Array.Empty<GridPosition>(),
                    new[] { new GridPosition(1, 1) }, new[] { new GridPosition(0, 0) })
                : LevelParser.Parse("########\n#@-$#-.#\n########");
            using var source = new Simulation(level, new LevelPopulator(), 60f);
            var ticks = new List<GameplaySnapshot>();
            source.Send(overflow ? MoveCommand.Right : MoveCommand.Left);
            Tick(source, ticks, 0f);
            Assert.That(source.CanUndo, Is.False);
            source.Send(overflow ? MoveCommand.Left : MoveCommand.Right);
            Tick(source, ticks, 0.1f);
            source.Send(MoveCommand.Right);
            Tick(source, ticks, 0.1f);
            source.Send(MoveCommand.Right);
            Tick(source, ticks, 0f);
            source.Send((MoveCommand)99);
            Tick(source, ticks, 0f);
            AssertReplay(source, ticks);
            using var replay = new SimulationReplay(source.ExportRecording());
            replay.Run();
            int undos = 0;
            while (source.CanUndo)
            {
                Assert.That(replay.Simulation.RequestUndo(), Is.EqualTo(source.RequestUndo()));
                source.Tick(0.125f);
                replay.Simulation.Tick(0.125f);
                Assert.That(replay.Simulation.CaptureSnapshot(), Is.EqualTo(source.CaptureSnapshot()));
                Assert.That(++undos, Is.LessThan(4));
            }
            Assert.That(undos, Is.EqualTo(overflow ? 2 : 1));
            Assert.That(replay.Simulation.CanUndo, Is.False);
        }

        [Test]
        public void Snapshot_SortsBoxesCopiesDataAndComparesValuesAcrossWorlds()
        {
            GridPosition[] boxes = { new(3, 2), new(1, 2), new(3, 0) };
            var level = new LevelDefinition(4, 3, new GridPosition(0, 1), Array.Empty<GridPosition>(), boxes,
                new[] { new GridPosition(0, 0), new GridPosition(1, 0), new GridPosition(2, 0) });
            var reversed = new LevelDefinition(4, 3, level.PlayerStart, level.Walls, boxes.Reverse(), level.Goals);
            using var first = new Simulation(level, new LevelPopulator(), 60f);
            using var second = new Simulation(reversed, new LevelPopulator(), 60f);
            GameplaySnapshot snapshot = first.CaptureSnapshot();
            Assert.That(snapshot.BoxPositions, Is.EqualTo(new[] { new GridPosition(1, 2), new GridPosition(3, 0), new GridPosition(3, 2) }));
            Assert.That(snapshot, Is.EqualTo(second.CaptureSnapshot()));
            Assert.That(snapshot.GetHashCode(), Is.EqualTo(second.CaptureSnapshot().GetHashCode()));
            Assert.Throws<NotSupportedException>(() => ((IList<GridPosition>)snapshot.BoxPositions)[0] = default);
            second.Send(MoveCommand.Right);
            second.Tick(0.25f);
            Assert.That(snapshot, Is.Not.EqualTo(second.CaptureSnapshot()));
            Assert.That(snapshot.PlayerPosition, Is.EqualTo(new GridPosition(0, 1)));
            Assert.That(snapshot.RemainingSeconds, Is.EqualTo(60f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReplayFailure_DisposesItsWorldAndUsingCleanupIsSafe(bool duringTick)
        {
            using var source = Create();
            source.Send(MoveCommand.Right);
            source.Tick(0.25f);
            GameplaySnapshot before = source.CaptureSnapshot();
            using var replay = new SimulationReplay(source.ExportRecording());
            World world = replay.Simulation.World;
            int disposals = 0;
            using var disposal = world.SubscribeWorldDisposed(_ => disposals++);
            if (duringTick) Assert.That(replay.Step(), Is.True);
            using var fault = duringTick
                ? world.SubscribeComponentChanged<LevelTimer>((in Entity entity, in LevelTimer previous, in LevelTimer current) => throw new InvalidOperationException("planned replay fault"))
                : world.SubscribeComponentAdded<MoveRequest>((in Entity entity, in MoveRequest request) => throw new InvalidOperationException("planned replay fault"));
            Assert.Throws<InvalidOperationException>(() => replay.Step());
            Assert.That(disposals, Is.EqualTo(1));
            Assert.Throws<ObjectDisposedException>(() => { _ = replay.Simulation; });
            Assert.DoesNotThrow(() => replay.Dispose());
            Assert.That(source.CaptureSnapshot(), Is.EqualTo(before));
        }

        private static Simulation Create(float limit = 60f) =>
            new(LevelParser.Parse("########\n#@-$--.#\n########"), new LevelPopulator(), limit);

        private static int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);

        private static void Tick(Simulation source, ICollection<GameplaySnapshot> snapshots, float delta)
        {
            source.Tick(delta);
            snapshots.Add(source.CaptureSnapshot());
        }

        private static void AssertReplay(Simulation source, IReadOnlyList<GameplaySnapshot> expectedTicks)
        {
            SimulationRecording recording = source.ExportRecording();
            GameplaySnapshot before = source.CaptureSnapshot();
            using var replay = new SimulationReplay(recording);
            int tick = 0;
            foreach (SimulationInput input in recording.Inputs)
            {
                Assert.That(replay.Step(), Is.True);
                if (input.Kind == SimulationInputKind.Tick)
                    Assert.That(replay.Simulation.CaptureSnapshot(), Is.EqualTo(expectedTicks[tick++]), $"Replayed tick {tick}");
            }
            Assert.That(tick, Is.EqualTo(expectedTicks.Count));
            Assert.That(replay.IsFinished, Is.True);
            Assert.That(replay.Step(), Is.False);
            Assert.DoesNotThrow(() => replay.Run());
            Assert.That(replay.Simulation.CaptureSnapshot(), Is.EqualTo(before));
            Assert.That(source.CaptureSnapshot(), Is.EqualTo(before));
        }
    }
}
