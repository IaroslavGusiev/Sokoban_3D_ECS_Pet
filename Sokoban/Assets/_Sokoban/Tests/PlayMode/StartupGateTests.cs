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
        private readonly List<UnityEngine.Object> _objects = new();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (UnityEngine.Object target in _objects)
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
            using var layer = new ViewLayer(new LevelRoot(CreateObject("Root").transform), world, provider, new ViewFeatures(), new RunOptions(SkinId.Default, 60f, 0.16f));
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
        public void Binding_TicksViewsAndPreservesQueuedMovementUntilPlaying()
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
            Assert.That(player.Get<GridPosition>(), Is.EqualTo(new GridPosition(2, 1)));
            Assert.That(player.Has<ViewPending>(), Is.True);
            provider.Complete(CreateObject("Prefab"));
            Transform view = player.Get<TransformComponent>().Transform;
            Assert.That(view.position, Is.EqualTo(new Vector3(2, 0, 1)),
                "Binding must use the latest position before the next projection tick.");
            view.position = Vector3.one * 50;
            controller.Tick();
            Assert.That(view.position, Is.EqualTo(new Vector3(2, 0, 1)));
            Assert.That(StateOf(controller), Is.EqualTo("Playing"));
            Assert.That(input.Reads, Is.Zero);
            controller.Tick();
            Assert.That(input.Reads, Is.EqualTo(1));
            Assert.That(player.Get<GridPosition>(), Is.EqualTo(new GridPosition(3, 1)));
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
            Assert.That(player.Get<ViewSkin>().Id, Is.EqualTo(SkinId.Punk));
            Assert.That(player.Has<TransformComponent>(), Is.True);
            controller.Tick(0.16f);
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
            Assert.That(controller.Results, Is.Empty);
            input.NextSkinPressed = true;
            controller.Tick();
            input.NextSkinPressed = false;
            Assert.That(StateOf(controller), Is.EqualTo("Playing"));
            controller.Tick();
            Assert.That(StateOf(controller), Is.EqualTo("Completed"));
            Assert.That(controller.Results.Count, Is.EqualTo(1));
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
            Assert.That(player.Get<ViewSkin>().Id, Is.EqualTo(SkinId.Punk));
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
            object factory = typeof(GameplayController).GetField("_runFactory", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            FieldInfo rootField = typeof(LevelRunFactory).GetField("_root", BindingFlags.Instance | BindingFlags.NonPublic);
            object root = rootField.GetValue(factory);
            rootField.SetValue(factory, null);
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

            rootField.SetValue(factory, root);
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
            LogAssert.Expect(LogType.Exception, new Regex("NullReferenceException"));
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

        private GameplayController CreateController(Input input, Source source, Provider provider, float timeLimitSeconds = 60f, float completionDelay = 0.75f, TestStaticData staticData = null, float moveDuration = 0.16f)
        {
            GameplaySettings settings = TestGameplaySettings.Create(timeLimitSeconds, completionDelay, moveDuration);
            _objects.Add(settings);
            var factory = new LevelRunFactory(new LevelRoot(CreateObject("Level root").transform), provider, new ViewFeatures(), new LevelPopulator());
            staticData ??= new TestStaticData();
            staticData.Gameplay = settings;
            return new GameplayController(input, source, factory, staticData);
        }

        [Test]
        public void ConfigLoading_BlocksLevelRequestsAndInputUntilReady()
        {
            var data = new TestStaticData { Pending = new UniTaskCompletionSource() };
            var source = new Source();
            var input = new Input { RestartPressed = true, NextLevelPressed = true, NextSkinPressed = true };
            var provider = new Provider();
            provider.Complete(CreateObject("Prefab"));
            using var controller = CreateController(input, source, provider, staticData: data);
            controller.Initialize();
            controller.Tick();
            Assert.That(StateOf(controller), Is.EqualTo("Loading"));
            Assert.That(source.Requests, Is.Empty);
            Assert.That(input.Reads, Is.Zero);
            Assert.That(SimulationOf(controller), Is.Null);
            input.RestartPressed = input.NextLevelPressed = input.NextSkinPressed = false;
            data.Pending.TrySetResult();
            Assert.That(StateOf(controller), Is.EqualTo("BindingViews"));
            Assert.That(source.Requests, Is.EqualTo(new[] { 0 }));
            Assert.That(controller.RemainingSeconds, Is.EqualTo(60f));
        }

        [Test]
        public void ConfigLoadFailure_RestartRetriesConfigsBeforeLoadingTheLevel()
        {
            var data = new TestStaticData { Pending = new UniTaskCompletionSource() };
            var source = new Source();
            var input = new Input();
            var provider = new Provider();
            provider.Complete(CreateObject("Prefab"));
            using var controller = CreateController(input, source, provider, staticData: data);
            controller.Initialize();
            LogAssert.Expect(LogType.Exception, new Regex("planned config failure"));
            data.Pending.TrySetException(new InvalidOperationException("planned config failure"));
            Assert.That(StateOf(controller), Is.EqualTo("LevelFailed"));
            Assert.That(source.Requests, Is.Empty);
            data.Pending = null;
            input.RestartPressed = true;
            controller.Tick();
            Assert.That(StateOf(controller), Is.EqualTo("BindingViews"));
            Assert.That(data.LoadCalls, Is.EqualTo(2));
            Assert.That(source.Requests, Is.EqualTo(new[] { 0 }));
        }

        [Test]
        public void DisposalDuringConfigLoad_LateCompletionCannotCreateARun()
        {
            var data = new TestStaticData { Pending = new UniTaskCompletionSource() };
            var source = new Source();
            var controller = CreateController(new Input(), source, new Provider(), staticData: data);
            controller.Initialize();
            controller.Dispose();
            data.Pending.TrySetResult();
            Assert.That(source.Requests, Is.Empty);
            Assert.That(SimulationOf(controller), Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Countdown_WaitsForViewsThenTimesOutAndRestartsWithFullBudget()
        {
            var provider = new Provider();
            var source = new Source();
            var input = new Input();
            using var controller = CreateController(input, source, provider, 1f);
            controller.Initialize();
            controller.Tick(20f);
            Assert.That(controller.RemainingSeconds, Is.EqualTo(1f));
            Assert.That(StateOf(controller), Is.EqualTo("BindingViews"));
            provider.Complete(CreateObject("Prefab"));
            controller.Tick(20f);
            Assert.That(StateOf(controller), Is.EqualTo("Playing"));
            Assert.That(controller.RemainingSeconds, Is.EqualTo(1f));
            Simulation previous = SimulationOf(controller);
            Entity previousLevel = previous.World.Single(entity => entity.Has<LevelStateTag>());
            Assert.That(previousLevel.Get<LevelTimer>().RemainingSeconds, Is.EqualTo(1f));
            bool disposed = false;
            using var subscription = previous.World.SubscribeWorldDisposed(_ => disposed = true);
            controller.Tick(1f);
            Assert.That(StateOf(controller), Is.EqualTo("BindingViews"));
            Assert.That(disposed, Is.True);
            Assert.That(SimulationOf(controller), Is.Not.SameAs(previous));
            Entity freshLevel = SimulationOf(controller).World.Single(entity => entity.Has<LevelStateTag>());
            Assert.That(freshLevel.World, Is.Not.SameAs(previous.World));
            Assert.That(freshLevel.Get<MoveCount>().Value, Is.Zero);
            Assert.That(freshLevel.Has<LevelTimedOutTag>() || freshLevel.Has<LevelCompletedTag>(), Is.False);
            Assert.That(controller.RemainingSeconds, Is.EqualTo(1f));
            Assert.That(source.Requests, Is.EqualTo(new[] { 0 }));
            controller.Tick(20f);
            Assert.That(StateOf(controller), Is.EqualTo("Playing"));
            Assert.That(controller.RemainingSeconds, Is.EqualTo(1f));
        }

        [Test]
        public void Countdown_AutomaticRestartSuppressesSameFrameCommandsAndPreservesSkin()
        {
            var provider = new Provider();
            provider.Complete(CreateObject("Prefab"));
            var source = new Source();
            var input = new Input();
            using var controller = CreateController(input, source, provider, 1f);
            controller.Initialize();
            controller.Tick();
            input.NextSkinPressed = true;
            controller.Tick();
            input.NextSkinPressed = false;
            Simulation previous = SimulationOf(controller);
            previous.Tick(1f);
            input.NextLevelPressed = true;
            input.RestartPressed = true;
            controller.Tick();
            Assert.That(StateOf(controller), Is.EqualTo("BindingViews"));
            Assert.That(SimulationOf(controller), Is.Not.SameAs(previous));
            Assert.That(controller.RemainingSeconds, Is.EqualTo(1f));
            Assert.That(source.Requests, Is.EqualTo(new[] { 0 }));
            var player = SimulationOf(controller).World.Single(entity => entity.Has<PlayerTag>());
            Assert.That(player.Get<ViewSkin>().Id, Is.EqualTo(SkinId.Alternate));
        }

        [Test]
        public void Countdown_PausesDuringViewFailureAndResumesAfterRecovery()
        {
            var provider = new Provider();
            provider.Complete(CreateObject("Prefab"));
            var source = new Source();
            var input = new Input();
            using var controller = CreateController(input, source, provider, 5f);
            controller.Initialize();
            controller.Tick(20f);
            controller.Tick(1f);
            Assert.That(controller.RemainingSeconds, Is.EqualTo(4f));
            var player = SimulationOf(controller).World.Single(entity => entity.Has<PlayerTag>());
            player.Set<ViewFailed>();
            controller.Tick(1f);
            Assert.That(StateOf(controller), Is.EqualTo("ViewsFailed"));
            Assert.That(controller.RemainingSeconds, Is.EqualTo(3f));
            controller.Tick(20f);
            Assert.That(controller.RemainingSeconds, Is.EqualTo(3f));
            player.Remove<ViewFailed>();
            controller.Tick(20f);
            Assert.That(StateOf(controller), Is.EqualTo("Playing"));
            Assert.That(controller.RemainingSeconds, Is.EqualTo(3f));
            controller.Tick(0.5f);
            Assert.That(controller.RemainingSeconds, Is.EqualTo(2.5f));
        }

        [Test]
        public void Countdown_ContinuesDuringPendingSkinRebind()
        {
            var provider = new Provider();
            provider.Complete(CreateObject("Prefab"));
            var input = new Input();
            using var controller = CreateController(input, new Source(), provider, 5f);
            controller.Initialize();
            controller.Tick();
            provider.Reset();
            input.NextSkinPressed = true;
            controller.Tick();
            input.NextSkinPressed = false;
            float before = controller.RemainingSeconds;
            controller.Tick(1f);
            Assert.That(StateOf(controller), Is.EqualTo("Playing"));
            Assert.That(controller.RemainingSeconds, Is.EqualTo(before - 1f).Within(0.0001f));
            var player = SimulationOf(controller).World.Single(entity => entity.Has<PlayerTag>());
            Assert.That(player.Has<ViewPending>(), Is.True);
        }

        [TestCase(1f)]
        [TestCase(5f)]
        [TestCase(6f)]
        public void Completion_StopsClockAndNextLevelStartsWithFullBudget(float winningDelta)
        {
            var provider = new Provider();
            provider.Complete(CreateObject("Prefab"));
            var source = new Source();
            using var controller = CreateController(new Input(), source, provider, 5f);
            controller.Initialize();
            controller.Tick(20f);
            Simulation simulation = SimulationOf(controller);
            Entity previousLevel = simulation.World.Single(entity => entity.Has<LevelStateTag>());
            bool disposed = false;
            using var subscription = simulation.World.SubscribeWorldDisposed(_ => disposed = true);
            simulation.World.Single(entity => entity.Has<BoxTag>()).Set(new GridPosition(4, 2));
            controller.Tick(winningDelta);
            Assert.That(StateOf(controller), Is.EqualTo("Completed"));
            Assert.That(controller.RemainingSeconds, Is.EqualTo(5f));
            Assert.That(previousLevel.Has<LevelCompletedTag>(), Is.True);
            controller.Tick(0.5f);
            Assert.That(controller.RemainingSeconds, Is.EqualTo(5f));
            controller.Tick(0.25f);
            Assert.That(source.Requests, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(StateOf(controller), Is.EqualTo("BindingViews"));
            Assert.That(controller.RemainingSeconds, Is.EqualTo(5f));
            Assert.That(disposed, Is.True);
            Entity freshLevel = SimulationOf(controller).World.Single(entity => entity.Has<LevelStateTag>());
            Assert.That(freshLevel.World, Is.Not.SameAs(simulation.World));
            Assert.That(freshLevel.Get<MoveCount>().Value, Is.Zero);
            Assert.That(freshLevel.Has<LevelCompletedTag>() || freshLevel.Has<LevelTimedOutTag>(), Is.False);
        }

        [Test]
        public void Completion_EachLevelWaitsForTheFullCompletionDelay()
        {
            var provider = new Provider();
            provider.Complete(CreateObject("Prefab"));
            var source = new Source();
            using var controller = CreateController(new Input(), source, provider);
            controller.Initialize();
            controller.Tick(0f);
            SimulationOf(controller).World.Single(entity => entity.Has<BoxTag>()).Set(new GridPosition(4, 2));
            controller.Tick(0f);
            controller.Tick(0.75f);
            Assert.That(source.Requests, Is.EqualTo(new[] { 0, 1 }));

            controller.Tick(0f);
            SimulationOf(controller).World.Single(entity => entity.Has<BoxTag>()).Set(new GridPosition(4, 2));
            controller.Tick(0f);
            Assert.That(StateOf(controller), Is.EqualTo("Completed"));
            controller.Tick(0.5f);
            Assert.That(source.Requests, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(StateOf(controller), Is.EqualTo("Completed"));
            controller.Tick(0.25f);
            Assert.That(source.Requests, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(StateOf(controller), Is.EqualTo("Finished"));
        }


        [TestCase(0f)]
        [TestCase(2f)]
        public void Completion_UsesSettingsDelay(float delay)
        {
            var provider = new Provider();
            provider.Complete(CreateObject("Prefab"));
            var source = new Source();
            using var controller = CreateController(new Input(), source, provider, 7f, delay);
            Assert.That(controller.RemainingSeconds, Is.EqualTo(7f));
            controller.Initialize();
            controller.Tick(0f);
            SimulationOf(controller).World.Single(entity => entity.Has<BoxTag>()).Set(new GridPosition(4, 2));
            controller.Tick(0.25f);
            Assert.That(StateOf(controller), Is.EqualTo("Completed"));
            if (delay > 0f)
            {
                controller.Tick(delay - 0.25f);
                Assert.That(source.Requests, Is.EqualTo(new[] { 0 }));
                Assert.That(controller.RemainingSeconds, Is.EqualTo(7f));
                controller.Tick(0.25f);
            }
            else controller.Tick(0f);
            Assert.That(source.Requests, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(controller.RemainingSeconds, Is.EqualTo(7f));
        }

        [TestCase("BindingViews")]
        [TestCase("ViewsFailed")]
        [TestCase("Completed")]
        [TestCase("LevelFailed")]
        [TestCase("Finished")]
        public void NonPlayingState_PreservesCoreStateWithoutReadingMovement(string state)
        {
            var provider = new Provider();
            if (state != "BindingViews" && state != "ViewsFailed")
            {
                provider.Complete(CreateObject("Prefab"));
            }
            var input = new Input();
            var source = new Source();
            using var controller = CreateController(input, source, provider, 5f);
            controller.Initialize();
            if (state == "ViewsFailed")
            {
                provider.Fail();
                controller.Tick(0f);
            }
            else if (state == "LevelFailed")
            {
                source.Fail = true;
                input.NextLevelPressed = true;
                LogAssert.Expect(LogType.Exception, new Regex("planned definition failure"));
                controller.Tick(0f);
                input.NextLevelPressed = false;
            }
            else if (state == "Completed" || state == "Finished")
            {
                CompleteCurrentLevel(controller);
                if (state == "Finished")
                {
                    controller.Tick(0.75f);
                    CompleteCurrentLevel(controller);
                    controller.Tick(0.75f);
                }
            }
            Assert.That(controller.Phase.ToString(), Is.EqualTo(state));
            Simulation simulation = SimulationOf(controller);
            var player = simulation.World.Single(entity => entity.Has<PlayerTag>());
            GridPosition position = player.Get<GridPosition>();
            simulation.Send(MoveCommand.Left);
            bool pending = player.Has<MoveRequest>();
            int reads = input.Reads;
            float remaining = simulation.RemainingSeconds;
            Assert.That(controller.CanUndo, Is.False);
            controller.RequestUndo();
            input.UndoPressed = true;
            controller.Tick(0.25f);
            input.UndoPressed = false;
            Assert.That(player.Has<MoveRequest>(), Is.EqualTo(pending));
            Assert.That(player.Get<GridPosition>(), Is.EqualTo(position));
            Assert.That(simulation.RemainingSeconds, Is.EqualTo(remaining));
            Assert.That(input.Reads, Is.EqualTo(reads));
        }

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
            int reads = input.Reads;
            input.NextLevelPressed = true;
            input.RestartPressed = true;
            controller.Tick(0.75f);
            Assert.That(source.Requests, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(StateOf(controller), Is.EqualTo("BindingViews"));
            Assert.That(input.Reads, Is.EqualTo(reads));
        }

        [Test]
        public void LastLevel_FinishesWithOneResultPerCompletionAndRestartClearsResults()
        {
            var provider = new Provider();
            provider.Complete(CreateObject("Prefab"));
            var input = new Input();
            var source = new Source { Definition = LevelParser.Parse("#####\n#@$.#\n#####") };
            using var controller = CreateController(input, source, provider);
            IGameplayStatus status = controller;
            Assert.That(status.LevelCount, Is.Zero);
            controller.Initialize();
            for (int index = 0; index < 2; index++)
            {
                controller.Tick();
                Assert.That(status.LevelNumber, Is.EqualTo(index + 1));
                Assert.That(status.LevelCount, Is.EqualTo(2));
                Assert.That(status.Moves, Is.Zero);
                input.Move = MoveCommand.Right;
                controller.Tick();
                input.Move = default;
                Assert.That(status.Phase, Is.EqualTo(GameplayPhase.Completed));
                Assert.That(status.Moves, Is.EqualTo(1));
                Assert.That(status.Results.Count, Is.EqualTo(index + 1));
                Assert.That(status.Results[index].Moves, Is.EqualTo(1));
                controller.Tick(0.75f);
            }
            Simulation last = SimulationOf(controller);
            Assert.That(status.Phase, Is.EqualTo(GameplayPhase.Finished));
            int reads = input.Reads;
            float remaining = status.RemainingSeconds;
            input.Move = MoveCommand.Left;
            input.NextLevelPressed = true;
            input.NextSkinPressed = true;
            controller.Tick();
            input.NextSkinPressed = false;
            controller.Tick();
            Assert.That(status.Phase, Is.EqualTo(GameplayPhase.Finished));
            Assert.That(status.Results.Count, Is.EqualTo(2));
            Assert.That(status.RemainingSeconds, Is.EqualTo(remaining));
            Assert.That(input.Reads, Is.EqualTo(reads));
            Assert.That(SimulationOf(controller), Is.SameAs(last));
            Assert.That(last.World.Single(entity => entity.Has<PlayerTag>()).Get<ViewSkin>().Id, Is.EqualTo(SkinId.Alternate));
            Assert.That(source.Requests, Is.EqualTo(new[] { 0, 1 }));
            input.RestartPressed = true;
            controller.Tick();
            Assert.That(status.Phase, Is.EqualTo(GameplayPhase.BindingViews));
            Assert.That(status.LevelNumber, Is.EqualTo(1));
            Assert.That(status.Moves, Is.Zero);
            Assert.That(status.Results, Is.Empty);
            Assert.That(source.Requests, Is.EqualTo(new[] { 0, 1, 0 }));
            Assert.That(SimulationOf(controller), Is.Not.SameAs(last));
        }

        [Test]
        public void RestartTimeoutAndDevLevelCycling_DoNotRecordResults()
        {
            var provider = new Provider();
            provider.Complete(CreateObject("Prefab"));
            var source = new Source();
            var input = new Input();
            using var controller = CreateController(input, source, provider);
            controller.Initialize();
            controller.Tick();
            input.RestartPressed = true;
            controller.Tick();
            input.RestartPressed = false;
            input.NextLevelPressed = true;
            controller.Tick();
            controller.Tick();
            input.NextLevelPressed = false;
            controller.Tick();
            controller.Tick(60f);
            Assert.That(controller.Phase, Is.EqualTo(GameplayPhase.BindingViews));
            Assert.That(controller.Results, Is.Empty);
            Assert.That(source.Requests, Is.EqualTo(new[] { 0, 1, 0 }));
        }

        [Test]
        public void Commands_CoalesceWithKeyboardAndAreConsumedOnce()
        {
            var provider = new Provider();
            provider.Complete(CreateObject("Prefab"));
            var input = new Input();
            using var controller = CreateController(input, new Source(), provider);
            controller.Initialize();
            controller.Tick(0f);
            Simulation previous = SimulationOf(controller);
            controller.RequestRestart();
            controller.RequestRestart();
            controller.RequestNextSkin();
            controller.RequestNextSkin();
            input.RestartPressed = true;
            input.NextSkinPressed = true;
            controller.Tick(0f);
            Simulation replacement = SimulationOf(controller);
            Assert.That(replacement, Is.Not.SameAs(previous));
            Assert.That(replacement.World.Single(e => e.Has<PlayerTag>()).Get<ViewSkin>().Id, Is.EqualTo(SkinId.Alternate));
            input.RestartPressed = false;
            input.NextSkinPressed = false;
            controller.Tick(0f);
            Assert.That(SimulationOf(controller), Is.SameAs(replacement));
            Assert.That(controller.Phase, Is.EqualTo(GameplayPhase.Playing));
        }

        [Test]
        public void Commands_DuringLoadingAreRejectedWithoutDelayedReplay()
        {
            var provider = new Provider();
            provider.Complete(CreateObject("Prefab"));
            var data = new TestStaticData { Pending = new UniTaskCompletionSource() };
            var source = new Source();
            using var controller = CreateController(new Input(), source, provider, staticData: data);
            controller.Initialize();
            Assert.That(controller.CanRestart || controller.CanSwitchSkin || controller.CanUndo, Is.False);
            controller.RequestRestart();
            controller.RequestNextSkin();
            controller.RequestUndo();
            controller.Tick(0f);
            data.Pending.TrySetResult();
            Simulation initial = SimulationOf(controller);
            controller.Tick(0f);
            Assert.That(SimulationOf(controller), Is.SameAs(initial));
            Assert.That(source.Requests, Is.EqualTo(new[] { 0 }));
            Assert.That(initial.World.Single(e => e.Has<PlayerTag>()).Get<ViewSkin>().Id, Is.EqualTo(SkinId.Default));
        }

        [Test]
        public void SkinCommand_RecoversAViewFailureWithoutRestart()
        {
            var provider = new Provider { FailAlternatePlayer = true };
            provider.Complete(CreateObject("Prefab"));
            using var controller = CreateController(new Input(), new Source(), provider);
            controller.Initialize();
            controller.Tick(0f);
            Simulation original = SimulationOf(controller);
            controller.RequestNextSkin();
            controller.Tick(0f);
            Assert.That(controller.Phase, Is.EqualTo(GameplayPhase.ViewsFailed));
            Assert.That(controller.CanSwitchSkin, Is.True);
            controller.RequestNextSkin();
            controller.Tick(0f);
            Assert.That(controller.Phase, Is.EqualTo(GameplayPhase.Playing));
            Assert.That(SimulationOf(controller), Is.SameAs(original));
        }

        [Test]
        public void ReplayCommand_FromFinishedClearsResultsAndStartsFirstLevel()
        {
            var provider = new Provider();
            provider.Complete(CreateObject("Prefab"));
            var source = new Source();
            using var controller = CreateController(new Input(), source, provider);
            controller.Initialize();
            CompleteCurrentLevel(controller);
            controller.Tick(0.75f);
            CompleteCurrentLevel(controller);
            controller.Tick(0.75f);
            Assert.That(controller.Phase, Is.EqualTo(GameplayPhase.Finished));
            Assert.That(controller.Results.Count, Is.EqualTo(2));
            controller.RequestNextSkin();
            controller.Tick(0f);
            Assert.That(controller.Results.Count, Is.EqualTo(2));
            controller.RequestRestart();
            controller.Tick(0f);
            Assert.That(controller.Results, Is.Empty);
            Assert.That(controller.LevelNumber, Is.EqualTo(1));
            Assert.That(source.Requests, Is.EqualTo(new[] { 0, 1, 0 }));
        }

        [Test]
        public void NonDefaultMoveDuration_ReachesTheComposedViewLayer()
        {
            var provider = new Provider();
            provider.Complete(CreateObject("Prefab"));
            var input = new Input();
            using var controller = CreateController(input, new Source(), provider, moveDuration: 0.4f);
            controller.Initialize();
            controller.Tick(0f);
            input.Move = MoveCommand.Right;
            controller.Tick(0.1f);
            Entity player = SimulationOf(controller).World.Single(e => e.Has<PlayerTag>());
            Assert.That(player.Get<ViewMove>().Duration, Is.EqualTo(0.4f));
            Assert.That(player.Get<TransformComponent>().Transform.position.x, Is.EqualTo(2.25f).Within(0.001f));
        }

        [Test]
        public void LevelState_DoesNotRequestOrBindAViewAndRestartCreatesFreshState()
        {
            var provider = new Provider();
            provider.Complete(CreateObject("Prefab"));
            var source = new Source();
            using var controller = CreateController(new Input(), source, provider, 5f);
            controller.Initialize();
            controller.Tick(0f);
            Simulation previous = SimulationOf(controller);
            Entity level = previous.World.Single(entity => entity.Has<LevelStateTag>());
            bool disposed = false;
            using var subscription = previous.World.SubscribeWorldDisposed(_ => disposed = true);
            int visualCount = source.Definition.Width * source.Definition.Height + source.Definition.Boxes.Count + 1;
            Assert.That(previous.World.Count(), Is.EqualTo(visualCount + 1));
            Assert.That(provider.Requests.Count, Is.EqualTo(visualCount));
            Assert.That(previous.World.Count(entity => entity.Has<TransformComponent>()), Is.EqualTo(visualCount));
            Assert.That(level.Has<GridPosition>() || level.Has<TransformComponent>() || level.Has<ViewPending>()
                || level.Has<ViewFailed>() || level.Has<ViewSkin>(), Is.False);
            controller.Tick(1f);
            controller.RequestRestart();
            controller.Tick(0f);
            Assert.That(disposed, Is.True);
            Simulation fresh = SimulationOf(controller);
            Entity freshLevel = fresh.World.Single(entity => entity.Has<LevelStateTag>());
            Assert.That(freshLevel.World, Is.SameAs(fresh.World));
            Assert.That(freshLevel.World, Is.Not.SameAs(previous.World));
            Assert.That(freshLevel.Get<LevelTimer>().RemainingSeconds, Is.EqualTo(5f));
            Assert.That(freshLevel.Get<MoveCount>().Value, Is.Zero);
            Assert.That(freshLevel.Has<LevelCompletedTag>() || freshLevel.Has<LevelTimedOutTag>(), Is.False);
            Assert.That(provider.Requests.Count, Is.EqualTo(visualCount * 2));
            Assert.That(fresh.World.Count(entity => entity.Has<TransformComponent>()), Is.EqualTo(visualCount));
        }

        [Test]
        public void UndoCommands_CoalesceAndSuppressForwardInputWithoutLoadingViews()
        {
            var provider = new Provider();
            provider.Complete(CreateObject("Prefab"));
            var input = new Input();
            using var controller = CreateController(input, new Source(), provider);
            controller.Initialize();
            controller.Tick(0f);
            input.Move = MoveCommand.Right;
            controller.Tick(0.16f);
            controller.Tick(0.16f);
            Simulation simulation = SimulationOf(controller);
            Entity[] entities = simulation.World.ToArray();
            int loads = provider.Requests.Count;
            Assert.That(controller.CanUndo, Is.True);
            controller.RequestUndo();
            controller.RequestUndo();
            input.UndoPressed = true;
            input.Move = MoveCommand.Up;
            controller.Tick(0.04f);
            input.UndoPressed = false;
            input.Move = default;
            Assert.That(controller.Moves, Is.EqualTo(1));
            Assert.That(controller.CanUndo, Is.False);
            Assert.That(simulation.World.Single(e => e.Has<PlayerTag>()).Get<GridPosition>(), Is.EqualTo(new GridPosition(3, 1)));
            controller.Tick(0.12f);
            controller.Tick(0f);
            Assert.That(controller.Moves, Is.EqualTo(1));
            Assert.That(controller.CanUndo, Is.True);
            Assert.That(SimulationOf(controller), Is.SameAs(simulation));
            Assert.That(simulation.World.ToArray(), Is.EqualTo(entities));
            Assert.That(provider.Requests.Count, Is.EqualTo(loads));
        }

        [Test]
        public void UndoDuringInterpolation_IsDiscardedAndAcceptedUndoClearsTheBufferedForwardMove()
        {
            var provider = new Provider();
            provider.Complete(CreateObject("Prefab"));
            var input = new Input();
            using var controller = CreateController(input, new Source(), provider);
            controller.Initialize();
            controller.Tick(0f);
            input.Move = MoveCommand.Right;
            controller.Tick(0.04f);
            Assert.That(controller.CanUndo, Is.False);
            controller.RequestUndo();
            input.UndoPressed = true;
            input.Move = MoveCommand.Up;
            controller.Tick(0.04f);
            input.UndoPressed = false;
            input.Move = default;
            controller.Tick(0.08f);
            Assert.That(controller.CanUndo, Is.True);
            Assert.That(controller.Moves, Is.EqualTo(1));
            controller.RequestUndo();
            controller.Tick(0.04f);
            Assert.That(controller.Moves, Is.Zero);
            controller.Tick(0.12f);
            controller.Tick(0.16f);
            Assert.That(SimulationOf(controller).World.Single(e => e.Has<PlayerTag>()).Get<GridPosition>(), Is.EqualTo(new GridPosition(2, 1)));
            Assert.That(controller.Moves, Is.Zero);
            Assert.That(controller.RemainingSeconds, Is.EqualTo(59.52f).Within(0.001f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RestartAndNextLevel_TakePriorityOverUndoAndReplaceHistory(bool next)
        {
            var provider = new Provider();
            provider.Complete(CreateObject("Prefab"));
            var source = new Source();
            var input = new Input();
            using var controller = CreateController(input, source, provider);
            controller.Initialize();
            controller.Tick(0f);
            input.Move = MoveCommand.Right;
            controller.Tick(0.16f);
            input.Move = default;
            Simulation previous = SimulationOf(controller);
            Assert.That(controller.CanUndo, Is.True);
            controller.RequestUndo();
            input.UndoPressed = true;
            input.NextLevelPressed = next;
            input.RestartPressed = !next;
            controller.Tick(0f);
            input.UndoPressed = input.NextLevelPressed = input.RestartPressed = false;
            Assert.That(SimulationOf(controller), Is.Not.SameAs(previous));
            Assert.That(controller.Moves, Is.Zero);
            Assert.That(controller.CanUndo, Is.False);
            controller.Tick(0f);
            Assert.That(controller.CanUndo, Is.False);
            Assert.That(controller.Phase, Is.EqualTo(GameplayPhase.Playing));
            Assert.That(source.Requests, Is.EqualTo(next ? new[] { 0, 1 } : new[] { 0 }));
        }

        [Test]
        public void UndoDuringPendingSkinBinding_IsDiscardedRatherThanReplayedAfterBinding()
        {
            var provider = new Provider();
            provider.Complete(CreateObject("Prefab"));
            var input = new Input();
            using var controller = CreateController(input, new Source(), provider);
            controller.Initialize();
            controller.Tick(0f);
            input.Move = MoveCommand.Right;
            controller.Tick(0.16f);
            input.Move = default;
            Assert.That(controller.CanUndo, Is.True);
            provider.Reset();
            input.NextSkinPressed = true;
            controller.Tick(0f);
            input.NextSkinPressed = false;
            Assert.That(controller.CanUndo, Is.False);
            controller.RequestUndo();
            input.UndoPressed = true;
            controller.Tick(0.25f);
            input.UndoPressed = false;
            provider.Complete(CreateObject("New skin"));
            controller.Tick(0f);
            Assert.That(controller.Moves, Is.EqualTo(1));
            Assert.That(controller.CanUndo, Is.True);
            controller.RequestUndo();
            controller.Tick(0.16f);
            Assert.That(controller.Moves, Is.Zero);
        }

        private static void CompleteCurrentLevel(GameplayController controller)
        {
            controller.Tick(0f);
            Simulation simulation = SimulationOf(controller);
            simulation.World.Single(e => e.Has<BoxTag>()).Set(new GridPosition(4, 2));
            controller.Tick(0f);
            Assert.That(controller.Phase, Is.EqualTo(GameplayPhase.Completed));
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

        private static string StateOf(GameplayController controller) => controller.Phase.ToString();

        private sealed class Input : IPlayerInput
        {
            public bool UndoPressed { get; set; }
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
            public LevelDefinition Definition { get; set; } = new(5, 3, new GridPosition(2, 1), Array.Empty<GridPosition>(),
                new[] { new GridPosition(0, 0) }, new[] { new GridPosition(4, 2) });
            public UniTask<LevelDefinition> LoadAsync(int index, CancellationToken ct)
            {
                Requests.Add(index);
                if (ReturnNull) return UniTask.FromResult<LevelDefinition>(null);
                return Fail
                    ? UniTask.FromException<LevelDefinition>(new InvalidOperationException("planned definition failure"))
                    : UniTask.FromResult(Definition);
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
