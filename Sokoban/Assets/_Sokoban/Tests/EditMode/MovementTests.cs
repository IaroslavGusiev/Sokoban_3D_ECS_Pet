using System;
using DefaultEcs;
using DefaultEcs.System;
using NUnit.Framework;
using Sokoban.Core.Systems;
using Sokoban.Core.Commands;
using Sokoban.Core.Components;
using System.Collections.Generic;

namespace Sokoban.Tests.EditMode
{
    public sealed class MovementTests
    {
        private World _world;
        private ISystem<float> _movement;
        private Entity _unrelatedBox;

        [SetUp]
        public void SetUp()
        {
            _world = new World();
            _movement = new SequentialSystem<float>(
                new MovementValidationSystem(_world),
                new ApplyMovementSystem(_world));
            CreateEntity<GoalTag>(10, 10);
            CreateEntity<WallTag>(20, 20);
            _unrelatedBox = CreateEntity<BoxTag>(30, 30);
        }

        [TearDown]
        public void TearDown()
        {
            _movement?.Dispose();
            _world?.Dispose();
        }

        [TestCase(MoveCommand.Up, 0, 1)]
        [TestCase(MoveCommand.Down, 0, -1)]
        [TestCase(MoveCommand.Left, -1, 0)]
        [TestCase(MoveCommand.Right, 1, 0)]
        public void EmptyCell_PlayerMovesOneCell(MoveCommand command, int deltaX, int deltaY)
        {
            Entity player = CreateEntity<PlayerTag>(0, 0);

            Assert.That(ExecuteMove(_world, _movement, command), Is.True);

            Assert.That(player.Get<GridPosition>(), Is.EqualTo(new GridPosition(deltaX, deltaY)));
            Assert.That(_unrelatedBox.Get<GridPosition>(), Is.EqualTo(new GridPosition(30, 30)));
        }

        [TestCase(MoveCommand.Up, 0, 1)]
        [TestCase(MoveCommand.Down, 0, -1)]
        [TestCase(MoveCommand.Left, -1, 0)]
        [TestCase(MoveCommand.Right, 1, 0)]
        public void WallAtDestination_MoveIsRejectedWithoutPositionChanges(
            MoveCommand command, int deltaX, int deltaY)
        {
            CreateEntity<PlayerTag>(0, 0);
            CreateEntity<WallTag>(deltaX, deltaY);

            AssertRejectedWithoutPositionChanges(command);
        }

        [TestCase(MoveCommand.Up, 0, 1)]
        [TestCase(MoveCommand.Down, 0, -1)]
        [TestCase(MoveCommand.Left, -1, 0)]
        [TestCase(MoveCommand.Right, 1, 0)]
        public void BoxWithEmptyCellBehind_PlayerPushesExactlyOneBox(
            MoveCommand command, int deltaX, int deltaY)
        {
            Entity player = CreateEntity<PlayerTag>(0, 0);
            Entity box = CreateEntity<BoxTag>(deltaX, deltaY);

            Assert.That(ExecuteMove(_world, _movement, command), Is.True);

            Assert.That(player.Get<GridPosition>(), Is.EqualTo(new GridPosition(deltaX, deltaY)));
            Assert.That(box.Get<GridPosition>(), Is.EqualTo(new GridPosition(2 * deltaX, 2 * deltaY)));
            Assert.That(_unrelatedBox.Get<GridPosition>(), Is.EqualTo(new GridPosition(30, 30)));
            Assert.That(player.Has<MoveRequest>(), Is.False);
            Assert.That(player.Has<MovementPlan>(), Is.False);
        }

        [TestCase(MoveCommand.Up, 0, 1)]
        [TestCase(MoveCommand.Down, 0, -1)]
        [TestCase(MoveCommand.Left, -1, 0)]
        [TestCase(MoveCommand.Right, 1, 0)]
        public void WallBehindBox_PushIsRejectedWithoutPositionChanges(
            MoveCommand command, int deltaX, int deltaY)
        {
            CreateEntity<PlayerTag>(0, 0);
            CreateEntity<BoxTag>(deltaX, deltaY);
            CreateEntity<WallTag>(2 * deltaX, 2 * deltaY);

            AssertRejectedWithoutPositionChanges(command);
        }

        [TestCase(MoveCommand.Up, 0, 1)]
        [TestCase(MoveCommand.Down, 0, -1)]
        [TestCase(MoveCommand.Left, -1, 0)]
        [TestCase(MoveCommand.Right, 1, 0)]
        public void SecondBoxBehindBox_PushIsRejectedWithoutPositionChanges(
            MoveCommand command, int deltaX, int deltaY)
        {
            CreateEntity<PlayerTag>(0, 0);
            CreateEntity<BoxTag>(deltaX, deltaY);
            CreateEntity<BoxTag>(2 * deltaX, 2 * deltaY);

            AssertRejectedWithoutPositionChanges(command);
        }

