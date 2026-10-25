using System.Linq;
using DefaultEcs;
using NUnit.Framework;
using Sokoban.Core;
using Sokoban.Core.Commands;
using Sokoban.Core.Components;

namespace Sokoban.CoreTests
{
    public sealed class LevelStateTests
    {
        [Test]
        public void Simulation_CreatesOneNonSpatialLevelEntityAndKeepsMovementOnThePlayer()
        {
            using var simulation = Create();
            Entity level = simulation.World.Single(entity => entity.Has<LevelStateTag>());
            Entity player = simulation.World.Single(entity => entity.Has<PlayerTag>());
            Assert.That(level.World, Is.SameAs(player.World));
            Assert.That(level, Is.Not.EqualTo(player));
            Assert.That(level.Get<LevelTimer>().RemainingSeconds, Is.EqualTo(3f));
            Assert.That(level.Get<MoveCount>().Value, Is.Zero);
            Assert.That(level.Has<GridPosition>() || level.Has<PlayerTag>() || level.Has<BoxTag>()
                || level.Has<WallTag>() || level.Has<FloorTag>() || level.Has<GoalTag>(), Is.False);
            Assert.That(level.Has<LevelCompletedTag>() || level.Has<LevelTimedOutTag>(), Is.False);
            Assert.That(player.Has<LevelStateTag>() || player.Has<LevelTimer>() || player.Has<MoveCount>()
                || player.Has<LevelCompletedTag>() || player.Has<LevelTimedOutTag>(), Is.False);
            simulation.Send(MoveCommand.Right);
            Assert.That(player.Has<MoveRequest>(), Is.True);
            Assert.That(level.Has<MoveRequest>() || level.Has<MovementPlan>(), Is.False);
            simulation.Tick(0.25f);
            Assert.That(level.Get<MoveCount>().Value, Is.EqualTo(1));
            Assert.That(player.Has<MoveCount>(), Is.False);
            Assert.That(simulation.World.Single(entity => entity.Has<PlayerTag>()), Is.EqualTo(player));
            simulation.Tick(0f);
            Assert.That(simulation.World.Single(entity => entity.Has<PlayerTag>()), Is.EqualTo(player));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DisposalAndReplacement_ReleaseBothEntitiesAndResetAttemptState(bool complete)
        {
            var previous = Create();
            World oldWorld = previous.World;
            Entity oldLevel = previous.World.Single(entity => entity.Has<LevelStateTag>());
            Entity oldPlayer = previous.World.Single(entity => entity.Has<PlayerTag>());
            if (complete)
            {
                previous.Send(MoveCommand.Right);
                previous.Tick(0f);
                previous.Send(MoveCommand.Right);
                previous.Tick(0f);
                previous.Send(MoveCommand.Right);
                previous.Tick(0f);
                Assert.That(previous.IsCompleted, Is.True);
            }
            else
            {
                previous.Tick(3f);
                Assert.That(previous.IsTimedOut, Is.True);
            }
            LevelResult snapshot = previous.Result;
            previous.Dispose();
            Assert.That(oldLevel.World, Is.Null);
            Assert.That(oldPlayer.World, Is.Null);
            using var replacement = Create();
            Entity level = replacement.World.Single(entity => entity.Has<LevelStateTag>());
            Assert.That(level.World, Is.Not.SameAs(oldWorld));
            Assert.That(level.IsAlive, Is.True);
            Assert.That(replacement.RemainingSeconds, Is.EqualTo(3f));
            Assert.That(replacement.Result.Moves, Is.Zero);
            Assert.That(replacement.IsCompleted || replacement.IsTimedOut, Is.False);
            replacement.Tick(0.5f);
            Assert.That(replacement.Result.Moves, Is.Zero, "The position snapshot starts at the new player's spawn.");
            Assert.That(snapshot.Moves, Is.EqualTo(complete ? 3 : 0));
            Assert.That(snapshot.RemainingSeconds, Is.EqualTo(complete ? 3f : 0f));
        }

        private static Simulation Create() =>
            new(LevelParser.Parse("#######\n#@-$-.#\n#######"), new LevelPopulator(), 3f);
    }
}
