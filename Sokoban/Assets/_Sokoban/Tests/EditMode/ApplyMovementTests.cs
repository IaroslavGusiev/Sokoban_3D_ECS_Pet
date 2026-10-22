using System;
using System.Collections.Generic;
using System.Linq;
using DefaultEcs;
using DefaultEcs.System;
using NUnit.Framework;
using Sokoban.Core.Components;
using Sokoban.Core.Systems;

namespace Sokoban.Tests.EditMode
{
    public sealed class ApplyMovementTests
    {
        private World _world;
        private ApplyMovementSystem _apply;
        private SequentialSystem<float> _pipeline;

        [SetUp]
        public void SetUp()
        {
            _world = new World();
            _apply = new ApplyMovementSystem(_world);
            _pipeline = new SequentialSystem<float>(_apply);
        }

        [TearDown]
        public void TearDown()
        {
            _pipeline?.Dispose();
            _world.Dispose();
        }

        [TestCase(TargetKind.Untagged)]
        [TestCase(TargetKind.Player)]
        [TestCase(TargetKind.Box)]
        [TestCase(TargetKind.Goal)]
        public void Application_IsIndependentOfEntityTagsAndOwnerPosition(TargetKind kind)
        {
            Entity target = CreateTarget(kind);
            Entity owner = _world.CreateEntity();
            owner.Set(new MovementPlan(new MovementStep(target, new GridPosition(2, 3), new GridPosition(9, 7))));
            int notifications = 0;
            using var subscription = _world.SubscribeComponentChanged<GridPosition>(
                (in Entity entity, in GridPosition oldValue, in GridPosition newValue) => notifications++);

            _apply.Update(0f);
            _apply.Update(0f);

            Assert.That(target.Get<GridPosition>(), Is.EqualTo(new GridPosition(9, 7)));
            Assert.That(owner.Has<MovementPlan>(), Is.False);
            Assert.That(notifications, Is.EqualTo(1));
        }

        [Test]
        public void ConsumingSeveralPlans_DoesNotSkipQueryMembers()
        {
            var targets = new List<Entity>();
            for (int i = 0; i < 7; i++)
            {
                Entity target = Positioned(i, 0);
                target.Set(new MovementPlan(new MovementStep(target, new GridPosition(i, 0), new GridPosition(i, 1))));
                targets.Add(target);
            }

            _apply.Update(0f);

            for (int i = 0; i < targets.Count; i++)
            {
                Assert.That(targets[i].Get<GridPosition>(), Is.EqualTo(new GridPosition(i, 1)));
                Assert.That(targets[i].Has<MovementPlan>(), Is.False);
            }
        }

