using Sokoban.Core;
using System;
using System.Linq;
using DefaultEcs;
using DefaultEcs.System;
using NUnit.Framework;
using Sokoban.Core.Features;
using Sokoban.Core.Commands;
using Sokoban.Core.Components;

namespace Sokoban.Tests.App
{
    public sealed class SimulationTests
    {
        [Test]
        public void PushingOnlyBoxOntoGoal_CompletesOnceAfterMovement()
        {
            using var simulation = new Simulation(LevelParser.Parse("#####\n#@$.#\n#####"));
            Entity player = simulation.World.Single(entity => entity.Has<PlayerTag>());
            int completions = 0;
            using var subscription = simulation.World.SubscribeComponentAdded<LevelCompletedTag>((in Entity entity, in LevelCompletedTag _) =>
            {
                Assert.That(entity, Is.EqualTo(player));
                completions++;
            });
            Assert.That(simulation.IsCompleted, Is.False);
            simulation.Tick(0f);
            Assert.That(simulation.IsCompleted, Is.False);
            simulation.Send(MoveCommand.Right);
            Assert.That(simulation.IsCompleted, Is.False);
            simulation.Tick(0f);
            Assert.That(simulation.IsCompleted, Is.True);
            simulation.Tick(0f);
            simulation.Tick(1f);
            Assert.That(simulation.IsCompleted, Is.True);
            Assert.That(completions, Is.EqualTo(1));
        }

        [Test]
        public void TwoBoxes_RequireBothGoalsToBeCovered()
        {
            using var simulation = new Simulation(LevelParser.Parse("#######\n#@$.--#\n#--$.-#\n#######"));
            simulation.Send(MoveCommand.Right);
            simulation.Tick(0f);
            Assert.That(simulation.IsCompleted, Is.False);
            simulation.Send(MoveCommand.Down);
            simulation.Tick(0f);
            simulation.Send(MoveCommand.Right);
            simulation.Tick(0f);
            Assert.That(simulation.IsCompleted, Is.True);
        }

        [Test]
        public void Simulation_PopulatesEntitiesFromItsDefinition()
        {
            LevelDefinition level = CreateLevel();
            using (var simulation = CreateSimulation(level))
            {
                Entity[] entities = simulation.World.ToArray();
                Assert.That(entities.Length, Is.EqualTo(level.Width * level.Height + level.Walls.Count + level.Boxes.Count + 1));
                Assert.That(entities.All(entity => entity.Has<GridPosition>()), Is.True);
                Assert.That(entities.Count(entity => entity.Has<FloorTag>()), Is.EqualTo(level.Width * level.Height));
                Assert.That(entities.Where(entity => entity.Has<GoalTag>()).All(entity => entity.Has<FloorTag>()), Is.True);
                Assert.That(Positions<WallTag>(simulation), Is.EquivalentTo(level.Walls));
                Assert.That(Positions<BoxTag>(simulation), Is.EquivalentTo(level.Boxes));
                Assert.That(Positions<GoalTag>(simulation), Is.EquivalentTo(level.Goals));
                Assert.That(Positions<PlayerTag>(simulation), Is.EqualTo(new[] { level.PlayerStart }));
                Assert.That(entities.Select(entity => entity.World).Distinct().Count(), Is.EqualTo(1));
            }
        }

        [Test]
        public void Simulations_SharingDefinitionHaveIndependentWorldsAndPositions()
        {
            LevelDefinition level = CreateLevel();
            using (var first = CreateSimulation(level))
            using (var second = CreateSimulation(level))
            {
                Assert.That(first.World.First().World, Is.Not.SameAs(second.World.First().World));
                first.Send(MoveCommand.Left);
                first.Tick(0f);
                Assert.That(Positions<PlayerTag>(first), Is.EqualTo(new[] { new GridPosition(2, 2) }));
                Assert.That(Positions<PlayerTag>(second), Is.EqualTo(new[] { level.PlayerStart }));
                Assert.That(level.PlayerStart, Is.EqualTo(new GridPosition(3, 2)));
                Assert.That(level.Boxes, Is.EqualTo(new[] { new GridPosition(2, 3), new GridPosition(4, 3) }));
            }
        }

