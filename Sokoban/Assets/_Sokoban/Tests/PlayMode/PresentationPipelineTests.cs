using Sokoban.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DefaultEcs;
using NUnit.Framework;
using Sokoban.Core.Commands;
using Sokoban.Core.Components;
using Sokoban.Unity;
using Sokoban.Unity.Ecs.Adapters;
using Sokoban.Unity.Ecs.Components;
using Sokoban.Unity.Ecs.Systems;
using Sokoban.Unity.Ecs.Model;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Sokoban.Tests.PlayMode
{
    public sealed class PresentationPipelineTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly List<Simulation> _simulations = new List<Simulation>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // Simulations own ECS cleanup; view destruction never accesses an entity handle.
            foreach (Simulation simulation in _simulations) simulation.Dispose();
            foreach (GameObject target in _objects)
            {
                if (target != null) UnityEngine.Object.Destroy(target);
            }
            yield return null;
            _objects.Clear();
            _simulations.Clear();
        }

        [UnityTest]
        public IEnumerator BindingAndSync_ProjectIntoWorldSpaceUnderTranslatedAndRotatedParent()
        {
            var simulation = Track(CreateSimulation(HardcodedLevel.Create()));
            World world = simulation.World;
            using var sync = new GridTransformSyncSystem(world);
            Entity player = Player(simulation);
            GameObject parent = CreateObject("Offset board");
            parent.transform.position = new Vector3(10, 3, -4);
            parent.transform.rotation = Quaternion.Euler(0, 37, 0);
            GameObject actor = CreateObject("Actor");
            actor.transform.SetParent(parent.transform, false);
            var adapter = CreateAdapter(actor);
            adapter.Install(player);
            Assert.That(actor.transform.position, Is.EqualTo(parent.transform.position), "Adapters install data; only synchronization projects positions.");
            sync.Update(0f);
            AssertPosition(actor.transform, new Vector3(3, 0, 2));

            player.Set(new GridPosition(4, 5));
            actor.transform.localPosition = new Vector3(99, 99, 99);
            sync.Update(0f);
            AssertPosition(actor.transform, new Vector3(4, 0, 5));
            Assert.That(player.Get<GridPosition>(), Is.EqualTo(new GridPosition(4, 5)));

            actor.SetActive(false);
            Assert.That(player.Has<TransformComponent>(), Is.True, "Disabling a view does not detach its component.");
            Assert.That(player.IsAlive, Is.True);
            player.Set(new GridPosition(5, 5));
            sync.Update(0f);
            AssertPosition(actor.transform, new Vector3(5, 0, 5));
            actor.SetActive(true);
            Assert.That(player.Get<TransformComponent>().Transform, Is.SameAs(actor.transform));
            Assert.That(simulation.World.Count(), Is.EqualTo(29));
            yield return null;
        }

        [UnityTest]
        public IEnumerator DuplicateBinding_IsRejectedWithoutReplacingTheOriginal()
        {
            var simulation = Track(CreateSimulation(HardcodedLevel.Create()));
            Entity player = Player(simulation);
            var first = CreateAdapter(CreateObject("First"));
            var second = CreateAdapter(CreateObject("Second"));
            first.Install(player);
            Assert.Throws<InvalidOperationException>(() => second.Install(player));
            Assert.That(player.Get<TransformComponent>().Transform, Is.SameAs(first.transform));
            Assert.That(player.IsAlive, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DestroyingOldViews_CannotAffectReplacementSimulationBinding()
        {
            var previous = Track(CreateSimulation(HardcodedLevel.Create()));
            var oldAdapter = CreateAdapter(CreateObject("Old view"));
            oldAdapter.Install(Player(previous));
            previous.Dispose();
            _simulations.Remove(previous);

            var replacement = Track(CreateSimulation(HardcodedLevel.Create()));
            Entity player = Player(replacement);
            var currentAdapter = CreateAdapter(CreateObject("Current view"));
            currentAdapter.Install(player);
            UnityEngine.Object.Destroy(oldAdapter.gameObject);
            yield return null;
            Assert.That(player.Get<TransformComponent>().Transform, Is.SameAs(currentAdapter.transform));
            Assert.That(player.IsAlive, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator OrderedPipeline_PresentsACompletePushInTheSameTick()
        {
            var level = new LevelDefinition(5, 3, new GridPosition(1, 1), Array.Empty<GridPosition>(),
                new[] { new GridPosition(2, 1) }, new[] { new GridPosition(3, 1) });
            var simulation = Track(CreateSimulation(level));
            using var sync = new GridTransformSyncSystem(simulation.World);
            Entity player = Player(simulation);
            Entity box = simulation.World.Single(entity => entity.Has<BoxTag>());
            var playerView = CreateAdapter(CreateObject("Player view"));
            var boxView = CreateAdapter(CreateObject("Box view"));
            playerView.Install(player);
            boxView.Install(box);
            simulation.Send(MoveCommand.Right);
            simulation.Tick(0f);
            sync.Update(0f);
            Assert.That(player.Get<GridPosition>(), Is.EqualTo(new GridPosition(2, 1)));
            Assert.That(box.Get<GridPosition>(), Is.EqualTo(new GridPosition(3, 1)));
            AssertPosition(playerView.transform, new Vector3(2, 0, 1));
            AssertPosition(boxView.transform, new Vector3(3, 0, 1));
            Assert.That(player.Has<MoveRequest>(), Is.False);
            yield return null;
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator Controller_AppliesRestartAfterMovementAndReleasesCurrentLevel()
        {
            var trace = new List<string>();
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<ViewCatalog>("Assets/_Sokoban/Scenes/LevelViewCatalog.asset");
            var input = new TestInput();
            var controller = new GameplayController(input, new HardcodedLevelProvider(),
                new ViewFeatures(), catalog, new LevelRoot(CreateObject("Level root").transform));
            try
            {
                controller.Initialize();
                Simulation simulation = controller.Current;
                World world = simulation.World;
                using var movement = world.SubscribeComponentChanged<GridPosition>(
                    (in Entity entity, in GridPosition oldValue, in GridPosition newValue) =>
                    {
                        trace.Add("movement");
                        input.RestartPressed = true;
                        Assert.That(controller.Current, Is.SameAs(simulation));
                    });
                using var disposed = world.SubscribeWorldDisposed(_ => trace.Add("disposed"));
                simulation.Send(MoveCommand.Left);
                controller.Tick();
                Assert.That(trace, Is.EqualTo(new[] { "movement", "disposed" }));
                Assert.That(controller.Current, Is.Not.SameAs(simulation));
                Assert.That(trace.Count, Is.EqualTo(2));
            }
            finally
            {
                controller.Dispose();
            }
            Assert.That(controller.Current, Is.Null);
            yield return null;
        }
#endif

        [UnityTest]
        public IEnumerator ViewInstallationFailure_LeavesCleanupToTheSimulation()
        {
            var simulation = Track(CreateSimulation(HardcodedLevel.Create()));
            Entity player = Player(simulation);
            GameObject host = CreateObject("Entity view");
            var view = host.AddComponent<EntityView>();
            var first = CreateAdapter(host);
            var duplicate = CreateAdapter(CreateObject("Duplicate"));
            SetField(view, "_adapters", new EntityAdapter[] { first, duplicate });

            Assert.Throws<InvalidOperationException>(() => view.Install(player));
            Assert.That(player.Get<TransformComponent>().Transform, Is.SameAs(host.transform));
            Assert.That(player.IsAlive, Is.True);

            simulation.Dispose();
            _simulations.Remove(simulation);
            var replacement = Track(CreateSimulation(HardcodedLevel.Create()));
            Assert.That(Player(replacement).Has<TransformComponent>(), Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MissingTransform_IsSkippedByProjection()
        {
            var simulation = Track(CreateSimulation(HardcodedLevel.Create()));
            World world = simulation.World;
            using var sync = new GridTransformSyncSystem(world);
            Entity player = Player(simulation);
            var adapter = CreateAdapter(CreateObject("Invalid preset"));
            SetField(adapter, "_value", default(TransformComponent));
            adapter.Install(player);
            Assert.DoesNotThrow(() => sync.Update(0f));
            Assert.That(player.Get<TransformComponent>().Transform, Is.Null);
            Assert.That(player.Get<GridPosition>(), Is.EqualTo(new GridPosition(3, 2)));
            yield return null;
        }

        [UnityTest]
        public IEnumerator EntityView_InstallsComponentsWithoutCreatingEntities()
        {
            var simulation = Track(CreateSimulation(HardcodedLevel.Create()));
            Entity player = Player(simulation);
            GameObject host = CreateObject("Actor");
            var adapter = CreateAdapter(host);
            var view = host.AddComponent<EntityView>();
            SetField(view, "_adapters", new EntityAdapter[] { adapter });

            view.Install(player);
            Assert.That(player.Get<TransformComponent>().Transform, Is.SameAs(adapter.transform));
            Assert.That(simulation.World.Count(), Is.EqualTo(29));
            Assert.Throws<InvalidOperationException>(() => view.Install(player));
            yield return null;
        }

        [UnityTest]
        public IEnumerator EntityDisposal_RemovesGameplayAndAdapterComponentsFromQueries()
        {
            var simulation = Track(CreateSimulation(HardcodedLevel.Create()));
            Entity player = Player(simulation);
            var adapter = CreateAdapter(CreateObject("Actor"));
            adapter.Install(player);
            using var transforms = player.World.GetEntities().With<TransformComponent>().AsSet();
            using var players = player.World.GetEntities().With<PlayerTag>().AsSet();
            using var positions = player.World.GetEntities().With<GridPosition>().AsSet();
            Assert.That(transforms.Count, Is.EqualTo(1));
            Assert.That(players.Count, Is.EqualTo(1));
            Assert.That(positions.Count, Is.EqualTo(29));

            player.Dispose();

            Assert.That(transforms.Count, Is.Zero);
            Assert.That(players.Count, Is.Zero);
            Assert.That(positions.Count, Is.EqualTo(28));
            yield return null;
        }

        [UnityTest]
        public IEnumerator DestroyingAView_DoesNotDisposeItsGameplayEntity()
        {
            var simulation = Track(CreateSimulation(HardcodedLevel.Create()));
            World world = simulation.World;
            using var sync = new GridTransformSyncSystem(world);
            Entity player = Player(simulation);
            var adapter = CreateAdapter(CreateObject("Actor"));
            adapter.Install(player);

            UnityEngine.Object.Destroy(adapter.gameObject);
            yield return null;

            Assert.That(player.IsAlive, Is.True);
            Assert.That(player.Has<TransformComponent>(), Is.True, "The simulation retains the component until ECS disposal.");
            Assert.That(player.Get<GridPosition>(), Is.EqualTo(new GridPosition(3, 2)));
            Assert.DoesNotThrow(() => sync.Update(0f));
        }

        [UnityTest]
        public IEnumerator TransformProjection_UpdatesWorldPositionWithoutWritingEcsComponents()
        {
            var simulation = Track(CreateSimulation(HardcodedLevel.Create()));
            World world = simulation.World;
            using var sync = new GridTransformSyncSystem(world);
            Entity player = Player(simulation);
            var adapter = CreateAdapter(CreateObject("Observed actor"));
            adapter.Install(player);
            int transformChanges = 0;
            int gridChanges = 0;
            using var transformSubscription = world.SubscribeComponentChanged<TransformComponent>(
                (in Entity entity, in TransformComponent oldValue, in TransformComponent newValue) => transformChanges++);
            using var gridSubscription = world.SubscribeComponentChanged<GridPosition>(
                (in Entity entity, in GridPosition oldValue, in GridPosition newValue) => gridChanges++);

            sync.Update(0f);
            AssertPosition(adapter.transform, new Vector3(3, 0, 2));
            sync.Update(0f);
            adapter.transform.position = new Vector3(99, 99, 99);
            sync.Update(0f);

            AssertPosition(adapter.transform, new Vector3(3, 0, 2));
            Assert.That(player.Get<TransformComponent>().Transform, Is.SameAs(adapter.transform));
            Assert.That(transformChanges, Is.Zero);
            Assert.That(gridChanges, Is.Zero, "Projection must not write GridPosition back.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Pushing_NotifiesGridPositionsAndProjectsWithoutWritingTransformComponent()
        {
            var level = new LevelDefinition(5, 3, new GridPosition(1, 1), new[] { new GridPosition(4, 1) },
                new[] { new GridPosition(2, 1) }, Array.Empty<GridPosition>());
            var simulation = Track(CreateSimulation(level));
            World world = simulation.World;
            using var sync = new GridTransformSyncSystem(world);
            Entity player = Player(simulation);
            Entity box = simulation.World.Single(entity => entity.Has<BoxTag>());
            var playerView = CreateAdapter(CreateObject("Observed player"));
            var boxView = CreateAdapter(CreateObject("Observed box"));
            playerView.Install(player);
            boxView.Install(box);
            simulation.Tick(0f);
            sync.Update(0f);
            var changes = new List<Entity>();
            using var subscription = world.SubscribeComponentChanged<GridPosition>(
                (in Entity entity, in GridPosition oldValue, in GridPosition newValue) => changes.Add(entity));

            int transformChanges = 0;
            using var transformSubscription = world.SubscribeComponentChanged<TransformComponent>(
                (in Entity entity, in TransformComponent oldValue, in TransformComponent newValue) => transformChanges++);

            simulation.Send(MoveCommand.Right);
            simulation.Tick(0f);
            sync.Update(0f);
            Assert.That(changes, Is.EquivalentTo(new[] { player, box }));
            AssertPosition(playerView.transform, new Vector3(2, 0, 1));
            AssertPosition(boxView.transform, new Vector3(3, 0, 1));

            changes.Clear();
            simulation.Send(MoveCommand.Right);
            simulation.Tick(0f);
            sync.Update(0f);
            Assert.That(changes, Is.Empty);
            Assert.That(transformChanges, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DirectionalInput_UsesPriorityAndQueuesMovementWithoutExecutingIt()
        {
            var simulation = Track(CreateSimulation(HardcodedLevel.Create()));
            Entity player = Player(simulation);
            World world = player.World;
            Entity wall = world.CreateEntity();
            wall.Set<WallTag>();
            wall.Set(new GridPosition(3, 3));
            var input = new KeyboardMoveInput();
            yield return WithKeyboard(keyboard =>
            {
                PressKeys(keyboard, Key.UpArrow, Key.LeftArrow);
                simulation.Send(input.ReadMove());
                Assert.That(player.Get<MoveRequest>().Command, Is.EqualTo(MoveCommand.Up));
                Assert.That(player.Get<GridPosition>(), Is.EqualTo(new GridPosition(3, 2)));
                Assert.That(player.Has<MovementPlan>(), Is.False);
            });
        }

        [UnityTest]
        public IEnumerator RestartInput_IsReadWithoutMutatingSimulationRequests()
        {
            var simulation = Track(CreateSimulation(HardcodedLevel.Create()));
            Entity player = Player(simulation);
            var input = new KeyboardMoveInput();
            simulation.Send(MoveCommand.Left);
            yield return WithKeyboard(keyboard =>
            {
                PressKeys(keyboard, Key.R, Key.RightArrow);
                Assert.That(input.RestartPressed, Is.True);
                Assert.That(input.ReadMove(), Is.EqualTo(MoveCommand.Right));
                Assert.That(player.Get<MoveRequest>().Command, Is.EqualTo(MoveCommand.Left));
                Assert.That(player.Get<GridPosition>(), Is.EqualTo(new GridPosition(3, 2)));
            });
        }

        private static void PressKeys(Keyboard keyboard, params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            InputSystem.Update();
            // Native events may select another device while Update processes its queue.
            keyboard.MakeCurrent();
        }

        private static IEnumerator WithKeyboard(Action<Keyboard> check)
        {
            var previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            var previousBackgroundInput = InputSystem.settings.backgroundBehavior;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                check(keyboard);
                yield return null;
            }
            finally
            {
                if (keyboard.added) InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
                InputSystem.settings.backgroundBehavior = previousBackgroundInput;
            }
        }

        private static TransformAdapter CreateAdapter(GameObject target)
        {
            var adapter = target.AddComponent<TransformAdapter>();
            SetField(adapter, "_value", new TransformComponent(target.transform));
            return adapter;
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private static void AssertPosition(Transform target, Vector3 expected) =>
            Assert.That(Vector3.Distance(target.position, expected), Is.LessThan(0.0001f));

        private GameObject CreateObject(string name)
        {
            var target = new GameObject(name);
            _objects.Add(target);
            return target;
        }

        private Simulation Track(Simulation simulation)
        {
            _simulations.Add(simulation);
            return simulation;
        }

        private static Entity Player(Simulation simulation) => simulation.World.Single(entity => entity.Has<PlayerTag>());
        private static Entity Player(World world) => world.Single(entity => entity.Has<PlayerTag>());

        private sealed class TestInput : IPlayerInput
        {
            public bool RestartPressed { get; set; }
            public MoveCommand ReadMove() => default;
        }
        private static Simulation CreateSimulation(LevelDefinition level) =>
            new Simulation(level);
    }
}
