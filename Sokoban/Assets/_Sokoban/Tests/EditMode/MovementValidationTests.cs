using System;
using System.Collections.Generic;
using System.Linq;
using DefaultEcs;
using DefaultEcs.System;
using NUnit.Framework;
using Sokoban.Core.Commands;
using Sokoban.Core.Components;
using Sokoban.Core.Systems;

namespace Sokoban.Tests.EditMode
{
    public sealed class MovementValidationTests
    {
        private World _world;
        private MovementValidationSystem _validation;
        private Entity _player;

        [SetUp]
        public void SetUp()
        {
            _world = new World();
            _validation = new MovementValidationSystem(_world);
            _player = Create<PlayerTag>(0, 0);
        }

        [TearDown]
        public void TearDown()
        {
            _validation?.Dispose();
            _world.Dispose();
        }

        [TestCase(MoveCommand.Up, 0, 1)]
        [TestCase(MoveCommand.Down, 0, -1)]
        [TestCase(MoveCommand.Left, -1, 0)]
        [TestCase(MoveCommand.Right, 1, 0)]
        public void EmptyCell_ProducesOneStepPlanWithoutChangingPositions(MoveCommand command, int x, int y)
        {
            _player.Set(new MoveRequest(command));
            int notifications = 0;
            using var subscription = _world.SubscribeComponentChanged<GridPosition>(
                (in Entity entity, in GridPosition oldValue, in GridPosition newValue) => notifications++);

            _validation.Update(100f);

            MovementPlan movement = _player.Get<MovementPlan>();
            Assert.That(movement.StepCount, Is.EqualTo(1));
            Assert.That(movement.First.Target, Is.EqualTo(_player));
            Assert.That(movement.First.ExpectedSource, Is.EqualTo(new GridPosition(0, 0)));
            Assert.That(movement.First.Destination, Is.EqualTo(new GridPosition(x, y)));
            Assert.That(_player.Get<GridPosition>(), Is.EqualTo(new GridPosition(0, 0)));
            Assert.That(_player.Has<MoveRequest>(), Is.False);
            Assert.That(notifications, Is.Zero);
        }

        [Test]
        public void Push_ProducesOnePlanWithTwoOrderedStepsBeforeAnyPositionWrites()
        {
            Entity box = Create<BoxTag>(1, 0);
            _player.Set(new MoveRequest(MoveCommand.Right));

            _validation.Update(0f);

            MovementPlan movement = _player.Get<MovementPlan>();
            Assert.That(movement.StepCount, Is.EqualTo(2));
            Assert.That(movement.First.Target, Is.EqualTo(box));
            Assert.That(movement.First.ExpectedSource, Is.EqualTo(new GridPosition(1, 0)));
            Assert.That(movement.First.Destination, Is.EqualTo(new GridPosition(2, 0)));
            Assert.That(movement.Second.Target, Is.EqualTo(_player));
            Assert.That(movement.Second.Destination, Is.EqualTo(new GridPosition(1, 0)));
            Assert.That(_player.Get<GridPosition>(), Is.EqualTo(new GridPosition(0, 0)));
            Assert.That(box.Get<GridPosition>(), Is.EqualTo(new GridPosition(1, 0)));
            Assert.That(box.Has<MovementPlan>(), Is.False, "There must be no orphan half-plan on the box.");
        }

        [TestCase(BlockingArrangement.WallAtDestination)]
        [TestCase(BlockingArrangement.WallBehindBox)]
        [TestCase(BlockingArrangement.BoxBehindBox)]
        public void BlockedMoveOrPush_ConsumesRequestWithoutAnyPlanOrPositionWrite(BlockingArrangement arrangement)
        {
            ArrangeBlockedPath(arrangement);
            var before = _world.ToDictionary(entity => entity, entity => entity.Get<GridPosition>());
            _player.Set(new MoveRequest(MoveCommand.Right));
            int notifications = 0;
            using var subscription = _world.SubscribeComponentChanged<GridPosition>(
                (in Entity entity, in GridPosition oldValue, in GridPosition newValue) => notifications++);

            _validation.Update(0f);

            Assert.That(_player.Has<MoveRequest>(), Is.False);
            Assert.That(_world.Any(entity => entity.Has<MovementPlan>()), Is.False);
            Assert.That(_world.ToDictionary(entity => entity, entity => entity.Get<GridPosition>()), Is.EquivalentTo(before));
            Assert.That(notifications, Is.Zero);
        }