        [TestCase(MoveCommand.Up, 0, 1)]
        [TestCase(MoveCommand.Down, 0, -1)]
        [TestCase(MoveCommand.Left, -1, 0)]
        [TestCase(MoveCommand.Right, 1, 0)]
        public void GoalAtDestination_DoesNotBlockPlayer(MoveCommand command, int deltaX, int deltaY)
        {
            Entity player = CreateEntity<PlayerTag>(0, 0);
            Entity goal = CreateEntity<GoalTag>(deltaX, deltaY);

            Assert.That(ExecuteMove(_world, _movement, command), Is.True);

            Assert.That(player.Get<GridPosition>(), Is.EqualTo(new GridPosition(deltaX, deltaY)));
            Assert.That(goal.Get<GridPosition>(), Is.EqualTo(new GridPosition(deltaX, deltaY)));
        }

        [TestCase(MoveCommand.Up, 0, 1)]
        [TestCase(MoveCommand.Down, 0, -1)]
        [TestCase(MoveCommand.Left, -1, 0)]
        [TestCase(MoveCommand.Right, 1, 0)]
        public void GoalBehindBox_DoesNotBlockPush(MoveCommand command, int deltaX, int deltaY)
        {
            Entity player = CreateEntity<PlayerTag>(0, 0);
            Entity box = CreateEntity<BoxTag>(deltaX, deltaY);
            Entity goal = CreateEntity<GoalTag>(2 * deltaX, 2 * deltaY);

            Assert.That(ExecuteMove(_world, _movement, command), Is.True);

            Assert.That(player.Get<GridPosition>(), Is.EqualTo(new GridPosition(deltaX, deltaY)));
            Assert.That(box.Get<GridPosition>(), Is.EqualTo(new GridPosition(2 * deltaX, 2 * deltaY)));
            Assert.That(goal.Get<GridPosition>(), Is.EqualTo(new GridPosition(2 * deltaX, 2 * deltaY)));
        }

        [TestCase(MoveCommand.Up, 0, 1)]
        [TestCase(MoveCommand.Down, 0, -1)]
        [TestCase(MoveCommand.Left, -1, 0)]
        [TestCase(MoveCommand.Right, 1, 0)]
        public void RepeatedCommands_UseCurrentPositionsAndObserveNewWall(
            MoveCommand command, int deltaX, int deltaY)
        {
            Entity player = CreateEntity<PlayerTag>(0, 0);
            Entity box = CreateEntity<BoxTag>(deltaX, deltaY);

            Assert.That(ExecuteMove(_world, _movement, command), Is.True);
            Assert.That(ExecuteMove(_world, _movement, command), Is.True);

            Assert.That(player.Get<GridPosition>(), Is.EqualTo(new GridPosition(2 * deltaX, 2 * deltaY)));
            Assert.That(box.Get<GridPosition>(), Is.EqualTo(new GridPosition(3 * deltaX, 3 * deltaY)));

            CreateEntity<WallTag>(4 * deltaX, 4 * deltaY);
            AssertRejectedWithoutPositionChanges(command);
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(99)]
        public void InvalidCommand_IsRejectedWithoutPositionChanges(int value)
        {
            CreateEntity<PlayerTag>(0, 0);
            CreateEntity<BoxTag>(1, 0);

            AssertRejectedWithoutPositionChanges((MoveCommand)value);
        }

        [Test]
        public void NoPlayer_MoveIsRejectedWithoutPositionChanges()
        {
            AssertRejectedWithoutPositionChanges(MoveCommand.Right);
        }

        [Test]
        public void MultiplePlayers_MoveIsRejectedWithoutPositionChanges()
        {
            CreateEntity<PlayerTag>(0, 0);
            CreateEntity<PlayerTag>(1, 0);

            AssertRejectedWithoutPositionChanges(MoveCommand.Right);
        }

        [TestCase(MoveCommand.Up, 0, int.MaxValue)]
        [TestCase(MoveCommand.Down, 0, int.MinValue)]
        [TestCase(MoveCommand.Left, int.MinValue, 0)]
        [TestCase(MoveCommand.Right, int.MaxValue, 0)]
        public void PlayerDestinationOutsideIntegerRange_MoveIsRejectedWithoutPositionChanges(
            MoveCommand command, int playerX, int playerY)
        {
            CreateEntity<PlayerTag>(playerX, playerY);

            AssertRejectedWithoutPositionChanges(command);
        }

