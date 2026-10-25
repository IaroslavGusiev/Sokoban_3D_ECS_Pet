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
                    player.Set(new ViewMove { From = GridCoordinates.ToWorld(new GridPosition(1, 3)), To = expected, Duration = 0.4f, Elapsed = 0.2f });
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

        private static Entity Player(World world, GridPosition position)
        {
            Entity player = world.CreateEntity();
            player.Set<PlayerTag>();
            player.Set(position);
            return player;
        }
    }
}
