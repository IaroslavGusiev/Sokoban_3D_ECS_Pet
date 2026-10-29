using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Sokoban.Core;
using Sokoban.Core.Commands;
using Sokoban.Presentation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Sokoban.Tests.PlayMode
{
    public sealed class BackgroundLifecycleTests
    {
        private readonly List<Object> _objects = new();
        private readonly List<Fixture> _fixtures = new();
        private readonly List<GameplayController> _controllers = new();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameplayController controller in _controllers)
            {
                controller.Dispose();
            }
            foreach (Fixture fixture in _fixtures)
            {
                fixture.Owner.Dispose();
                foreach (ControlledBackgroundService.Request request in fixture.Service.Requests)
                {
                    request.Finish(null);
                }
            }
            foreach (Object target in _objects)
            {
                if (target != null)
                {
                    Object.Destroy(target);
                }
            }
            _controllers.Clear();
            _fixtures.Clear();
            _objects.Clear();
            yield return null;
        }

        [Test]
        public void Entry_SelectsOnceAndIdleUpdatesNeverReselect()
        {
            Fixture f = Create();
            f.Enter();
            string name = f.Owner.SelectedName;
            Assert.That(f.Service.Requests.Count, Is.EqualTo(1));
            for (int i = 0; i < 20; i++)
            {
                f.Owner.Update();
            }
            Assert.That(f.Owner.SelectedName, Is.EqualTo(name));
            Assert.That(f.Service.Requests.Count, Is.EqualTo(1));
            Assert.That(f.Owner.Status, Is.EqualTo(BackgroundStatus.Loading));
        }

        [UnityTest]
        public IEnumerator Display_DisposalRestoresCameraAndRemovesOwnedRenderingResources()
        {
            var target = GameObject("Owned background display");
            Camera camera = target.AddComponent<Camera>();
            camera.enabled = false;
            var data = target.AddComponent<HDAdditionalCameraData>();
            data.clearColorMode = HDAdditionalCameraData.ClearColorMode.Sky;
            Color original = new Color(0.13f, 0.21f, 0.34f);
            data.backgroundColorHDR = original;
            var volume = target.AddComponent<Volume>();
            var display = new BackgroundDisplay(camera, volume, Shader.Find("Hidden/Sokoban/BackgroundProjection"));
            CustomPassVolume projection = target.GetComponent<CustomPassVolume>();
            var pass = (FullScreenCustomPass)projection.customPasses.Single();
            Material material = pass.fullscreenPassMaterial;
            VolumeProfile profile = volume.profile;

            try
            {
                Assert.That(projection.targetCamera, Is.SameAs(camera));
                Assert.That(projection.injectionPoint, Is.EqualTo(CustomPassInjectionPoint.BeforePreRefraction));
                Assert.That(pass.enabled, Is.False, "Fallback must not sample an unbound cubemap.");
                Cubemap cube = Cube();
                display.Show(cube, new BackgroundPreset("Test", "unused"));
                Assert.That(pass.enabled, Is.True);
                Assert.That(material.GetTexture("_BackgroundCube"), Is.SameAs(cube));
            }
            finally
            {
                display.Dispose();
                display.Dispose();
            }

            Assert.That(pass.enabled, Is.False);
            Assert.That(projection.enabled, Is.False);
            Assert.That(material.GetTexture("_BackgroundCube"), Is.Null);
            Assert.That(data.clearColorMode, Is.EqualTo(HDAdditionalCameraData.ClearColorMode.Sky));
            Assert.That(data.backgroundColorHDR, Is.EqualTo(original));
            yield return null;
            Assert.That(projection == null, Is.True);
            Assert.That(material == null, Is.True);
            Assert.That(profile == null, Is.True);
            Assert.That(target.GetComponents<CustomPassVolume>(), Is.Empty);
        }

        [Test]
        public void Preview_CoalescesCommandsAndVisitsEveryPresetThenWraps()
        {
            Fixture f = Create();
            f.Enter();
            string first = f.Owner.SelectedName;
            var names = new HashSet<string>();
            for (int i = 0; i < f.Catalog.Presets.Count; i++)
            {
                names.Add(f.Owner.SelectedName);
                f.Service.Requests.Last().Finish(Cube());
                f.Owner.Update();
                int calls = f.Service.Requests.Count;
                f.Owner.RequestNext();
                f.Owner.RequestNext();
                f.Owner.Update();
                Assert.That(f.Service.Requests.Count, Is.EqualTo(calls + 1));
            }
            Assert.That(names.Count, Is.EqualTo(f.Catalog.Presets.Count));
            Assert.That(f.Owner.SelectedName, Is.EqualTo(first));
        }

        [Test]
        public void Replacement_KeepsPreviousVisibleAndReleasesItOnlyAfterApplying()
        {
            Fixture f = Create();
            f.Enter();
            ControlledBackgroundService.Request first = f.Service.Requests.Single();
            first.Finish(Cube());
            f.Owner.Update();
            Assert.That(f.Shown, Is.SameAs(first.Value));
            f.Owner.RequestNext();
            f.Owner.Update();
            Assert.That(f.Shown, Is.SameAs(first.Value));
            Assert.That(first.ReleaseCalls, Is.Zero);
            f.Service.OnRelease = request => Assert.That(f.Shown, Is.Not.SameAs(request.Value));
            ControlledBackgroundService.Request second = f.Service.Requests.Last();
            second.Finish(Cube());
            Assert.That(f.Shown, Is.SameAs(first.Value), "Completion must wait for the application update.");
            f.Owner.Update();
            Assert.That(f.Shown, Is.SameAs(second.Value));
            Assert.That(first.ReleaseCalls, Is.EqualTo(1));
            Assert.That(first.Operation.DestroyCalls, Is.EqualTo(1));
            f.Owner.Dispose();
            f.Owner.Dispose();
            Assert.That(second.ReleaseCalls, Is.EqualTo(1));
            Assert.That(second.Operation.DestroyCalls, Is.EqualTo(1));
            Assert.That(f.Shown, Is.Null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OverlappingSelections_IgnoreLateSuccessOrFailure(bool fail)
        {
            Fixture f = Create();
            f.Enter();
            ControlledBackgroundService.Request old = f.Service.Requests.Single();
            f.Owner.RequestNext();
            f.Owner.Update();
            ControlledBackgroundService.Request current = f.Service.Requests.Last();
            Assert.That(old.Token.IsCancellationRequested, Is.True);
            Assert.That(old.ReleaseCalls, Is.Zero, "Ignoring waiter must finish observation before releasing its handle.");
            current.Finish(Cube());
            f.Owner.Update();
            old.Finish(Cube(), fail);
            f.Owner.Update();
            Assert.That(f.Shown, Is.SameAs(current.Value));
            Assert.That(f.Owner.Status, Is.EqualTo(BackgroundStatus.Ready));
            Assert.That(old.ReleaseCalls, Is.EqualTo(1));
            Assert.That(old.Operation.DestroyCalls, Is.EqualTo(1));
            Assert.That(current.ReleaseCalls, Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void NewEntry_InvalidatesOldRequestBeforeTheNewLevelIsReady()
        {
            Fixture f = Create();
            f.Enter();
            ControlledBackgroundService.Request old = f.Service.Requests.Single();
            f.Owner.BeginLevelEntry();
            old.Finish(Cube());
            f.Owner.Update();
            Assert.That(f.Shown, Is.Null);
            Assert.That(f.Owner.CanPreview, Is.False);
            Assert.That(old.ReleaseCalls, Is.EqualTo(1));
            f.Owner.EnterLevel();
            Assert.That(f.Service.Requests.Count, Is.EqualTo(2));
            f.Service.Requests.Last().Finish(Cube());
            f.Owner.Update();
            Assert.That(f.Owner.Status, Is.EqualTo(BackgroundStatus.Ready));
        }

        [Test]
        public void Failure_KeepsActiveSkyAndDebugControlRetriesTheFailedSelection()
        {
            Fixture f = Create();
            f.Enter();
            ControlledBackgroundService.Request active = f.Service.Requests.Single();
            active.Finish(Cube());
            f.Owner.Update();
            f.Owner.RequestNext();
            f.Owner.Update();
            ControlledBackgroundService.Request failed = f.Service.Requests.Last();
            failed.Finish(null, true);
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: planned background failure");
            f.Owner.Update();
            Assert.That(f.Shown, Is.SameAs(active.Value));
            Assert.That(active.ReleaseCalls, Is.Zero);
            Assert.That(failed.ReleaseCalls, Is.EqualTo(1));
            f.Owner.RequestNext();
            f.Owner.Update();
            ControlledBackgroundService.Request retry = f.Service.Requests.Last();
            Assert.That(retry.Address, Is.EqualTo(failed.Address));
            retry.Finish(Cube());
            f.Owner.Update();
            Assert.That(f.Shown, Is.SameAs(retry.Value));
            Assert.That(active.ReleaseCalls, Is.EqualTo(1));
        }

        [Test]
        public void InitialFailure_LeavesNeutralFallbackAndAllowsRetry()
        {
            Fixture f = Create();
            f.Enter();
            f.Service.Requests.Single().Finish(null, true);
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: planned background failure");
            f.Owner.Update();
            Assert.That(f.Shown, Is.Null);
            Assert.That(f.CameraData.clearColorMode, Is.EqualTo(HDAdditionalCameraData.ClearColorMode.Color));
            Assert.That(f.Owner.Status, Is.EqualTo(BackgroundStatus.Failed));
            f.Owner.RequestNext();
            f.Owner.Update();
            f.Service.Requests.Last().Finish(Cube());
            f.Owner.Update();
            Assert.That(f.Owner.Status, Is.EqualTo(BackgroundStatus.Ready));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Disposal_ObservesIgnoringWaiterAndReleasesLateResultWithoutAnotherUpdate(bool fail)
        {
            Fixture f = Create();
            f.Enter();
            ControlledBackgroundService.Request pending = f.Service.Requests.Single();
            f.Owner.Dispose();
            Assert.That(pending.Token.IsCancellationRequested, Is.True);
            pending.Finish(Cube(), fail);
            Assert.That(pending.ReleaseCalls, Is.EqualTo(1));
            Assert.That(pending.Operation.DestroyCalls, Is.EqualTo(1));
            Assert.That(f.Shown, Is.Null);
            f.Owner.Dispose();
            f.Owner.Update();
            Assert.That(pending.ReleaseCalls, Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void CooperativeCancellation_ReleasesWaiterWithoutReleasingTheDisplayedSky()
        {
            Fixture f = Create();
            f.Service.IgnoreCancellation = false;
            f.Enter();
            ControlledBackgroundService.Request active = f.Service.Requests.Single();
            active.Finish(Cube());
            f.Owner.Update();
            f.Owner.RequestNext();
            f.Owner.Update();
            ControlledBackgroundService.Request pending = f.Service.Requests.Last();
            f.Owner.BeginLevelEntry();
            Assert.That(pending.Token.IsCancellationRequested, Is.True);
            Assert.That(pending.ReleaseCalls, Is.EqualTo(1));
            Assert.That(active.ReleaseCalls, Is.Zero);
            Assert.That(f.Shown, Is.SameAs(active.Value));
        }

        [Test]
        public void SharedService_DoesNotShareCoordinatorSelectionOrResourceOwnership()
        {
            var service = new ControlledBackgroundService();
            Fixture first = Create(service);
            Fixture second = Create(service);
            first.Enter();
            second.Enter();
            service.Requests[0].Finish(Cube());
            service.Requests[1].Finish(Cube());
            first.Owner.Update();
            second.Owner.Update();
            string selected = second.Owner.SelectedName;
            Cubemap shown = second.Shown;
            first.Owner.RequestNext();
            first.Owner.Update();
            first.Owner.Dispose();
            service.Requests.Last().Finish(Cube());
            Assert.That(second.Owner.SelectedName, Is.EqualTo(selected));
            Assert.That(second.Shown, Is.SameAs(shown));
            Assert.That(service.Requests[1].ReleaseCalls, Is.Zero);
            Assert.That(service.Requests[0].ReleaseCalls, Is.EqualTo(1));
        }

        [Test]
        public void SynchronousLoadFailure_IsObservableAndRetryable()
        {
            Fixture f = Create();
            f.Service.ThrowOnLoad = true;
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: planned background start failure");
            f.Enter();
            Assert.That(f.Owner.Status, Is.EqualTo(BackgroundStatus.Failed));
            f.Service.ThrowOnLoad = false;
            f.Owner.RequestNext();
            f.Owner.Update();
            f.Service.Requests.Single().Finish(Cube());
            f.Owner.Update();
            Assert.That(f.Owner.Status, Is.EqualTo(BackgroundStatus.Ready));
        }

        [Test]
        public void Controller_PreservesPreviewThroughRestartTimeoutUndoAndSkinChanges()
        {
            Fixture f = Create();
            var input = new Input();
            GameplayController controller = Controller(f, input, LevelParser.Parse("#######\n#@-$-.#\n#######"));
            controller.Initialize();
            TestCharacterSelection.Tick(controller, 0f);
            f.Service.Requests.Last().Finish(Cube());
            TestCharacterSelection.Tick(controller, 0f);
            controller.RequestNextBackground();
            TestCharacterSelection.Tick(controller, 0f);
            f.Service.Requests.Last().Finish(Cube());
            TestCharacterSelection.Tick(controller, 0f);
            string chosen = controller.BackgroundName;
            Cubemap shown = f.Shown;
            int calls = f.Service.Requests.Count;

            input.Move = MoveCommand.Right;
            TestCharacterSelection.Tick(controller, 0.16f);
            controller.RequestUndo();
            TestCharacterSelection.Tick(controller, 0.16f);
            controller.RequestNextSkin();
            TestCharacterSelection.Tick(controller, 0f);
            controller.RequestRestart();
            TestCharacterSelection.Tick(controller, 0f);
            TestCharacterSelection.Tick(controller, 0f);
            TestCharacterSelection.Tick(controller, 3f);
            TestCharacterSelection.Tick(controller, 0f);

            Assert.That(controller.BackgroundName, Is.EqualTo(chosen));
            Assert.That(f.Shown, Is.SameAs(shown));
            Assert.That(f.Service.Requests.Count, Is.EqualTo(calls));
            Assert.That(controller.Phase, Is.EqualTo(GameplayPhase.Playing));
        }

        [Test]
        public void Controller_PendingSelectionSurvivesManualAndTimeoutRunReplacement()
        {
            Fixture f = Create();
            var input = new Input();
            GameplayController controller = Controller(f, input, LevelParser.Parse("#######\n#@-$-.#\n#######"));
            controller.Initialize();
            TestCharacterSelection.Tick(controller, 0f);
            string chosen = controller.BackgroundName;
            ControlledBackgroundService.Request pending = f.Service.Requests.Single();
            controller.RequestRestart();
            TestCharacterSelection.Tick(controller, 0f);
            TestCharacterSelection.Tick(controller, 0f);
            TestCharacterSelection.Tick(controller, 3f);
            TestCharacterSelection.Tick(controller, 0f);
            Assert.That(pending.Token.IsCancellationRequested, Is.False);
            Assert.That(controller.BackgroundName, Is.EqualTo(chosen));
            Assert.That(f.Service.Requests.Count, Is.EqualTo(1));
            pending.Finish(Cube());
            TestCharacterSelection.Tick(controller, 0f);
            Assert.That(f.Shown, Is.SameAs(pending.Value));
            Assert.That(controller.BackgroundStatus, Is.EqualTo(BackgroundStatus.Ready));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Controller_NewLevelEntrySelectsAgainForDevelopmentCommandAndProgression(bool progression)
        {
            Fixture f = Create();
            var input = new Input();
            GameplayController controller = Controller(f, input, LevelParser.Parse("#####\n#@$.#\n#####"));
            var expected = new System.Random(21);
            int first = expected.Next(f.Catalog.Presets.Count);
            int next = expected.Next(f.Catalog.Presets.Count);
            controller.Initialize();
            TestCharacterSelection.Tick(controller, 0f);
            Assert.That(controller.BackgroundName, Is.EqualTo(f.Catalog.Presets[first].Name));
            ControlledBackgroundService.Request old = f.Service.Requests.Single();
            if (progression)
            {
                input.Move = MoveCommand.Right;
                TestCharacterSelection.Tick(controller, 0.16f);
                TestCharacterSelection.Tick(controller, 0.1f);
            }
            else
            {
                input.NextLevelPressed = true;
                TestCharacterSelection.Tick(controller, 0f);
                input.NextLevelPressed = false;
            }
            Assert.That(controller.LevelNumber, Is.EqualTo(2));
            Assert.That(controller.BackgroundName, Is.EqualTo(f.Catalog.Presets[next].Name));
            Assert.That(old.Token.IsCancellationRequested, Is.True);
            old.Finish(Cube());
            TestCharacterSelection.Tick(controller, 0f);
            Assert.That(old.ReleaseCalls, Is.EqualTo(1));
            Assert.That(f.Shown, Is.Null);
            f.Service.Requests.Last().Finish(Cube());
            TestCharacterSelection.Tick(controller, 0f);
            Assert.That(f.Shown, Is.Not.Null);
        }

        [Test]
        public void Controller_BackgroundFailureDoesNotChangeBoardReadinessOrStopMovement()
        {
            Fixture f = Create();
            var input = new Input();
            GameplayController controller = Controller(f, input, LevelParser.Parse("#######\n#@-$-.#\n#######"));
            controller.Initialize();
            TestCharacterSelection.Tick(controller, 0f);
            f.Service.Requests.Single().Finish(null, true);
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: planned background failure");
            TestCharacterSelection.Tick(controller, 0f);
            Assert.That(controller.Phase, Is.EqualTo(GameplayPhase.Playing));
            input.Move = MoveCommand.Right;
            TestCharacterSelection.Tick(controller, 0.16f);
            Assert.That(controller.Moves, Is.EqualTo(1));
            Assert.That(controller.BackgroundStatus, Is.EqualTo(BackgroundStatus.Failed));
            Assert.That(controller.CanNextBackground, Is.True);
        }

        private Fixture Create(ControlledBackgroundService service = null)
        {
            var catalog = ScriptableObject.CreateInstance<BackgroundCatalog>();
            _objects.Add(catalog);
            for (int i = 0; i < 3; i++)
            {
                catalog.Presets.Add(new BackgroundPreset("Preset " + i, "backgrounds/test-" + i));
            }
            var target = GameObject("Background fixture");
            Camera camera = target.AddComponent<Camera>();
            camera.enabled = false;
            var cameraData = target.AddComponent<HDAdditionalCameraData>();
            var volume = target.AddComponent<Volume>();
            var display = new BackgroundDisplay(camera, volume, Shader.Find("Hidden/Sokoban/BackgroundProjection"));
            var pass = (FullScreenCustomPass)target.GetComponent<CustomPassVolume>().customPasses.Single();
            var data = new TestStaticData { Backgrounds = catalog };
            service ??= new ControlledBackgroundService();
            var owner = new BackgroundCoordinator(data, service, display, new System.Random(21));
            var fixture = new Fixture(owner, service, catalog, cameraData, pass.fullscreenPassMaterial, data);
            _fixtures.Add(fixture);
            return fixture;
        }

        private GameplayController Controller(Fixture f, Input input, LevelDefinition level)
        {
            GameplaySettings settings = TestGameplaySettings.Create(2f, 0f);
            _objects.Add(settings);
            f.Data.Gameplay = settings;
            TestCharacterSelection.Configure(f.Data, _objects);
            var provider = new Views(GameObject("View prefab"));
            var factory = new LevelRunFactory(new LevelRoot(GameObject("Run root").transform), provider,
                new LevelPopulator(), new SimulationPipelineFactory(), new ViewPipelineFactory());
            var controller = new GameplayController(input, new Source(level), new HintCoordinator(new HintService(new HintSolver()), provider), f.Owner, factory, f.Data);
            _controllers.Add(controller);
            return controller;
        }

        private GameObject GameObject(string name)
        {
            var target = new GameObject(name);
            _objects.Add(target);
            return target;
        }

        private Cubemap Cube()
        {
            var cube = new Cubemap(4, TextureFormat.RGBA32, false);
            _objects.Add(cube);
            return cube;
        }

        private sealed class Fixture
        {
            public readonly BackgroundCoordinator Owner;
            public readonly ControlledBackgroundService Service;
            public readonly BackgroundCatalog Catalog;
            public readonly HDAdditionalCameraData CameraData;
            public readonly Material Material;
            public readonly TestStaticData Data;
            public Cubemap Shown => Material == null ? null : Material.GetTexture("_BackgroundCube") as Cubemap;

            public Fixture(BackgroundCoordinator owner, ControlledBackgroundService service, BackgroundCatalog catalog, HDAdditionalCameraData cameraData, Material material, TestStaticData data)
            {
                Owner = owner;
                Service = service;
                Catalog = catalog;
                CameraData = cameraData;
                Material = material;
                Data = data;
            }

            public void Enter()
            {
                Owner.BeginLevelEntry();
                Owner.EnterLevel();
            }
        }

        private sealed class Input : IPlayerInput
        {
            public MoveCommand Move;
            public bool NextLevelPressed { get; set; }
            public bool RestartPressed => false;
            public bool IsNeutral => true;
            public bool UndoPressed => false;
            public MoveCommand ReadMove()
            {
                MoveCommand move = Move;
                Move = default;
                return move;
            }
        }

        private sealed class Views : IViewProvider
        {
            private readonly GameObject _prefab;
            public Views(GameObject prefab) => _prefab = prefab;
            public UniTask<GameObject> LoadAsync(ViewKey key, CancellationToken ct) => UniTask.FromResult(_prefab);
        }

        private sealed class Source : ILevelSource
        {
            private readonly LevelDefinition _level;
            public int Count => 2;
            public Source(LevelDefinition level) => _level = level;
            public UniTask<LevelDefinition> LoadAsync(int index, CancellationToken ct) => UniTask.FromResult(_level);
        }
    }
}
