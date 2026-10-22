using System;
using DefaultEcs;
using DefaultEcs.System;
using NUnit.Framework;
using Sokoban.Core.Commands;
using Sokoban.Core.Components;
using Sokoban.Core.Systems;

namespace Sokoban.Tests.EditMode
{
    public sealed class MovementTickTests
    {
        private World _world;
        private ISystem<float> _movement;
        private Entity _player;

        [SetUp]
        public void SetUp()
        {
            _world = new World();
            _player = Create<PlayerTag>(0, 0);
            _movement = new SequentialSystem<float>(
                new MovementValidationSystem(_world),
                new ApplyMovementSystem(_world));
        }

        [TearDown]
        public void TearDown()
        {
            _movement?.Dispose();
            _world.Dispose();
        }

        [TestCase(MoveCommand.Up, 0, 1, 0f)]
        [TestCase(MoveCommand.Down, 0, -1, 0.001f)]
        [TestCase(MoveCommand.Left, -1, 0, 1f)]
        [TestCase(MoveCommand.Right, 1, 0, 100f)]
        public void Tick_ConsumesOneRequestAndIdleTicksDoNotRepeatIt(MoveCommand command, int x, int y, float deltaTime)
        {
            _player.Set(new MoveRequest(command));
            Assert.That(_player.Get<GridPosition>(), Is.EqualTo(new GridPosition(0, 0)));

            _movement.Update(deltaTime);
            Assert.That(_player.Get<GridPosition>(), Is.EqualTo(new GridPosition(x, y)));
            Assert.That(_player.Has<MoveRequest>(), Is.False);

            _movement.Update(deltaTime);
            _movement.Update(deltaTime);
            Assert.That(_player.Get<GridPosition>(), Is.EqualTo(new GridPosition(x, y)));
        }

        [Test]
        public void BlockedRequest_IsConsumedWithoutPositionNotificationsOrLaterRetry()
        {
            Entity wall = Create<WallTag>(1, 0);
            int notifications = 0;
            using (_world.SubscribeComponentChanged<GridPosition>(OnPositionChanged))
            {
                _player.Set(new MoveRequest(MoveCommand.Right));
                _movement.Update(0f);
                Assert.That(_player.Has<MoveRequest>(), Is.False);
                Assert.That(_player.Get<GridPosition>(), Is.EqualTo(new GridPosition(0, 0)));
                Assert.That(notifications, Is.Zero);

                wall.Dispose();
                _movement.Update(1f);
                Assert.That(_player.Get<GridPosition>(), Is.EqualTo(new GridPosition(0, 0)));
                Assert.That(notifications, Is.Zero);
            }

            void OnPositionChanged(in Entity entity, in GridPosition oldValue, in GridPosition newValue) => notifications++;
        }

        [TestCase(0)]
        [TestCase(99)]
        public void InvalidRequest_IsConsumedWithoutMovement(int command)
        {
            _player.Set(new MoveRequest((MoveCommand)command));
            _movement.Update(0f);
            Assert.That(_player.Get<GridPosition>(), Is.EqualTo(new GridPosition(0, 0)));
            Assert.That(_player.Has<MoveRequest>(), Is.False);
        }

        [Test]
        public void DisabledPipeline_LeavesRequestPendingUntilEnabled()
        {
            _player.Set(new MoveRequest(MoveCommand.Up));
            _movement.IsEnabled = false;
            _movement.Update(0f);
            Assert.That(_player.Has<MoveRequest>(), Is.True);
            Assert.That(_player.Get<GridPosition>(), Is.EqualTo(new GridPosition(0, 0)));

            _movement.IsEnabled = true;
            _movement.Update(0f);
            Assert.That(_player.Has<MoveRequest>(), Is.False);
            Assert.That(_player.Get<GridPosition>(), Is.EqualTo(new GridPosition(0, 1)));
        }

        [Test]
        public void MultiplePlayers_ConsumeAllRequestsWithoutMoving()
        {
            Entity other = Create<PlayerTag>(3, 3);
            _player.Set(new MoveRequest(MoveCommand.Up));
            other.Set(new MoveRequest(MoveCommand.Down));
            _movement.Update(0f);

            Assert.That(_player.Has<MoveRequest>(), Is.False);
            Assert.That(other.Has<MoveRequest>(), Is.False);
            Assert.That(_player.Get<GridPosition>(), Is.EqualTo(new GridPosition(0, 0)));
            Assert.That(other.Get<GridPosition>(), Is.EqualTo(new GridPosition(3, 3)));
        }

        [Test]
        public void DisposingSystem_LeavesWorldUsable()
        {
            _movement.Dispose();
            _movement = null;
            Assert.That(_world.CreateEntity().IsAlive, Is.True);
        }

        private Entity Create<T>(int x, int y) where T : struct
        {
            Entity entity = _world.CreateEntity();
            entity.Set(new GridPosition(x, y));
            entity.Set<T>();
            return entity;
        }
    }
}
