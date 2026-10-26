using DefaultEcs;
using NUnit.Framework;
using Sokoban.Core.Components;
using Sokoban.Presentation.Ecs;
using Sokoban.Presentation.Ecs.Components;
using Sokoban.Presentation.Ecs.Systems;
using UnityEngine;

namespace Sokoban.Tests.Editor
{
    public sealed class ActorMotionTests
    {
        [Test]
        public void Move_CreatesVisualStepAndFacing()
        {
            using var world = new World();
            Entity player = Player(world, new GridPosition(2, 3));
            using var system = new ActorMotionSystem(world, 0.4f);
            Assert.That(player.Has<ActorViewState>(), Is.False);
            system.Update(0f);
            Assert.That(player.Get<ActorViewState>().HasFacing, Is.False);
            player.Set(new GridPosition(3, 3));
            system.Update(0f);
            ViewMove move = player.Get<ViewMove>();
            Assert.That(move.From, Is.EqualTo(GridCoordinates.ToWorld(new GridPosition(2, 3))));
            Assert.That(move.To, Is.EqualTo(GridCoordinates.ToWorld(new GridPosition(3, 3))));
            Assert.That(move.Duration, Is.EqualTo(0.4f));
            Assert.That(move.IsPushing, Is.False);
            ActorViewState state = player.Get<ActorViewState>();
            Assert.That(state.HasFacing, Is.True);
            Assert.That(Quaternion.Angle(state.Facing, Quaternion.LookRotation(Vector3.right)), Is.LessThan(0.001f));
        }

        [Test]
        public void Elapsed_AdvancesAndRemovesTheFinishedStep()
        {
            using var world = new World();
            Entity player = Player(world, new GridPosition(0, 0));
            using var system = new ActorMotionSystem(world, 0.4f);
            system.Update(0f);
            player.Set(new GridPosition(0, 1));
            system.Update(0.1f);
            Assert.That(player.Get<ViewMove>().Elapsed, Is.EqualTo(0.1f));
            system.Update(0.1f);
            Assert.That(player.Get<ViewMove>().Elapsed, Is.EqualTo(0.2f));
            system.Update(0.2f);
            Assert.That(player.Has<ViewMove>(), Is.False);
        }

        [Test]
        public void Retarget_StartsFromTheCurrentInterpolatedPosition()
        {
            using var world = new World();
            Entity player = Player(world, new GridPosition(0, 0));
            using var system = new ActorMotionSystem(world, 0.4f);
            system.Update(0f);
            player.Set(new GridPosition(1, 0));
            system.Update(0.1f);
            Vector3 previous = player.Get<ViewMove>().Position;
            player.Set(new GridPosition(1, 1));
            system.Update(0f);
            Assert.That(player.Get<ViewMove>().From, Is.EqualTo(previous));
            Assert.That(player.Get<ViewMove>().To, Is.EqualTo(GridCoordinates.ToWorld(new GridPosition(1, 1))));
        }

        [Test]
        public void Push_MarksOnlyThePlayer()
        {
            using var world = new World();
            Entity player = Player(world, new GridPosition(0, 0));
            Entity box = world.CreateEntity();
            box.Set<BoxTag>();
            box.Set(new GridPosition(1, 0));
            using var system = new ActorMotionSystem(world, 0.4f);
            system.Update(0f);
            box.Set(new GridPosition(2, 0));
            player.Set(new GridPosition(1, 0));
            system.Update(0f);
            Assert.That(player.Get<ViewMove>().IsPushing, Is.True);
            Assert.That(box.Get<ViewMove>().IsPushing, Is.False);
        }

        [Test]
        public void LateActors_AreInitializedBeforeMovementDetection()
        {
            using var world = new World();
            using var system = new ActorMotionSystem(world, 0.4f);
            system.Update(0f);
            for (int index = 0; index < 4; index++) Player(world, new GridPosition(index, 2));
            Assert.DoesNotThrow(() => system.Update(0f));
            foreach (Entity actor in world)
            {
                Assert.That(actor.Get<ActorViewState>().Position, Is.EqualTo(actor.Get<GridPosition>()));
                Assert.That(actor.Get<ActorViewState>().HasFacing, Is.False);
                Assert.That(actor.Has<ViewMove>(), Is.False);
            }
        }

