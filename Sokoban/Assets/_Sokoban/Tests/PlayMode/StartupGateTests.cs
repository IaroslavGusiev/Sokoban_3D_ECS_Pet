using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using DefaultEcs;
using NUnit.Framework;
using Sokoban.Core;
using Sokoban.Core.Commands;
using Sokoban.Core.Components;
using Sokoban.Presentation;
using Sokoban.Presentation.Ecs.Components;
using UnityEngine;
using UnityEngine.TestTools;

namespace Sokoban.Tests.PlayMode
{
    public sealed class StartupGateTests
    {
        private readonly List<GameObject> _objects = new();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject target in _objects)
            {
                if (target != null) UnityEngine.Object.Destroy(target);
            }
            _objects.Clear();
            yield return null;
        }

        [Test]
        public void Status_TracksUnrequestedViewsAndGivesFailurePriority()
        {
            using var world = new World();
            Entity floor = world.CreateEntity();
            floor.Set<FloorTag>();
            floor.Set(new GridPosition(1, 2));
            Entity ignored = world.CreateEntity();
            ignored.Set(new GridPosition(3, 4));
            var provider = new Provider();
            using var layer = new ViewLayer(new LevelRoot(CreateObject("Root").transform), world, provider, new ViewFeatures());
            Assert.That(floor.Has<ViewPending>(), Is.False);
            Assert.That(layer.Status, Is.EqualTo(ViewStatus.Binding));
            ignored.Set<ViewFailed>();
            Assert.That(layer.Status, Is.EqualTo(ViewStatus.Failed));
            ignored.Remove<ViewFailed>();
            layer.Tick(0f);
            Assert.That(floor.Has<ViewPending>(), Is.True);
            Assert.That(layer.Status, Is.EqualTo(ViewStatus.Binding));
            provider.Complete(CreateObject("Prefab"));
            Assert.That(layer.Status, Is.EqualTo(ViewStatus.Ready));
            Assert.That(ignored.Has<TransformComponent>(), Is.False);
            floor.Remove<TransformComponent>();
            Assert.That(layer.Status, Is.EqualTo(ViewStatus.Binding));
        }

        [Test]
        public void Binding_TicksBothLayersButDoesNotReadMovementUntilPlaying()
        {
            var provider = new Provider();
            var input = new Input { Move = MoveCommand.Left };
            using var controller = CreateController(input, new Source(), provider);
            controller.Initialize();
            Simulation simulation = SimulationOf(controller);
            Entity player = simulation.World.Single(entity => entity.Has<PlayerTag>());
            Assert.That(StateOf(controller), Is.EqualTo("BindingViews"));
            simulation.Send(MoveCommand.Right);
            controller.Tick();
            Assert.That(input.Reads, Is.Zero);
            Assert.That(player.Get<GridPosition>(), Is.EqualTo(new GridPosition(3, 1)));
            Assert.That(player.Has<ViewPending>(), Is.True);
            provider.Complete(CreateObject("Prefab"));
            Transform view = player.Get<TransformComponent>().Transform;
            Assert.That(view.position, Is.EqualTo(new Vector3(3, 0, 1)),
                "Binding must use the latest position before the next projection tick.");
            view.position = Vector3.one * 50;
            controller.Tick();
            Assert.That(view.position, Is.EqualTo(new Vector3(3, 0, 1)));
            Assert.That(StateOf(controller), Is.EqualTo("Playing"));
            Assert.That(input.Reads, Is.Zero);
            controller.Tick();
            Assert.That(input.Reads, Is.EqualTo(1));
            Assert.That(player.Get<GridPosition>(), Is.EqualTo(new GridPosition(2, 1)));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void RestartAndNext_RemainAvailableWhileBindingOrFailed(bool failed, bool next)
        {
            var provider = new Provider();
            var source = new Source();
            var input = new Input();
            using var controller = CreateController(input, source, provider);
            controller.Initialize();
            Simulation old = SimulationOf(controller);
            if (failed)
            {
                provider.Fail();
                controller.Tick();
                Assert.That(StateOf(controller), Is.EqualTo("ViewsFailed"));
            }
            provider.Reset();
            input.RestartPressed = !next;
            input.NextLevelPressed = next;
            controller.Tick();
            input.RestartPressed = false;
            input.NextLevelPressed = false;
            Assert.That(SimulationOf(controller), Is.Not.SameAs(old));
            Assert.That(StateOf(controller), Is.EqualTo("BindingViews"));
            Assert.That(source.Requests, Is.EqualTo(next ? new[] { 0, 1 } : new[] { 0 }));
            Assert.That(input.Reads, Is.Zero);
            provider.Complete(CreateObject("Recovered prefab"));
            controller.Tick();
            Assert.That(StateOf(controller), Is.EqualTo("Playing"));
        }

        [Test]
        public void PlayingSkinFailure_PausesMovementAndRecoversThroughSkinInput()
        {
            var provider = new Provider();
            provider.Complete(CreateObject("Default prefab"));
            var input = new Input();
            var source = new Source();
            using var controller = CreateController(input, source, provider);
            controller.Initialize();
            controller.Tick();
            Assert.That(StateOf(controller), Is.EqualTo("Playing"));
            Simulation simulation = SimulationOf(controller);
            Entity player = simulation.World.Single(entity => entity.Has<PlayerTag>());
            provider.Reset();
            input.NextSkinPressed = true;
            input.Move = MoveCommand.Left;
            controller.Tick();
            input.NextSkinPressed = false;
            input.Move = default;
            Assert.That(StateOf(controller), Is.EqualTo("Playing"));
            Assert.That(player.Get<GridPosition>(), Is.EqualTo(new GridPosition(1, 1)));
            Assert.That(player.Has<ViewPending>(), Is.True);
            provider.Fail();
            controller.Tick();
            Assert.That(StateOf(controller), Is.EqualTo("ViewsFailed"));
            Assert.That(player.Has<ViewFailed>(), Is.True);
            Assert.That(player.Has<TransformComponent>(), Is.False);
            int reads = input.Reads;
            input.Move = MoveCommand.Right;
            controller.Tick();
            Assert.That(input.Reads, Is.EqualTo(reads));
            Assert.That(player.Get<GridPosition>(), Is.EqualTo(new GridPosition(1, 1)));
            provider.Reset();
            input.NextSkinPressed = true;
            controller.Tick();
            input.NextSkinPressed = false;
            Assert.That(StateOf(controller), Is.EqualTo("BindingViews"));
            Assert.That(player.Has<ViewFailed>(), Is.False);
            Assert.That(input.Reads, Is.EqualTo(reads));
            provider.Complete(CreateObject("Recovered Default prefab"));
            controller.Tick();
            Assert.That(StateOf(controller), Is.EqualTo("Playing"));
            Assert.That(input.Reads, Is.EqualTo(reads));
            Assert.That(player.Get<ViewSkin>().Id, Is.EqualTo(SkinId.Default));
            Assert.That(player.Has<TransformComponent>(), Is.True);
            controller.Tick();
            Assert.That(player.Get<GridPosition>(), Is.EqualTo(new GridPosition(2, 1)));
            Assert.That(SimulationOf(controller), Is.SameAs(simulation));
            Assert.That(source.Requests, Is.EqualTo(new[] { 0 }));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ViewFailure_HasPriorityOverCompletionInTheSameTick()
        {
            var provider = new Provider();
            provider.Complete(CreateObject("Default prefab"));
            var input = new Input();
            var source = new Source();
            using var controller = CreateController(input, source, provider);
            controller.Initialize();
            controller.Tick();
            Simulation simulation = SimulationOf(controller);
            simulation.World.Single(entity => entity.Has<BoxTag>()).Set(new GridPosition(4, 2));
            provider.FailAlternatePlayer = true;
            input.NextSkinPressed = true;
            controller.Tick();
            input.NextSkinPressed = false;
            Assert.That(simulation.IsCompleted, Is.True);
            Assert.That(StateOf(controller), Is.EqualTo("ViewsFailed"));
            controller.Tick();
            Assert.That(StateOf(controller), Is.EqualTo("ViewsFailed"));
            Assert.That(source.Requests, Is.EqualTo(new[] { 0 }));
            input.NextSkinPressed = true;
            controller.Tick();
            input.NextSkinPressed = false;
            Assert.That(StateOf(controller), Is.EqualTo("Playing"));
            controller.Tick();
            Assert.That(StateOf(controller), Is.EqualTo("Completed"));
        }

        [Test]
        public void InitialDefinitionFailure_CanBeRetriedWithRestart()
        {
            var provider = new Provider();
            provider.Complete(CreateObject("Prefab"));
            var source = new Source { Fail = true };
            var input = new Input();
            using var controller = CreateController(input, source, provider);
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: planned definition failure"));
            controller.Initialize();
            Assert.That(StateOf(controller), Is.EqualTo("LevelFailed"));
            Assert.That(SimulationOf(controller), Is.Null);
            source.Fail = false;
            input.RestartPressed = true;
            controller.Tick();
            input.RestartPressed = false;
            Assert.That(source.Requests, Is.EqualTo(new[] { 0, 0 }));
            Assert.That(StateOf(controller), Is.EqualTo("BindingViews"));
            controller.Tick();
            Assert.That(StateOf(controller), Is.EqualTo("Playing"));
        }

        [Test]
        public void SkinSwitch_DoesNotPausePlayingAndSurvivesRestartAndNext()
        {
            var provider = new Provider();
            provider.Complete(CreateObject("Default skin prefab"));
            var source = new Source();
            var input = new Input();
            using var controller = CreateController(input, source, provider);
            controller.Initialize();
            controller.Tick();
            Simulation simulation = SimulationOf(controller);
            Entity player = simulation.World.Single(entity => entity.Has<PlayerTag>());
            provider.Reset();
            input.NextSkinPressed = true;
            input.Move = MoveCommand.Left;
            controller.Tick();
            input.NextSkinPressed = false;
            input.Move = default;
            Assert.That(StateOf(controller), Is.EqualTo("Playing"));
            Assert.That(player.Get<GridPosition>(), Is.EqualTo(new GridPosition(1, 1)));
            Assert.That(player.Get<ViewSkin>().Id, Is.EqualTo(SkinId.Alternate));
            Assert.That(player.Has<ViewPending>(), Is.True);
            int reads = input.Reads;
            controller.Tick();
            Assert.That(input.Reads, Is.EqualTo(reads + 1));
            Assert.That(StateOf(controller), Is.EqualTo("Playing"));
            provider.Complete(CreateObject("Alternate skin prefab"));
            input.RestartPressed = true;
            controller.Tick();
            input.RestartPressed = false;
            Assert.That(SimulationOf(controller), Is.Not.SameAs(simulation));
            Assert.That(SimulationOf(controller).World.Single(entity => entity.Has<PlayerTag>()).Get<ViewSkin>().Id,
                Is.EqualTo(SkinId.Alternate));
            Assert.That(provider.Requests.Last(key => key.Kind == ViewKind.Player).Skin, Is.EqualTo(SkinId.Alternate));
            controller.Tick();
            input.NextLevelPressed = true;
            controller.Tick();
            input.NextLevelPressed = false;
            Assert.That(SimulationOf(controller).World.Single(entity => entity.Has<PlayerTag>()).Get<ViewSkin>().Id,
                Is.EqualTo(SkinId.Alternate));
            Assert.That(provider.Requests.Last(key => key.Kind == ViewKind.Player).Skin, Is.EqualTo(SkinId.Alternate));
            Assert.That(source.Requests, Is.EqualTo(new[] { 0, 1 }));
        }

        [Test]
        public void PersistedSkinViewFailure_RecoversThroughSkinInputWithoutRestart()
        {
            var provider = new Provider();
            provider.Complete(CreateObject("Default skin prefab"));
            var input = new Input();
            var source = new Source();
            using var controller = CreateController(input, source, provider);
            controller.Initialize();
            controller.Tick();
            input.NextSkinPressed = true;
            controller.Tick();
            input.NextSkinPressed = false;
            provider.FailAlternatePlayer = true;
            input.RestartPressed = true;
            controller.Tick();
            input.RestartPressed = false;
            controller.Tick();
            Assert.That(StateOf(controller), Is.EqualTo("ViewsFailed"));
            Entity player = SimulationOf(controller).World.Single(entity => entity.Has<PlayerTag>());
            Assert.That(player.Get<ViewSkin>().Id, Is.EqualTo(SkinId.Alternate));
            Assert.That(player.Has<ViewFailed>(), Is.True);
            int reads = input.Reads;
            provider.Reset();
            input.NextSkinPressed = true;
            controller.Tick();
            input.NextSkinPressed = false;
            Assert.That(StateOf(controller), Is.EqualTo("BindingViews"));
            Assert.That(player.Has<ViewFailed>(), Is.False);
            Assert.That(player.Has<ViewPending>(), Is.True);
            provider.Complete(CreateObject("Recovered Default skin prefab"));
            controller.Tick();
            Assert.That(StateOf(controller), Is.EqualTo("Playing"));
            Assert.That(input.Reads, Is.EqualTo(reads));
            Assert.That(player.Get<ViewSkin>().Id, Is.EqualTo(SkinId.Default));
            Assert.That(player.Has<ViewFailed>(), Is.False);
            Assert.That(player.Has<TransformComponent>(), Is.True);
            Assert.That(source.Requests, Is.EqualTo(new[] { 0 }));
            LogAssert.NoUnexpectedReceived();
        }

        [TestCase("initial", false)]
        [TestCase("initial", true)]
        [TestCase("restart", false)]
        [TestCase("restart", true)]
        [TestCase("next", false)]
        [TestCase("next", true)]
        public void RunConstructionFailure_ReleasesPartialRunAndAllowsRecovery(string trigger, bool recoverWithNext)
        {
            var provider = new Provider();
            provider.Complete(CreateObject("Prefab"));
            var input = new Input();
            var source = new Source();
            using var controller = CreateController(input, source, provider);
            bool previousWorldDisposed = false;
            if (trigger != "initial")
            {
                controller.Initialize();
                SimulationOf(controller).World.SubscribeWorldDisposed(_ => previousWorldDisposed = true);
            }

            var existingRoots = new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(target => target.name == "Level view"));
            FieldInfo rootField = typeof(GameplayController).GetField("_root", BindingFlags.Instance | BindingFlags.NonPublic);
            object root = rootField.GetValue(controller);
            // Inject a construction fault without adding a production factory just for tests.
            rootField.SetValue(controller, null);
            input.RestartPressed = trigger == "restart";
            input.NextLevelPressed = trigger == "next";
            LogAssert.Expect(LogType.Exception, new Regex("NullReferenceException"));
            if (trigger == "initial") controller.Initialize();
            else Assert.DoesNotThrow(controller.Tick);
            input.RestartPressed = false;
            input.NextLevelPressed = false;

            Assert.That(StateOf(controller), Is.EqualTo("LevelFailed"));
            Assert.That(SimulationOf(controller), Is.Null);
            Assert.That(typeof(GameplayController).GetField("_run", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(controller), Is.Null);
            Assert.That(previousWorldDisposed, Is.EqualTo(trigger != "initial"));
            foreach (GameObject failedRoot in Resources.FindObjectsOfTypeAll<GameObject>()
                         .Where(target => target.name == "Level view" && !existingRoots.Contains(target)))
                Assert.That(failedRoot.activeSelf, Is.False, "A failed view constructor must tear down its root.");
            Assert.DoesNotThrow(controller.Tick);

            rootField.SetValue(controller, root);
            int requestsBeforeRecovery = source.Requests.Count;
            input.RestartPressed = !recoverWithNext;
            input.NextLevelPressed = recoverWithNext;
            controller.Tick();
            input.RestartPressed = false;
            input.NextLevelPressed = false;
            Assert.That(StateOf(controller), Is.EqualTo("BindingViews"));
            Assert.That(SimulationOf(controller), Is.Not.Null);
            Assert.That(source.Requests.Count, Is.EqualTo(requestsBeforeRecovery + 1));
            controller.Tick();
            Assert.That(StateOf(controller), Is.EqualTo("Playing"));
        }

        [Test]
        public void SimulationConstructionFailure_EntersFailedAndRetainsRecoveryControls()
        {
            var provider = new Provider();
            provider.Complete(CreateObject("Prefab"));
            var source = new Source { ReturnNull = true };
            var input = new Input();
            using var controller = CreateController(input, source, provider);
            LogAssert.Expect(LogType.Exception, new Regex("ArgumentNullException"));
            controller.Initialize();
            Assert.That(StateOf(controller), Is.EqualTo("LevelFailed"));
            Assert.That(SimulationOf(controller), Is.Null);
            Assert.DoesNotThrow(controller.Tick);
            source.ReturnNull = false;
            input.RestartPressed = true;
            controller.Tick();
            input.RestartPressed = false;
            controller.Tick();
            Assert.That(StateOf(controller), Is.EqualTo("Playing"));
        }

        private GameplayController CreateController(Input input, Source source, Provider provider) =>
            new(input, source, new ViewFeatures(), new LevelRoot(CreateObject("Level root").transform), provider);

        [Test]
        public void SkinCycle_VisitsEveryValueAndWrapsAfterExactlyOneCycle()
        {
            var values = (SkinId[])Enum.GetValues(typeof(SkinId));
            foreach (SkinId start in values)
            {
                SkinId skin = start;
                var seen = new HashSet<SkinId> { start };
                for (int step = 1; step <= values.Length; step++)
                {
                    SkinId next = SkinCycle.Next(skin);
                    if (values.Length > 1) Assert.That(next, Is.Not.EqualTo(skin));
                    if (step < values.Length) Assert.That(seen.Add(next), Is.True);
                    else Assert.That(next, Is.EqualTo(start));
                    skin = next;
                }
                Assert.That(seen, Is.EquivalentTo(values));
            }
        }

        [Test]
        public void CompletionTimer_StartsOnlyOneLoadWhenSourceCompletesSynchronously()
        {
            var provider = new Provider();
            provider.Complete(CreateObject("Prefab"));
            var input = new Input();
            var source = new Source();
            using var controller = CreateController(input, source, provider);
            controller.Initialize();
            controller.Tick();
            Simulation simulation = SimulationOf(controller);
            simulation.World.Single(entity => entity.Has<BoxTag>()).Set(new GridPosition(4, 2));
            controller.Tick();
            Assert.That(StateOf(controller), Is.EqualTo("Completed"));
            typeof(GameplayController).GetField("_completionTimer", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(controller, 0.75f);
            int reads = input.Reads;
            input.NextLevelPressed = true;
            input.RestartPressed = true;
            controller.Tick();
            Assert.That(source.Requests, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(StateOf(controller), Is.EqualTo("BindingViews"));
            Assert.That(input.Reads, Is.EqualTo(reads));
        }

        private GameObject CreateObject(string name)
        {
            var target = new GameObject(name);
            _objects.Add(target);
            return target;
        }

        private static Simulation SimulationOf(GameplayController controller)
        {
            object run = typeof(GameplayController).GetField("_run", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            return (Simulation)run?.GetType().GetField("_simulation", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(run);
        }

        private static string StateOf(GameplayController controller) =>
            typeof(GameplayController).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller).ToString();

        private sealed class Input : IPlayerInput
        {
            public int Reads { get; private set; }
            public MoveCommand Move { get; set; }
            public bool RestartPressed { get; set; }
            public bool NextLevelPressed { get; set; }
            public bool NextSkinPressed { get; set; }
            public MoveCommand ReadMove()
            {
                Reads++;
                return Move;
            }
        }

        private sealed class Source : ILevelSource
        {
            public int Count => 2;
            public bool Fail { get; set; }
            public bool ReturnNull { get; set; }
            public List<int> Requests { get; } = new();
            public UniTask<LevelDefinition> LoadAsync(int index, CancellationToken ct)
            {
                Requests.Add(index);
                if (ReturnNull) return UniTask.FromResult<LevelDefinition>(null);
                return Fail
                    ? UniTask.FromException<LevelDefinition>(new InvalidOperationException("planned definition failure"))
                    : UniTask.FromResult(new LevelDefinition(5, 3, new GridPosition(2, 1), Array.Empty<GridPosition>(),
                        new[] { new GridPosition(0, 0) }, new[] { new GridPosition(4, 2) }));
            }
        }

        private sealed class Provider : IViewProvider
        {
            private UniTaskCompletionSource<GameObject> _pending = new();
            public List<ViewKey> Requests { get; } = new();
            public bool FailAlternatePlayer { get; set; }
            public UniTask<GameObject> LoadAsync(ViewKey key, CancellationToken ct)
            {
                Requests.Add(key);
                if (FailAlternatePlayer && key == new ViewKey(ViewKind.Player, SkinId.Alternate))
                    return UniTask.FromException<GameObject>(new InvalidOperationException("planned alternate player failure"));
                return _pending.Task;
            }
            public void Complete(GameObject prefab) => _pending.TrySetResult(prefab);
            public void Fail() => _pending.TrySetException(new InvalidOperationException("planned view failure"));
            public void Reset() => _pending = new UniTaskCompletionSource<GameObject>();
        }
    }
}