        [TestCase(MoveCommand.Up, 0, int.MaxValue - 1, 0, int.MaxValue)]
        [TestCase(MoveCommand.Down, 0, int.MinValue + 1, 0, int.MinValue)]
        [TestCase(MoveCommand.Left, int.MinValue + 1, 0, int.MinValue, 0)]
        [TestCase(MoveCommand.Right, int.MaxValue - 1, 0, int.MaxValue, 0)]
        public void BoxDestinationOutsideIntegerRange_PushIsRejectedWithoutPositionChanges(
            MoveCommand command, int playerX, int playerY, int boxX, int boxY)
        {
            CreateEntity<PlayerTag>(playerX, playerY);
            CreateEntity<BoxTag>(boxX, boxY);

            AssertRejectedWithoutPositionChanges(command);
        }

        [Test]
        public void SameCommandSequence_WithDifferentCreationOrder_ProducesSameResults()
        {
            Entity player = CreateEntity<PlayerTag>(0, 0);
            Entity box = CreateEntity<BoxTag>(1, 0);
            CreateEntity<WallTag>(3, 0);

            using var replayWorld = new World();
            Entity replayWall = replayWorld.CreateEntity();
            replayWall.Set<WallTag>();
            replayWall.Set(new GridPosition(3, 0));
            Entity replayBox = replayWorld.CreateEntity();
            replayBox.Set<BoxTag>();
            replayBox.Set(new GridPosition(1, 0));
            Entity replayPlayer = replayWorld.CreateEntity();
            replayPlayer.Set<PlayerTag>();
            replayPlayer.Set(new GridPosition(0, 0));
            using var replayMovement = new SequentialSystem<float>(
                new MovementValidationSystem(replayWorld),
                new ApplyMovementSystem(replayWorld));
            MoveCommand[] commands =
            {
                MoveCommand.Right, MoveCommand.Right, MoveCommand.Up,
                MoveCommand.Right, MoveCommand.Down, MoveCommand.Left
            };
            bool[] expectedResults = { true, false, true, true, true, true };

            for (int i = 0; i < commands.Length; i++)
            {
                Assert.That(ExecuteMove(_world, _movement, commands[i]), Is.EqualTo(expectedResults[i]));
                Assert.That(ExecuteMove(replayWorld, replayMovement, commands[i]), Is.EqualTo(expectedResults[i]));
                Assert.That(replayPlayer.Get<GridPosition>(), Is.EqualTo(player.Get<GridPosition>()));
                Assert.That(replayBox.Get<GridPosition>(), Is.EqualTo(box.Get<GridPosition>()));
            }

            Assert.That(player.Get<GridPosition>(), Is.EqualTo(new GridPosition(1, 0)));
            Assert.That(box.Get<GridPosition>(), Is.EqualTo(new GridPosition(2, -1)));
        }

        [Test]
        public void DisposingMovementSystem_LeavesWorldAndPositionsUnchanged()
        {
            CreateEntity<PlayerTag>(0, 0);
            Dictionary<Entity, GridPosition> before = SnapshotPositions();

            _movement.Dispose();

            _movement = null;
            Assert.That(SnapshotPositions(), Is.EquivalentTo(before));
            Assert.That(_world.CreateEntity().IsAlive, Is.True);
        }

        [Test]
        public void SuccessfulPlayerMove_EmitsOnePositionChange()
        {
            Entity player = CreateEntity<PlayerTag>(0, 0);
            var changes = new List<(Entity Entity, GridPosition OldValue, GridPosition NewValue)>();
            using var subscription = _world.SubscribeComponentChanged<GridPosition>(
                (in Entity entity, in GridPosition oldValue, in GridPosition newValue) =>
                    changes.Add((entity, oldValue, newValue)));

            Assert.That(ExecuteMove(_world, _movement, MoveCommand.Right), Is.True);

            Assert.That(changes, Is.EqualTo(new[]
            {
                (player, new GridPosition(0, 0), new GridPosition(1, 0))
            }));
        }

        [Test]
        public void SuccessfulPush_EmitsOnePositionChangeForPlayerAndBox()
        {
            Entity player = CreateEntity<PlayerTag>(0, 0);
            Entity box = CreateEntity<BoxTag>(1, 0);
            var changes = new List<(Entity Entity, GridPosition OldValue, GridPosition NewValue)>();
            using var subscription = _world.SubscribeComponentChanged<GridPosition>(
                (in Entity entity, in GridPosition oldValue, in GridPosition newValue) =>
                    changes.Add((entity, oldValue, newValue)));

            Assert.That(ExecuteMove(_world, _movement, MoveCommand.Right), Is.True);

            Assert.That(changes, Is.EquivalentTo(new[]
            {
                (player, new GridPosition(0, 0), new GridPosition(1, 0)),
                (box, new GridPosition(1, 0), new GridPosition(2, 0))
            }));
        }