        [Test]
        public void CancellationBeforeValidationOrAfterValidation_LeavesBothParticipantsUnchanged()
        {
            Entity box = Create<BoxTag>(1, 0);
            using var apply = new ApplyMovementSystem(_world);
            _player.Set(new MoveRequest(MoveCommand.Right));
            _player.Remove<MoveRequest>();
            _validation.Update(0f);
            apply.Update(0f);
            Assert.That(_player.Has<MovementPlan>(), Is.False);

            _player.Set(new MoveRequest(MoveCommand.Right));
            _validation.Update(0f);
            Assert.That(_player.Get<MovementPlan>().StepCount, Is.EqualTo(2));
            _player.Remove<MovementPlan>();
            apply.Update(0f);

            Assert.That(_player.Get<GridPosition>(), Is.EqualTo(new GridPosition(0, 0)));
            Assert.That(box.Get<GridPosition>(), Is.EqualTo(new GridPosition(1, 0)));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DestroyingAValidatedPushParticipant_CancelsTheWholePush(bool destroyPlayer)
        {
            Entity box = Create<BoxTag>(1, 0);
            using var apply = new ApplyMovementSystem(_world);
            _player.Set(new MoveRequest(MoveCommand.Right));
            _validation.Update(0f);
            Assert.That(_player.Get<MovementPlan>().StepCount, Is.EqualTo(2));
            if (destroyPlayer) _player.Dispose();
            else box.Dispose();
            int notifications = 0;
            using var subscription = _world.SubscribeComponentChanged<GridPosition>(
                (in Entity entity, in GridPosition oldValue, in GridPosition newValue) => notifications++);

            apply.Update(0f);

            if (destroyPlayer) Assert.That(box.Get<GridPosition>(), Is.EqualTo(new GridPosition(1, 0)));
            else Assert.That(_player.Get<GridPosition>(), Is.EqualTo(new GridPosition(0, 0)));
            Assert.That(_world.Any(entity => entity.Has<MovementPlan>()), Is.False);
            Assert.That(notifications, Is.Zero);
        }

        [Test]
        public void OrderedStages_ValidateWithoutMovingThenApplyBeforeOutput()
        {
            Entity box = Create<BoxTag>(1, 0);
            using var apply = new ApplyMovementSystem(_world);
            _player.Set(new MoveRequest(MoveCommand.Right));
            apply.Update(0f);
            Assert.That(_player.Get<GridPosition>(), Is.EqualTo(new GridPosition(0, 0)));

            _validation.Update(0f);
            Assert.That(_player.Has<MovementPlan>(), Is.True);
            var writes = new List<Entity>();
            using var subscription = _world.SubscribeComponentChanged<GridPosition>(
                (in Entity entity, in GridPosition oldValue, in GridPosition newValue) => writes.Add(entity));
            apply.Update(0f);
            Assert.That(writes, Is.EqualTo(new[] { box, _player }));
            Assert.That(_player.Get<GridPosition>(), Is.EqualTo(new GridPosition(1, 0)));
            Assert.That(box.Get<GridPosition>(), Is.EqualTo(new GridPosition(2, 0)));
            Assert.That(_world.Any(entity => entity.Has<MoveRequest>() || entity.Has<MovementPlan>()), Is.False);
        }

        [Test]
        public void RequestSubmittedAfterValidation_RemainsForTheNextTick()
        {
            using var apply = new ApplyMovementSystem(_world);
            _player.Set(new MoveRequest(MoveCommand.Right));
            _validation.Update(0f);
            _player.Set(new MoveRequest(MoveCommand.Up));
            apply.Update(0f);
            Assert.That(_player.Get<GridPosition>(), Is.EqualTo(new GridPosition(1, 0)));
            Assert.That(_player.Get<MoveRequest>().Command, Is.EqualTo(MoveCommand.Up));
            _validation.Update(0f);
            apply.Update(0f);
            Assert.That(_player.Get<GridPosition>(), Is.EqualTo(new GridPosition(1, 1)));
            Assert.That(_player.Has<MoveRequest>(), Is.False);
        }

        [Test]
        public void DisposingValidation_LeavesWorldUsable()
        {
            _validation.Dispose();
            _validation = null;
            Assert.That(_world.CreateEntity().IsAlive, Is.True);
        }

        public enum BlockingArrangement
        {
            WallAtDestination,
            WallBehindBox,
            BoxBehindBox
        }

        private void ArrangeBlockedPath(BlockingArrangement arrangement)
        {
            switch (arrangement)
            {
                case BlockingArrangement.WallAtDestination:
                    Create<WallTag>(1, 0);
                    break;
                case BlockingArrangement.WallBehindBox:
                    Create<BoxTag>(1, 0);
                    Create<WallTag>(2, 0);
                    break;
                case BlockingArrangement.BoxBehindBox:
                    Create<BoxTag>(1, 0);
                    Create<BoxTag>(2, 0);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(arrangement));
            }
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