        [Test]
        public void RejectedCommand_LeavesSimulationPositionsUnchanged()
        {
            using (var simulation = CreateSimulation(CreateLevel()))
            {
                simulation.Send(MoveCommand.Left);
                simulation.Tick(0f);
                simulation.Send(MoveCommand.Left);
                simulation.Tick(0f);
                GridPosition[] before = simulation.World.Select(entity => entity.Get<GridPosition>()).ToArray();

                simulation.Send(MoveCommand.Left);
                simulation.Tick(0f);
                Assert.That(simulation.World.Select(entity => entity.Get<GridPosition>()), Is.EqualTo(before));
            }
        }

        [Test]
        public void Dispose_ReleasesOwnedWorldExactlyOnce()
        {
            var simulation = CreateSimulation(CreateLevel());
            int disposed = 0;
            using (simulation.World.SubscribeWorldDisposed(_ => disposed++))
            {
                simulation.Dispose();
                Assert.That(disposed, Is.EqualTo(1));
            }
        }

        [Test]
        public void DisposedSimulation_CannotExposeAReplacementWorld()
        {
            var previous = CreateSimulation(CreateLevel());
            World oldWorld = previous.World;
            previous.Dispose();
            using var replacement = CreateSimulation(CreateLevel());
            Assert.That(replacement.World, Is.Not.SameAs(oldWorld));
            replacement.Send(MoveCommand.Left);
            replacement.Tick(0f);
            Assert.That(Positions<PlayerTag>(replacement), Is.EqualTo(new[] { new GridPosition(2, 2) }));
        }

        [Test]
        public void PendingRequest_IsDeferredAndConsumedOnce()
        {
            using (var simulation = CreateSimulation(CreateLevel()))
            {
                simulation.Send(MoveCommand.Left);
                Assert.That(Positions<PlayerTag>(simulation), Is.EqualTo(new[] { new GridPosition(3, 2) }));
                simulation.Tick(0f);
                simulation.Tick(100f);
                Assert.That(Positions<PlayerTag>(simulation), Is.EqualTo(new[] { new GridPosition(2, 2) }));
            }
        }

        [Test]
        public void Send_TwoCommandsBeforeTick_FirstRequestWins()
        {
            using var simulation = CreateSimulation(CreateLevel());
            Entity player = simulation.World.Single(entity => entity.Has<PlayerTag>());

            simulation.Send(MoveCommand.Left);
            simulation.Send(MoveCommand.Up);

            Assert.That(player.Get<MoveRequest>().Command, Is.EqualTo(MoveCommand.Left));
            Assert.That(player.Get<GridPosition>(), Is.EqualTo(new GridPosition(3, 2)));
            Assert.That(player.Has<MovementPlan>(), Is.False);

            simulation.Tick(0f);
            Assert.That(player.Get<GridPosition>(), Is.EqualTo(new GridPosition(2, 2)));
            Assert.That(player.Has<MoveRequest>(), Is.False);
            simulation.Tick(0f);
            Assert.That(player.Get<GridPosition>(), Is.EqualTo(new GridPosition(2, 2)),
                "The second command must not be queued for a later tick.");
        }

        [TestCase(0)]
        [TestCase(99)]
        public void InvalidCommand_IsNotAccepted(int command)
        {
            using (var simulation = CreateSimulation(CreateLevel()))
            {
                simulation.Send((MoveCommand)command);
                simulation.Tick(0f);
                Assert.That(Positions<PlayerTag>(simulation), Is.EqualTo(new[] { CreateLevel().PlayerStart }));
                Assert.That(simulation.World.Any(entity => entity.Has<MoveRequest>()), Is.False);
            }
        }

        [Test]
        public void ReplacementSimulation_DoesNotInheritPendingRequestOrOwnership()
        {
            var previous = CreateSimulation(CreateLevel());
            previous.Send(MoveCommand.Left);
            previous.Dispose();
            using (var replacement = CreateSimulation(CreateLevel()))
            {
                replacement.Tick(0f);
                Assert.That(Positions<PlayerTag>(replacement), Is.EqualTo(new[] { new GridPosition(3, 2) }));
                Assert.That(replacement.World.Any(entity => entity.Has<MoveRequest>()), Is.False);
            }
        }