        [Test]
        public void Floor_IsNotAnAnimatedActor()
        {
            using var world = new World();
            Entity floor = world.CreateEntity();
            floor.Set<FloorTag>();
            floor.Set(new GridPosition(0, 0));
            using var system = new ActorMotionSystem(world, 0.4f);
            system.Update(0f);
            floor.Set(new GridPosition(1, 0));
            system.Update(0.1f);
            Assert.That(floor.Has<ActorViewState>(), Is.False);
            Assert.That(floor.Has<ViewMove>(), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Sync_PreservesPrefabRotationUntilFacingExistsAndAvoidsRepeatedWrites(bool interpolating)
        {
            var target = new GameObject("Projection test");
            try
            {
                using var world = new World();
                Entity player = Player(world, new GridPosition(2, 3));
                player.Set(new TransformComponent(target.transform));
                target.transform.rotation = Quaternion.Euler(0, 37, 0);
                Quaternion original = target.transform.rotation;
                Quaternion facing = Quaternion.LookRotation(Vector3.left);
                player.Set(new ActorViewState { Position = player.Get<GridPosition>(), Facing = facing, HasFacing = false });
                Vector3 expected = GridCoordinates.ToWorld(player.Get<GridPosition>());
                if (interpolating)
                {
                    player.Set(new ViewMove(GridCoordinates.ToWorld(new GridPosition(1, 3)), expected, 0.4f, elapsed: 0.2f));
                    expected = player.Get<ViewMove>().Position;
                }
                using var system = new GridTransformSyncSystem(world);
                system.Update(0f);
                Assert.That(target.transform.position, Is.EqualTo(expected));
                Assert.That(target.transform.rotation, Is.EqualTo(original));
                ActorViewState state = player.Get<ActorViewState>();
                state.HasFacing = true;
                player.Set(state);
                system.Update(0f);
                Assert.That(Quaternion.Angle(target.transform.rotation, facing), Is.LessThan(0.001f));
                target.transform.hasChanged = false;
                system.Update(0f);
                Assert.That(target.transform.hasChanged, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Push_IsDetectedBeforeStateWritesForEitherActorOrder(bool boxFirst)
        {
            using var world = new World();
            Entity first = world.CreateEntity();
            Entity second = world.CreateEntity();
            Entity player = boxFirst ? second : first;
            Entity box = boxFirst ? first : second;
            player.Set<PlayerTag>();
            player.Set(new GridPosition(0, 0));
            box.Set<BoxTag>();
            box.Set(new GridPosition(1, 0));
            using var system = new ActorMotionSystem(world, 0.4f);
            system.Update(0f);
            box.Set(new GridPosition(2, 0));
            player.Set(new GridPosition(1, 0));
            system.Update(0.1f);
            ViewMove playerMove = player.Get<ViewMove>();
            ViewMove boxMove = box.Get<ViewMove>();
            Assert.That(playerMove.IsPushing, Is.True);
            Assert.That(boxMove.IsPushing, Is.False);
            Assert.That(playerMove.Elapsed, Is.EqualTo(0.1f));
            Assert.That(boxMove.Elapsed, Is.EqualTo(playerMove.Elapsed));
            Assert.That(playerMove.Position, Is.EqualTo(new Vector3(0.25f, 0, 0)));
            Assert.That(boxMove.Position, Is.EqualTo(new Vector3(1.25f, 0, 0)));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void Push_DoesNotConfuseWrappedDeltasWithSuccessfulPushing(bool reverse, bool vertical)
        {
            using var world = new World();
            int from = reverse ? int.MaxValue - 1023 : int.MinValue + 1023;
            int to = reverse ? int.MinValue : int.MaxValue;
            int boxTo = reverse ? to + 1024 : to - 1024;
            GridPosition Position(int coordinate) => vertical ? new GridPosition(0, coordinate) : new GridPosition(coordinate, 0);
            Entity player = Player(world, Position(from));
            Entity box = world.CreateEntity();
            box.Set<BoxTag>();
            box.Set(Position(to));
            using var system = new ActorMotionSystem(world, 0.4f);
            system.Update(0f);
            player.Set(Position(to));
            box.Set(Position(boxTo));
            system.Update(0f);
            Assert.That(player.Get<ViewMove>().IsPushing, Is.False);
            Assert.That(box.Get<ViewMove>().IsPushing, Is.False);
        }

        [TestCase(0f, 0f, 2f, false)]
        [TestCase(0.1f, 0.1f, 2.5f, false)]
        [TestCase(0.4f, 0.4f, 4f, true)]
        [TestCase(0.8f, 0.4f, 4f, true)]
        [TestCase(-0.1f, -0.1f, 2f, false)]
        public void ViewMove_AdvanceReturnsANewValueAndPreservesTimingBehavior(float delta, float elapsed, float x, bool done)
        {
            var original = new ViewMove(new Vector3(2, 0, 3), new Vector3(4, 0, 3), 0.4f, isPushing: true);
            ViewMove advanced = original.Advance(delta);
            Assert.That(original.Elapsed, Is.Zero);
            Assert.That(original.Position, Is.EqualTo(new Vector3(2, 0, 3)));
            Assert.That(advanced.Elapsed, Is.EqualTo(elapsed));
            Assert.That(advanced.Position, Is.EqualTo(new Vector3(x, 0, 3)));
            Assert.That(advanced.IsDone, Is.EqualTo(done));
            Assert.That(advanced.From, Is.EqualTo(original.From));
            Assert.That(advanced.To, Is.EqualTo(original.To));
            Assert.That(advanced.Duration, Is.EqualTo(original.Duration));
            Assert.That(advanced.IsPushing, Is.True);
        }

        [TestCase(0.4f)]
        [TestCase(0.75f)]
        public void AllFinishedMoves_AreRemovedInOneBufferedPass(float delta)
        {
            using var world = new World();
            for (var index = 0; index < 8; index++) Player(world, new GridPosition(index, 0));
            using var system = new ActorMotionSystem(world, 0.4f);
            system.Update(0f);
            foreach (Entity entity in world) entity.Set(new GridPosition(entity.Get<GridPosition>().X, 1));
            system.Update(delta);
            foreach (Entity entity in world)
            {
                Assert.That(entity.Has<ViewMove>(), Is.False);
                Assert.That(entity.Get<ActorViewState>().Position, Is.EqualTo(entity.Get<GridPosition>()));
            }
        }

        [TestCase(0f)]
        [TestCase(-0.1f)]
        public void NonpositiveDuration_KeepsTheExistingImmediateCompletionBehavior(float duration)
        {
            using var world = new World();
            Entity player = Player(world, new GridPosition(0, 0));
            using var system = new ActorMotionSystem(world, duration);
            system.Update(0f);
            player.Set(new GridPosition(1, 0));
            system.Update(0f);
            Assert.That(player.Has<ViewMove>(), Is.False);
            Assert.That(player.Get<ActorViewState>().Position, Is.EqualTo(new GridPosition(1, 0)));
        }

        [Test]
        public void Retarget_WithDeltaUsesThePreviousVisualPositionBeforeAdvancingTheNewMove()
        {
            using var world = new World();
            Entity player = Player(world, new GridPosition(0, 0));
            using var system = new ActorMotionSystem(world, 0.4f);
            system.Update(0f);
            player.Set(new GridPosition(1, 0));
            system.Update(0.1f);
            Vector3 previous = player.Get<ViewMove>().Position;
            player.Set(new GridPosition(1, 1));
            system.Update(0.1f);
            Assert.That(player.Get<ViewMove>().From, Is.EqualTo(previous));
            Assert.That(player.Get<ViewMove>().Elapsed, Is.EqualTo(0.1f));
            Assert.That(player.Get<ViewMove>().Position, Is.EqualTo(new Vector3(0.4375f, 0, 0.25f)));
        }

        [Test]
        public void Initialization_PreservesAuthoredRotationAndCreatesNoMovement()
        {
            var target = new GameObject("Initial actor pose");
            try
            {
                using var world = new World();
                Entity player = Player(world, new GridPosition(2, 3));
                target.transform.SetPositionAndRotation(new Vector3(2, 0, 3), Quaternion.Euler(0, 37, 0));
                Quaternion rotation = target.transform.rotation;
                player.Set(new TransformComponent(target.transform));
                using var motion = new ActorMotionSystem(world, 0.4f);
                using var projection = new GridTransformSyncSystem(world);
                motion.Update(0.1f);
                projection.Update(0.1f);
                Assert.That(player.Has<ViewMove>(), Is.False);
                Assert.That(player.Get<ActorViewState>().Position, Is.EqualTo(new GridPosition(2, 3)));
                Assert.That(player.Get<ActorViewState>().HasFacing, Is.False);
                Assert.That(target.transform.rotation, Is.EqualTo(rotation));
                Assert.That(target.transform.position, Is.EqualTo(new Vector3(2, 0, 3)));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void DisabledComposite_SkipsInitializationDetectionAndAdvancementUntilReenabled()
        {
            using var world = new World();
            Entity player = Player(world, new GridPosition(0, 0));
            using var system = new ActorMotionSystem(world, 0.4f);
            Assert.That(system.IsEnabled, Is.True);
            system.Update(0f);
            player.Set(new GridPosition(1, 0));
            system.Update(0.1f);
            Vector3 previous = player.Get<ViewMove>().Position;
            system.IsEnabled = false;
            player.Set(new GridPosition(1, 1));
            Entity late = Player(world, new GridPosition(3, 2));
            system.Update(0.2f);
            Assert.That(system.IsEnabled, Is.False);
            Assert.That(late.Has<ActorViewState>(), Is.False);
            Assert.That(player.Get<ActorViewState>().Position, Is.EqualTo(new GridPosition(1, 0)));
            Assert.That(player.Get<ViewMove>().Elapsed, Is.EqualTo(0.1f));
            system.IsEnabled = true;
            system.Update(0.1f);
            Assert.That(late.Get<ActorViewState>().Position, Is.EqualTo(late.Get<GridPosition>()));
            Assert.That(late.Has<ViewMove>(), Is.False);
            Assert.That(player.Get<ActorViewState>().Position, Is.EqualTo(new GridPosition(1, 1)));
            Assert.That(player.Get<ViewMove>().From, Is.EqualTo(previous));
            Assert.That(player.Get<ViewMove>().Elapsed, Is.EqualTo(0.1f));
        }

        [Test]
        public void Disposal_LeavesWorldAndMotionDataAvailableToItsOwner()
        {
            using var world = new World();
            Entity player = Player(world, new GridPosition(0, 0));
            var system = new ActorMotionSystem(world, 0.4f);
            system.Update(0f);
            player.Set(new GridPosition(1, 0));
            system.Update(0.1f);
            system.Dispose();
            Assert.That(player.IsAlive, Is.True);
            Assert.That(player.Get<ViewMove>().Elapsed, Is.EqualTo(0.1f));
            Entity late = Player(world, new GridPosition(3, 2));
            using var replacement = new ActorMotionSystem(world, 0.4f);
            replacement.Update(0.1f);
            Assert.That(late.Get<ActorViewState>().Position, Is.EqualTo(late.Get<GridPosition>()));
            Assert.That(late.Has<ViewMove>(), Is.False);
            Assert.That(player.Get<ViewMove>().Elapsed, Is.EqualTo(0.2f));
        }

        private static Entity Player(World world, GridPosition position)
        {
            Entity player = world.CreateEntity();
            player.Set<PlayerTag>();
            player.Set(position);
            return player;
        }
    }
}
