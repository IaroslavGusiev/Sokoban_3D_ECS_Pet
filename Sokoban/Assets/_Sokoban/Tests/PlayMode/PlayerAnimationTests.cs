using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using DefaultEcs;
using NUnit.Framework;
using Sokoban.Core;
using Sokoban.Core.Commands;
using Sokoban.Core.Components;
using Sokoban.Presentation;
using Sokoban.Presentation.Ecs.Components;
using Sokoban.Presentation.Ecs.Model;
using UnityEngine;
using UnityEngine.TestTools;

namespace Sokoban.Tests.PlayMode
{
    public sealed class PlayerAnimationTests
    {
        private readonly List<UnityEngine.Object> _objects = new();
        private static LevelDefinition Level => LevelParser.Parse("#######\n#@-$-.#\n#######");

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (UnityEngine.Object target in _objects)
            {
                if (target != null)
                {
                    UnityEngine.Object.Destroy(target);
                }
            }
            _objects.Clear();
            yield return null;
        }

        [Test]
        public void Push_UsesTheSameVisualProgressForPlayerAndBox()
        {
            using var simulation = new Simulation(Level, new LevelPopulator(), 60f, new SimulationPipelineFactory());
            using var views = CreateViews(simulation);
            views.Tick(0f);
            Entity player = simulation.World.Single(e => e.Has<PlayerTag>());
            Entity box = simulation.World.Single(e => e.Has<BoxTag>());
            Assert.That(player.Has<ViewMove>(), Is.False);
            simulation.Send(MoveCommand.Right);
            simulation.Tick(0f);
            views.Tick(0.16f);
            simulation.Send(MoveCommand.Right);
            simulation.Tick(0f);
            views.Tick(0.08f);
            Assert.That(player.Get<GridPosition>(), Is.EqualTo(new GridPosition(3, 1)));
            Assert.That(box.Get<GridPosition>(), Is.EqualTo(new GridPosition(4, 1)));
            AssertPosition(player, new Vector3(2.5f, 0, 1));
            AssertPosition(box, new Vector3(3.5f, 0, 1));
            Assert.That(player.Get<ViewMove>().IsPushing, Is.True);
            Assert.That(box.Get<ViewMove>().Elapsed, Is.EqualTo(player.Get<ViewMove>().Elapsed));
            Vector3 facing = player.Get<TransformComponent>().Transform.forward;
            Assert.That(Vector3.Distance(facing, Vector3.right), Is.LessThan(0.001f));
            views.Tick(0.08f);
            Assert.That(views.IsMoving, Is.False);
            AssertPosition(player, new Vector3(3, 0, 1));
            AssertPosition(box, new Vector3(4, 0, 1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BlockedMove_DoesNotStartMotionOrChangeFacing(bool push)
        {
            var level = push ? LevelParser.Parse("########\n#@$$-..#\n########") : Level;
            using var simulation = new Simulation(level, new LevelPopulator(), 60f, new SimulationPipelineFactory());
            using var views = CreateViews(simulation);
            views.Tick(0f);
            Entity player = simulation.World.Single(e => e.Has<PlayerTag>());
            GridPosition position = player.Get<GridPosition>();
            simulation.Send(push ? MoveCommand.Right : MoveCommand.Left);
            simulation.Tick(0.01f);
            views.Tick(0.08f);
            Assert.That(player.Get<GridPosition>(), Is.EqualTo(position));
            Assert.That(views.IsMoving, Is.False);
            Assert.That(player.Get<TransformComponent>().Transform.rotation, Is.EqualTo(Quaternion.identity));
        }

        [Test]
        public void Run_BuffersOnlyTheFirstMoveAndKeepsTheCountdownRunning()
        {
            GameplaySettings settings = TestGameplaySettings.Create();
            _objects.Add(settings);
            using var run = new LevelRun(new LevelRoot(CreateObject("Root").transform), Level,
                new Provider(CreateObject("Prefab")), new LevelPopulator(), new SimulationPipelineFactory(), new ViewPipelineFactory(),
                new RunOptions(SkinId.Default, settings.LevelTimeLimitSeconds, settings.MoveDurationSeconds));
            var simulation = (Simulation)typeof(LevelRun).GetField("_simulation", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(run);
            Entity player = simulation.World.Single(e => e.Has<PlayerTag>());
            run.Send(MoveCommand.Right);
            run.Tick(0.04f);
            run.Send(MoveCommand.Right);
            run.Send(MoveCommand.Left);
            run.Tick(0.12f);
            Assert.That(player.Get<GridPosition>(), Is.EqualTo(new GridPosition(2, 1)));
            Assert.That(run.RemainingSeconds, Is.EqualTo(59.84f).Within(0.001f));
            run.Tick(0f);
            Assert.That(player.Get<GridPosition>(), Is.EqualTo(new GridPosition(3, 1)));
            Assert.That(player.Get<ViewMove>().IsPushing, Is.True);
            run.Tick(0.16f);
            run.Tick(0f);
            Assert.That(player.Get<GridPosition>(), Is.EqualTo(new GridPosition(3, 1)));
        }

        [UnityTest]
        public IEnumerator SkinSwap_DuringPushRetainsVisualProgressAndDoesNotRestartMovement()
        {
            using var simulation = new Simulation(LevelParser.Parse("#####\n#@$.#\n#####"), new LevelPopulator(), 60f, new SimulationPipelineFactory());
            using var views = CreateViews(simulation);
            views.Tick(0f);
            Entity player = simulation.World.Single(e => e.Has<PlayerTag>());
            simulation.Send(MoveCommand.Right);
            simulation.Tick(0f);
            views.Tick(0.08f);
            Transform oldView = player.Get<TransformComponent>().Transform;
            views.SetPlayerSkin(SkinId.Punk);
            views.Tick(0f);
            Assert.That(player.Get<TransformComponent>().Transform, Is.Not.SameAs(oldView));
            AssertPosition(player, new Vector3(1.5f, 0, 1));
            Assert.That(player.Get<ViewMove>().Elapsed, Is.EqualTo(0.08f));
            views.Tick(0.08f);
            AssertPosition(player, new Vector3(2, 0, 1));
            Assert.That(views.IsMoving, Is.False);
            yield return null;
            Assert.That(oldView == null, Is.True);
        }

#if UNITY_EDITOR
        [TestCase("Doctor")]
        [TestCase("Farmer")]
        [TestCase("Punk")]
        public void ZombiePrefab_InstallsAnimatorAndDrivesWalkAndPush(string skin)
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Addressables/Player/Player_Zombie" + skin + ".prefab");
            GameObject actor = UnityEngine.Object.Instantiate(prefab);
            _objects.Add(actor);
            using var world = new World();
            Entity player = world.CreateEntity();
            player.Set<PlayerTag>();
            actor.GetComponent<EntityView>().Install(player);
            Assert.That(player.Get<PlayerAnimatorComponent>().Animator, Is.SameAs(actor.GetComponent<PlayerAnimator>()));
            var animator = actor.GetComponentInChildren<Animator>();
            Assert.That(animator.avatar.isValid && animator.avatar.isHuman, Is.True);
            Assert.That(animator.applyRootMotion, Is.False);
            animator.Rebind();
            animator.Update(0f);
            using var system = new Sokoban.Presentation.Ecs.Systems.PlayerAnimationSystem(world);
            player.Set(new ViewMove(default, default, 0.16f, isPushing: true));
            system.Update(0.08f);
            animator.Update(0.08f);
            Assert.That(animator.GetBool("Moving"), Is.True);
            Assert.That(animator.GetLayerWeight(1), Is.EqualTo(1f));
            Vector3 left = actor.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.LeftHand).position);
            Vector3 right = actor.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.RightHand).position);
            Assert.That(left.z, Is.GreaterThan(0.2f));
            Assert.That(right.z, Is.GreaterThan(0.2f));
            player.Remove<ViewMove>();
            system.Update(0.08f);
            Assert.That(animator.GetBool("Moving"), Is.False);
            Assert.That(animator.GetLayerWeight(1), Is.Zero);
            actor.GetComponent<EntityView>().Uninstall(player);
            Assert.That(player.Has<PlayerAnimatorComponent>(), Is.False);
        }
#endif

        private ViewLayer CreateViews(Simulation simulation) => new(new LevelRoot(CreateObject("Root").transform),
            simulation.World, new Provider(CreateObject("Prefab")), new ViewPipelineFactory(), new RunOptions(SkinId.Default, 60f, 0.16f));

        private GameObject CreateObject(string name)
        {
            var target = new GameObject(name);
            _objects.Add(target);
            return target;
        }

        private static void AssertPosition(Entity entity, Vector3 expected) =>
            Assert.That(Vector3.Distance(entity.Get<TransformComponent>().Transform.position, expected), Is.LessThan(0.001f));

        private sealed class Provider : IViewProvider
        {
            private readonly GameObject _prefab;
            public Provider(GameObject prefab) => _prefab = prefab;
            public UniTask<GameObject> LoadAsync(ViewKey key, CancellationToken ct) => UniTask.FromResult(_prefab);
        }
    }
}