        [TestCase(TargetInvalidation.Destroyed)]
        [TestCase(TargetInvalidation.PositionRemoved)]
        [TestCase(TargetInvalidation.SourceChanged)]
        [TestCase(TargetInvalidation.EntityDisabled)]
        [TestCase(TargetInvalidation.PositionDisabled)]
        [TestCase(TargetInvalidation.ForeignWorld)]
        [TestCase(TargetInvalidation.EntityIdReused)]
        [TestCase(TargetInvalidation.WorldDisposed)]
        public void InvalidSecondParticipant_RejectsTheEntirePlanBeforeAnyWrites(TargetInvalidation invalidation)
        {
            Entity first = Positioned(1, 0);
            using var otherWorld = new World();
            World participantWorld = invalidation is TargetInvalidation.ForeignWorld or TargetInvalidation.WorldDisposed
                ? otherWorld
                : _world;
            Entity second = participantWorld.CreateEntity();
            second.Set(new GridPosition(0, 0));

            Entity owner = _world.CreateEntity();
            owner.Set(new MovementPlan(
                new MovementStep(first, new GridPosition(1, 0), new GridPosition(2, 0)),
                new MovementStep(second, new GridPosition(0, 0), new GridPosition(1, 0))));

            InvalidateTarget(second, invalidation, otherWorld);

            var before = Snapshot();
            int notifications = 0;
            using var subscription = _world.SubscribeComponentChanged<GridPosition>(
                (in Entity entity, in GridPosition oldValue, in GridPosition newValue) => notifications++);

            _apply.Update(0f);

            Assert.That(Snapshot(), Is.EquivalentTo(before));
            Assert.That(notifications, Is.Zero);
            Assert.That(owner.Has<MovementPlan>(), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DestroyingPlanOwnerOrCancellingPlan_LeavesBothTargetsUnchanged(bool destroyOwner)
        {
            Entity first = Positioned(1, 0);
            Entity second = Positioned(0, 0);
            Entity owner = _world.CreateEntity();
            owner.Set(new MovementPlan(
                new MovementStep(first, new GridPosition(1, 0), new GridPosition(2, 0)),
                new MovementStep(second, new GridPosition(0, 0), new GridPosition(1, 0))));
            if (destroyOwner)
            {
                owner.Dispose();
            }
            else
            {
                owner.Remove<MovementPlan>();
            }

            _apply.Update(0f);

            Assert.That(first.Get<GridPosition>(), Is.EqualTo(new GridPosition(1, 0)));
            Assert.That(second.Get<GridPosition>(), Is.EqualTo(new GridPosition(0, 0)));
        }

        [Test]
        public void DefaultPlanAndDuplicateTargets_AreConsumedWithoutPositionWrites()
        {
            Entity target = Positioned(0, 0);
            target.Set(default(MovementPlan));
            _apply.Update(0f);
            Assert.That(target.Has<MovementPlan>(), Is.False);
            target.Set(new MovementPlan(
                new MovementStep(target, new GridPosition(0, 0), new GridPosition(1, 0)),
                new MovementStep(target, new GridPosition(0, 0), new GridPosition(2, 0))));
            int notifications = 0;
            using var subscription = _world.SubscribeComponentChanged<GridPosition>(
                (in Entity entity, in GridPosition oldValue, in GridPosition newValue) => notifications++);
            _apply.Update(0f);
            Assert.That(target.Has<MovementPlan>(), Is.False);
            Assert.That(target.Get<GridPosition>(), Is.EqualTo(new GridPosition(0, 0)));
            Assert.That(notifications, Is.Zero);
        }

        [Test]
        public void DisabledPipeline_DoesNotApplyUntilEnabled()
        {
            Entity target = Positioned(0, 0);
            target.Set(new MovementPlan(new MovementStep(target, new GridPosition(0, 0), new GridPosition(1, 0))));
            _pipeline.IsEnabled = false;
            _pipeline.Update(0f);
            Assert.That(target.Has<MovementPlan>(), Is.True);
            Assert.That(target.Get<GridPosition>(), Is.EqualTo(new GridPosition(0, 0)));
            _pipeline.IsEnabled = true;
            _pipeline.Update(0f);
            Assert.That(target.Has<MovementPlan>(), Is.False);
            Assert.That(target.Get<GridPosition>(), Is.EqualTo(new GridPosition(1, 0)));
        }

        [Test]
        public void DisposingApplicationPipeline_LeavesWorldUsable()
        {
            _pipeline.Dispose();
            _pipeline = null;
            Assert.That(_world.CreateEntity().IsAlive, Is.True);
        }

        public enum TargetKind
        {
            Untagged,
            Player,
            Box,
            Goal
        }

        public enum TargetInvalidation
        {
            Destroyed,
            PositionRemoved,
            SourceChanged,
            EntityDisabled,
            PositionDisabled,
            ForeignWorld,
            EntityIdReused,
            WorldDisposed
        }

        private Entity CreateTarget(TargetKind kind)
        {
            Entity target = Positioned(2, 3);
            switch (kind)
            {
                case TargetKind.Player:
                    target.Set<PlayerTag>();
                    break;
                case TargetKind.Box:
                    target.Set<BoxTag>();
                    break;
                case TargetKind.Goal:
                    target.Set<GoalTag>();
                    break;
            }

            return target;
        }

        private void InvalidateTarget(Entity target, TargetInvalidation invalidation, World otherWorld)
        {
            switch (invalidation)
            {
                case TargetInvalidation.Destroyed:
                    target.Dispose();
                    break;
                case TargetInvalidation.PositionRemoved:
                    target.Remove<GridPosition>();
                    break;
                case TargetInvalidation.SourceChanged:
                    target.Set(new GridPosition(8, 8));
                    break;
                case TargetInvalidation.EntityDisabled:
                    target.Disable();
                    break;
                case TargetInvalidation.PositionDisabled:
                    target.Disable<GridPosition>();
                    break;
                case TargetInvalidation.ForeignWorld:
                    break;
                case TargetInvalidation.EntityIdReused:
                    target.Dispose();
                    Entity replacement = Positioned(0, 0);
                    Assert.That(replacement, Is.Not.EqualTo(target));
                    Assert.That(target.IsAlive, Is.False);
                    break;
                case TargetInvalidation.WorldDisposed:
                    otherWorld.Dispose();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(invalidation));
            }
        }

        private Entity Positioned(int x, int y)
        {
            Entity entity = _world.CreateEntity();
            entity.Set(new GridPosition(x, y));
            return entity;
        }

        private Dictionary<Entity, GridPosition> Snapshot() => _world
            .Where(entity => entity.Has<GridPosition>())
            .ToDictionary(entity => entity, entity => entity.Get<GridPosition>());
    }
}