        [Test]
        public void ComposedFeatures_RunInListOrderBeforeAndAfterMovement()
        {
            int phase = 0;
            var input = new TestFeature(world => new TestSystem(deltaTime =>
            {
                Assert.That(deltaTime, Is.EqualTo(0.25f));
                Assert.That(phase++, Is.Zero);
                world.Single(entity => entity.Has<PlayerTag>()).Set(new MoveRequest(MoveCommand.Left));
            }));
            var output = new TestFeature(world => new TestSystem(_ =>
            {
                Assert.That(phase++, Is.EqualTo(1));
                Assert.That(world.Single(entity => entity.Has<PlayerTag>()).Get<GridPosition>(), Is.EqualTo(new GridPosition(2, 2)));
            }));
            using var simulation = new Simulation(CreateLevel(), new IFeature[] { input, new MovementFeature(), output });
            simulation.Tick(0.25f);
            Assert.That(phase, Is.EqualTo(2));
        }

        [Test]
        public void Simulation_DisposesFeaturesExactlyOnceBeforeItsWorld()
        {
            var first = new TestSystem(_ => { });
            var last = new TestSystem(_ => { });
            var simulation = new Simulation(CreateLevel(), new IFeature[]
            {
                new TestFeature(_ => first), new MovementFeature(), new TestFeature(_ => last)
            });
            using var subscription = simulation.World.SubscribeWorldDisposed(_ =>
            {
                Assert.That(first.DisposeCalls, Is.EqualTo(1));
                Assert.That(last.DisposeCalls, Is.EqualTo(1));
            });
            simulation.Dispose();
            Assert.That(first.DisposeCalls, Is.EqualTo(1));
            Assert.That(last.DisposeCalls, Is.EqualTo(1));
        }

        [Test]
        public void PipelineDisposalFailure_StillDisposesWorld()
        {
            var system = new TestSystem(_ => { }) { ThrowOnDispose = true };
            var simulation = new Simulation(CreateLevel(), new[] { new TestFeature(_ => system) });
            int worldDisposals = 0;
            using var subscription = simulation.World.SubscribeWorldDisposed(_ => worldDisposals++);
            Assert.Throws<InvalidOperationException>(() => simulation.Dispose());
            Assert.That(worldDisposals, Is.EqualTo(1));
        }

        private static Simulation CreateSimulation(LevelDefinition level) =>
            new Simulation(level);

        private sealed class TestFeature : IFeature
        {
            private readonly Func<World, ISystem<float>> _create;
            public TestFeature(Func<World, ISystem<float>> create)
            {
                _create = create;
            }
            public ISystem<float> Create(World world) => _create(world);
        }

        private static LevelDefinition CreateLevel()
        {
            var walls = from y in Enumerable.Range(0, 7)
                        from x in Enumerable.Range(0, 7)
                        where x == 0 || x == 6 || y == 0 || y == 6
                        select new GridPosition(x, y);
            return new LevelDefinition(7, 7, new GridPosition(3, 2), walls,
                new[] { new GridPosition(2, 3), new GridPosition(4, 3) },
                new[] { new GridPosition(2, 4), new GridPosition(4, 4) });
        }

        private sealed class TestSystem : ISystem<float>
        {
            private readonly Action<float> _update;
            public bool IsEnabled { get; set; } = true;
            public int DisposeCalls { get; private set; }
            public bool ThrowOnDispose { get; set; }
            public TestSystem(Action<float> update) => _update = update;
            public void Update(float state) => _update(state);
            public void Dispose()
            {
                DisposeCalls++;
                if (ThrowOnDispose) throw new InvalidOperationException("Test disposal failure");
            }
        }

        private static GridPosition[] Positions<T>(Simulation simulation) where T : struct => simulation.World
            .Where(entity => entity.Has<T>()).Select(entity => entity.Get<GridPosition>()).ToArray();
    }
}