        [Test]
        public void BoxPushedOntoAndOffGoal_LeavesGoalComponentsUnchanged()
        {
            Entity player = CreateEntity<PlayerTag>(0, 0);
            Entity box = CreateEntity<BoxTag>(1, 0);
            Entity goal = CreateEntity<GoalTag>(2, 0);
            GridPosition originalPosition = goal.Get<GridPosition>();
            GoalTag originalTag = goal.Get<GoalTag>();
            int goalComponentChanges = 0;
            using var positionChanges = _world.SubscribeComponentChanged<GridPosition>(
                (in Entity entity, in GridPosition oldValue, in GridPosition newValue) =>
                {
                    if (entity == goal) goalComponentChanges++;
                });
            using var positionRemovals = _world.SubscribeComponentRemoved<GridPosition>(
                (in Entity entity, in GridPosition value) =>
                {
                    if (entity == goal) goalComponentChanges++;
                });
            using var tagChanges = _world.SubscribeComponentChanged<GoalTag>(
                (in Entity entity, in GoalTag oldValue, in GoalTag newValue) =>
                {
                    if (entity == goal) goalComponentChanges++;
                });
            using var tagRemovals = _world.SubscribeComponentRemoved<GoalTag>(
                (in Entity entity, in GoalTag value) =>
                {
                    if (entity == goal) goalComponentChanges++;
                });

            Assert.That(ExecuteMove(_world, _movement, MoveCommand.Right), Is.True);
            Assert.That(player.Get<GridPosition>(), Is.EqualTo(new GridPosition(1, 0)));
            Assert.That(box.Get<GridPosition>(), Is.EqualTo(originalPosition));
            AssertGoalUnchanged();

            Assert.That(ExecuteMove(_world, _movement, MoveCommand.Right), Is.True);
            Assert.That(player.Get<GridPosition>(), Is.EqualTo(originalPosition));
            Assert.That(box.Get<GridPosition>(), Is.EqualTo(new GridPosition(3, 0)));
            AssertGoalUnchanged();

            void AssertGoalUnchanged()
            {
                Assert.That(goal.IsAlive, Is.True);
                Assert.That(goal.Has<GridPosition>(), Is.True);
                Assert.That(goal.Get<GridPosition>(), Is.EqualTo(originalPosition));
                Assert.That(goal.Has<GoalTag>(), Is.True);
                Assert.That(goal.Get<GoalTag>(), Is.EqualTo(originalTag));
                Assert.That(goalComponentChanges, Is.Zero);
            }
        }

        // Test-only convenience: submit input, run the real pipeline, and observe its result.
        private static bool ExecuteMove(World world, ISystem<float> movement, MoveCommand command)
        {
            using var players = world.GetEntities().With<PlayerTag>().With<GridPosition>().AsSet();
            if (players.Count > 0)
            {
                players.GetEntities()[0].Set(new MoveRequest(command));
            }

            bool moved = false;
            using var subscription = world.SubscribeComponentChanged<GridPosition>(
                (in Entity entity, in GridPosition oldValue, in GridPosition newValue) => moved = true);
            movement.Update(0f);
            return moved;
        }

        private Entity CreateEntity<TTag>(int x, int y) where TTag : struct
        {
            Entity entity = _world.CreateEntity();
            entity.Set(new GridPosition(x, y));
            entity.Set<TTag>();
            return entity;
        }

        private Dictionary<Entity, GridPosition> SnapshotPositions()
        {
            var snapshot = new Dictionary<Entity, GridPosition>();
            using var positions = _world.GetEntities().With<GridPosition>().AsSet();
            foreach (Entity entity in positions.GetEntities())
            {
                snapshot.Add(entity, entity.Get<GridPosition>());
            }

            return snapshot;
        }

        private void AssertRejectedWithoutPositionChanges(MoveCommand command)
        {
            Dictionary<Entity, GridPosition> before = SnapshotPositions();
            var changes = new List<Entity>();
            using var subscription = _world.SubscribeComponentChanged<GridPosition>(
                (in Entity entity, in GridPosition oldValue, in GridPosition newValue) => changes.Add(entity));

            Assert.That(ExecuteMove(_world, _movement, command), Is.False);

            Assert.That(SnapshotPositions(), Is.EquivalentTo(before));
            Assert.That(changes, Is.Empty);
        }
    }
}
