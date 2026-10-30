// These scene integration tests run in the Editor's Play Mode without adding the scene to build settings.
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using DefaultEcs;
using Sokoban.Core.Components;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Sokoban.Core.Commands;
using Sokoban.Presentation;
using Sokoban.Core;
using Sokoban.Presentation.Ecs.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Zenject;

namespace Sokoban.Tests.PlayMode
{
    public sealed class HardcodedLevelTests
    {
        private const string ScenePath = "Assets/_Sokoban/Scenes/GameplayScene.unity";
        private Scene _scene;
        private GameplayController _controller;
        private Transform _levelRoot;
        private ViewCatalog _catalog;
        private Simulation CurrentSimulation
        {
            get
            {
                object run = typeof(GameplayController).GetField("_run", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_controller);
                return (Simulation)run?.GetType().GetField("_simulation", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(run);
            }
        }
        private Keyboard _keyboard;
        private InputSettings.EditorInputBehaviorInPlayMode _editorInput;
        private InputSettings.BackgroundBehavior _backgroundInput;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            _backgroundInput = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            _keyboard = InputSystem.AddDevice<Keyboard>();

            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            _scene = SceneManager.GetSceneByPath(ScenePath);
            yield return null;
            SceneContext context = _scene.GetRootGameObjects().Select(root => root.GetComponent<SceneContext>())
                .Single(component => component != null);
            Assert.That(context.Container.Resolve<SimulationPipelineFactory>(),
                Is.SameAs(context.Container.Resolve<SimulationPipelineFactory>()));
            Assert.That(context.Container.Resolve<ViewPipelineFactory>(),
                Is.SameAs(context.Container.Resolve<ViewPipelineFactory>()));
            ITickable tickable = context.Container.ResolveAll<ITickable>().Single();
            Assert.That(tickable, Is.TypeOf<GameplayController>());
            _controller = context.Container.Resolve<GameplayController>();
            Assert.That(context.Container.Resolve<IGameplayStatus>(), Is.SameAs(_controller));
            Assert.That(context.Container.Resolve<IGameplayCommands>(), Is.SameAs(_controller));
            Assert.That(tickable, Is.SameAs(_controller));
            var disposables = context.Container.ResolveAll<IDisposable>();
            Assert.That(disposables.OfType<GameplayLifetime>().Single(),
                Is.SameAs(context.Container.Resolve<GameplayLifetime>()));
            Assert.That(disposables.OfType<GameplayController>(), Is.Empty);
            Assert.That(disposables.OfType<ViewAssets>(), Is.Empty);
            Assert.That(disposables.OfType<StaticDataProvider>(), Is.Empty);
            Assert.That(disposables.OfType<HintCoordinator>(), Is.Empty);
            Assert.That(disposables.OfType<BackgroundCoordinator>(), Is.Empty);
            Assert.That(disposables.OfType<BackgroundDisplay>(), Is.Empty);
            Assert.That(context.Container.Resolve<BackgroundCoordinator>(),
                Is.SameAs(context.Container.Resolve<BackgroundCoordinator>()));
            Assert.That(context.Container.Resolve<BackgroundDisplay>(),
                Is.SameAs(context.Container.Resolve<BackgroundDisplay>()));
            Assert.That(context.Container.HasBinding<Camera>(), Is.False);
            Assert.That(context.Container.HasBinding<Volume>(), Is.False);
            Assert.That(context.Container.HasBinding<Shader>(), Is.False);
            Assert.That(context.Container.HasBinding<HintArrow>(), Is.False);
            _levelRoot = context.Container.Resolve<LevelRoot>().Transform;
            float deadline = Time.realtimeSinceStartup + 10f;
            while (_controller.Phase != GameplayPhase.SelectingCharacter && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(_controller.Phase, Is.EqualTo(GameplayPhase.SelectingCharacter));
            Assert.That(CurrentSimulation.World.Single(e => e.Has<PlayerTag>()).Has<TransformComponent>(), Is.False);
            Assert.That(Board.childCount, Is.EqualTo(75));
            _controller.RequestConfirmCharacter();
            while (_controller.Phase != GameplayPhase.Playing && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(CurrentSimulation, Is.Not.Null, "Initial Addressable content did not load within 10 seconds.");
            _catalog = context.Container.Resolve<IStaticDataProvider>().GetViewCatalog();
            while (CurrentSimulation.World.Any(entity => entity.Has<GridPosition>() && !entity.Has<TransformComponent>()) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Board.childCount, Is.EqualTo(76));
            Assert.That(CurrentSimulation.World.Count(), Is.EqualTo(77));
            Entity levelState = CurrentSimulation.World.Single(entity => entity.Has<LevelStateTag>());
            Assert.That(levelState.Has<TransformComponent>() || levelState.Has<ViewPending>() || levelState.Has<ViewSkin>(), Is.False);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_scene.IsValid() && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
            if (_keyboard != null && _keyboard.added) InputSystem.RemoveDevice(_keyboard);
            InputSystem.settings.editorInputBehaviorInPlayMode = _editorInput;
            InputSystem.settings.backgroundBehavior = _backgroundInput;
        }

        [UnityTest]
        public IEnumerator GameplayInstaller_ComposesSavedReferencesAndKeepsOneDisplayAcrossRestart()
        {
            SceneContext context = _scene.GetRootGameObjects().Select(root => root.GetComponent<SceneContext>())
                .Single(component => component != null);
            var sceneBindings = new SerializedObject(context.GetComponent<GameplayInstaller>());
            var services = new SerializedObject(context.GetComponent<ServicesInstaller>());
            foreach (string field in new[] { "levelRoot", "gameplayCamera", "backgroundVolume", "backgroundShader" })
            {
                Assert.That(sceneBindings.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
                Assert.That(services.FindProperty(field), Is.Null, "Obsolete service scene reference: " + field);
            }
            Assert.That(sceneBindings.FindProperty("levelRoot").objectReferenceValue, Is.SameAs(_levelRoot));
            var camera = (Camera)sceneBindings.FindProperty("gameplayCamera").objectReferenceValue;
            var volume = (Volume)sceneBindings.FindProperty("backgroundVolume").objectReferenceValue;
            var shader = (Shader)sceneBindings.FindProperty("backgroundShader").objectReferenceValue;
            CustomPassVolume projection = volume.GetComponents<CustomPassVolume>().Single();
            Assert.That(projection.targetCamera, Is.SameAs(camera));
            var pass = (FullScreenCustomPass)projection.customPasses.Single();
            Assert.That(pass.fullscreenPassMaterial.shader, Is.SameAs(shader));
            float deadline = Time.realtimeSinceStartup + 10f;
            while (_controller.BackgroundStatus != BackgroundStatus.Ready && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.That(_controller.BackgroundStatus, Is.EqualTo(BackgroundStatus.Ready));
            string initial = _controller.BackgroundName;
            _controller.RequestNextBackground();
            while ((_controller.BackgroundName == initial || _controller.BackgroundStatus != BackgroundStatus.Ready)
                   && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.That(_controller.BackgroundName, Is.Not.EqualTo(initial));
            Assert.That(_controller.BackgroundStatus, Is.EqualTo(BackgroundStatus.Ready));
            string selected = _controller.BackgroundName;
            Texture texture = pass.fullscreenPassMaterial.GetTexture("_BackgroundCube");
            Assert.That(texture, Is.Not.Null);
            Transform oldBoard = Board;
            _controller.RequestRestart();
            while ((Board == oldBoard || _controller.Phase != GameplayPhase.Playing) && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.That(Board, Is.Not.SameAs(oldBoard));
            Assert.That(_controller.Phase, Is.EqualTo(GameplayPhase.Playing));
            Assert.That(_controller.BackgroundName, Is.EqualTo(selected));
            Assert.That(pass.fullscreenPassMaterial.GetTexture("_BackgroundCube"), Is.SameAs(texture));
            Assert.That(volume.GetComponents<CustomPassVolume>().Single(), Is.SameAs(projection));
        }

        [UnityTest]
        public IEnumerator KeyboardSolution_SynchronizesActorsAndPreservesGoalFloors()
        {
            UIDocument document = _scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<UIDocument>()).Single();
            document.rootVisualElement.Q<Button>("restart").Focus();
            Transform firstGoal = View("Goal (2, 4)");
            Transform secondGoal = View("Goal (4, 4)");
            Material goalMaterial = firstGoal.GetComponentInChildren<Renderer>().sharedMaterial;
            int initialFrame = Time.frameCount;

            yield return Press(Key.LeftArrow);
            AssertActors(new Vector3(2, 0, 2), new Vector3(2, 0, 3), new Vector3(4, 0, 3));
            yield return Press(Key.W);
            AssertActors(new Vector3(2, 0, 3), new Vector3(2, 0, 4), new Vector3(4, 0, 3));
            yield return Press(Key.S);
            yield return Press(Key.D);
            yield return Press(Key.RightArrow);
            yield return Press(Key.UpArrow);
            AssertActors(new Vector3(4, 0, 3), new Vector3(2, 0, 4), new Vector3(4, 0, 4));
            Assert.That(Time.frameCount, Is.GreaterThan(initialFrame));
            Assert.That(firstGoal.position, Is.EqualTo(new Vector3(2, 0, 4)));
            Assert.That(secondGoal.position, Is.EqualTo(new Vector3(4, 0, 4)));
            Assert.That(firstGoal.GetComponentInChildren<Renderer>().sharedMaterial, Is.SameAs(goalMaterial));
            Assert.That(secondGoal.GetComponentInChildren<Renderer>().sharedMaterial, Is.SameAs(goalMaterial));

            yield return Move(MoveCommand.Up);
            Assert.That(View("Box (4, 3)").position, Is.EqualTo(new Vector3(4, 0, 4)));
            Assert.That(secondGoal.position, Is.EqualTo(new Vector3(4, 0, 4)));
            Assert.That(secondGoal.GetComponentInChildren<Renderer>().sharedMaterial, Is.SameAs(goalMaterial));
        }

        [UnityTest]
        public IEnumerator RejectedMovement_LeavesAllActorViewsUnchanged()
        {
            yield return Move(MoveCommand.Left);
            yield return Move(MoveCommand.Left);
            yield return Move(MoveCommand.Left);
            AssertActors(new Vector3(1, 0, 2), new Vector3(2, 0, 3), new Vector3(4, 0, 3));
            yield return null;
            AssertActors(new Vector3(1, 0, 2), new Vector3(2, 0, 3), new Vector3(4, 0, 3));
        }

        [UnityTest]
        public IEnumerator RejectedPush_LeavesAllActorViewsUnchanged()
        {
            yield return Move(MoveCommand.Left);
            yield return Move(MoveCommand.Up);
            yield return Move(MoveCommand.Up);
            yield return Move(MoveCommand.Up);
            yield return null;
            AssertActors(new Vector3(2, 0, 4), new Vector3(2, 0, 5), new Vector3(4, 0, 3));
        }

        [UnityTest]
        public IEnumerator RestartKeyAndRepeatedRestart_ReplaceViewsAndRestoreInitialState()
        {
            Transform previousBoard = Board;
            yield return Move(MoveCommand.Left);
            CurrentSimulation.Send(MoveCommand.Up);
            yield return Press(Key.R, Key.UpArrow);
            Assert.That(previousBoard == null, Is.True);
            Assert.That(_levelRoot.childCount, Is.EqualTo(1));
            AssertActors(new Vector3(3, 0, 2), new Vector3(2, 0, 3), new Vector3(4, 0, 3));

            for (int i = 0; i < 3; i++)
            {
                previousBoard = Board;
                SetRestartInput();
                Assert.That(previousBoard.gameObject.activeSelf, Is.True);
                TickController();
                Assert.That(previousBoard.gameObject.activeSelf, Is.False);
            }
            yield return null;
            Assert.That(_levelRoot.childCount, Is.EqualTo(1));
            yield return Move(MoveCommand.Left);
            AssertActors(new Vector3(2, 0, 2), new Vector3(2, 0, 3), new Vector3(4, 0, 3));
        }

        [UnityTest]
        public IEnumerator DisposingGameplayLifetime_ReleasesCurrentWorldAndViews()
        {
            Transform previousBoard = Board;
            yield return Move(MoveCommand.Left);
            SceneContext context = _scene.GetRootGameObjects().Select(root => root.GetComponent<SceneContext>())
                .Single(component => component != null);
            context.Container.Resolve<TickableManager>().Remove(_controller);
            CustomPassVolume projection = _scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CustomPassVolume>()).Single();
            var pass = (FullScreenCustomPass)projection.customPasses.Single();
            Material material = pass.fullscreenPassMaterial;
            VolumeProfile profile = projection.GetComponent<Volume>().profile;
            GameplayLifetime lifetime = context.Container.Resolve<GameplayLifetime>();
            context.Container.Resolve<DisposableManager>().Remove(lifetime);
            foreach (GameplayDebugOverlay overlay in _scene.GetRootGameObjects()
                         .SelectMany(root => root.GetComponentsInChildren<GameplayDebugOverlay>(true)))
                overlay.enabled = false;
            lifetime.Dispose();
            Assert.That(projection.enabled, Is.False);
            Assert.That(pass.enabled, Is.False);
            Assert.That(pass.fullscreenPassMaterial, Is.Null);
            Assert.That(material.GetTexture("_BackgroundCube"), Is.Null);
            Assert.That(previousBoard.gameObject.activeSelf, Is.False);
            Assert.That(CurrentSimulation, Is.Null);
            yield return null;
            Assert.That(previousBoard == null, Is.True);
            Assert.That(_levelRoot.childCount, Is.Zero);
            Assert.That(projection == null, Is.True);
            Assert.That(material == null, Is.True);
            Assert.That(profile == null, Is.True);

        }

        [UnityTest]
        public IEnumerator QueuedMove_CommitsGridBeforeViewsFinishInterpolating()
        {
            CurrentSimulation.Send(MoveCommand.Left);
            AssertActors(new Vector3(3, 0, 2), new Vector3(2, 0, 3), new Vector3(4, 0, 3));
            TickController();
            Assert.That(CurrentSimulation.World.Single(entity => entity.Has<PlayerTag>()).Get<GridPosition>(), Is.EqualTo(new GridPosition(2, 2)));
            yield return WaitForMotion();
            AssertActors(new Vector3(2, 0, 2), new Vector3(2, 0, 3), new Vector3(4, 0, 3));

            CurrentSimulation.Send(MoveCommand.Up);
            TickController();
            yield return WaitForMotion();
            AssertActors(new Vector3(2, 0, 3), new Vector3(2, 0, 4), new Vector3(4, 0, 3));
            TickController();
            AssertActors(new Vector3(2, 0, 3), new Vector3(2, 0, 4), new Vector3(4, 0, 3));
            yield return null;
        }

        [UnityTest]
        public IEnumerator UndoShortcut_ReversesPushInterpolationAndStopsFeedbackWithoutReplacingViews()
        {
            SceneContext context = _scene.GetRootGameObjects().Select(root => root.GetComponent<SceneContext>())
                .Single(component => component != null);
            context.Container.Resolve<TickableManager>().Remove(_controller);
            Simulation simulation = CurrentSimulation;
            Entity[] entities = simulation.World.ToArray();
            Transform board = Board;
            CurrentSimulation.Send(MoveCommand.Left);
            _controller.Tick(0.16f);
            Entity box = CurrentSimulation.World.Single(e => e.Has<BoxTag>() && e.Get<GridPosition>() == new GridPosition(2, 3));
            BoxGoalAnimator animator = box.Get<BoxGoalFeedbackComponent>().Animator;
            Transform visual = animator.transform.Find("Visual");
            Vector3 baseline = visual.localPosition;
            Vector3 scale = visual.localScale;
            CurrentSimulation.Send(MoveCommand.Up);
            _controller.Tick(0.16f);
            _controller.Tick(0.055f);
            Assert.That(animator.IsPlaying, Is.True);
            Assert.That(_controller.CanUndo, Is.True);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Z));
            InputSystem.Update();
            _controller.Tick(0.04f);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            InputSystem.Update();
            Assert.That(_controller.Moves, Is.EqualTo(1));
            Assert.That(box.Get<GridPosition>(), Is.EqualTo(new GridPosition(2, 3)));
            Assert.That(box.Get<TransformComponent>().Transform.position.z, Is.EqualTo(3.75f).Within(0.001f));
            Assert.That(animator.IsPlaying, Is.False);
            Assert.That(PrimeTween.Tween.GetTweensCount(visual), Is.Zero);
            Assert.That(visual.localPosition, Is.EqualTo(baseline));
            Assert.That(visual.localScale, Is.EqualTo(scale));
            Assert.That(_controller.CanUndo, Is.False);
            _controller.Tick(0.12f);
            Assert.That(_controller.CanUndo, Is.True);
            Assert.That(box.Get<TransformComponent>().Transform.position, Is.EqualTo(new Vector3(2, 0, 3)));
            Assert.That(CurrentSimulation, Is.SameAs(simulation));
            Assert.That(Board, Is.SameAs(board));
            Assert.That(simulation.World.ToArray(), Is.EqualTo(entities));
            CurrentSimulation.Send(MoveCommand.Up);
            _controller.Tick(0.16f);
            Assert.That(animator.IsPlaying, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HintCommand_ShowsAnArrowWithoutMovingAndRestartDisposesIt()
        {
            SceneContext context = _scene.GetRootGameObjects().Select(root => root.GetComponent<SceneContext>())
                .Single(component => component != null);
            context.Container.Resolve<TickableManager>().Remove(_controller);
            Simulation simulation = CurrentSimulation;
            GameplaySnapshot before = simulation.CaptureSnapshot();
            _controller.RequestHint();
            _controller.Tick(0f);
            float deadline = Time.realtimeSinceStartup + 10f;
            while (_controller.HintStatus == HintStatus.Calculating && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                _controller.Tick(0f);
            }
            Assert.That(_controller.HintStatus, Is.EqualTo(HintStatus.Available));
            Assert.That(_controller.HintMove, Is.EqualTo(MoveCommand.Left));
            Assert.That(simulation.CaptureSnapshot(), Is.EqualTo(before));
            HintArrow arrow = Board.GetComponentInChildren<HintArrow>();
            Assert.That(arrow, Is.Not.Null);
            Assert.That(arrow.transform.position, Is.EqualTo(new Vector3(2.45f, 1.8f, 2f)));
            Assert.That(Vector3.Distance(arrow.transform.forward, Vector3.left), Is.LessThan(0.00001f));
            Assert.That(arrow.GetComponentsInChildren<Collider>(), Is.Empty);
            Assert.That(Board.childCount, Is.EqualTo(77));
            Assert.That(simulation.World.Count(), Is.EqualTo(77));
            simulation.Send(MoveCommand.Left);
            _controller.Tick(0.16f);
            Assert.That(arrow.gameObject.activeSelf, Is.False);
            Assert.That(_controller.HintStatus, Is.EqualTo(HintStatus.None));
            _controller.RequestHint();
            _controller.Tick(0f);
            deadline = Time.realtimeSinceStartup + 10f;
            while (_controller.HintStatus == HintStatus.Calculating && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                _controller.Tick(0f);
            }
            Assert.That(_controller.HintStatus, Is.EqualTo(HintStatus.Available));
            Assert.That(_controller.HintMove, Is.EqualTo(MoveCommand.Up));
            Assert.That(Board.GetComponentInChildren<HintArrow>(), Is.SameAs(arrow));
            Assert.That(arrow.transform.position, Is.EqualTo(new Vector3(2f, 1.8f, 2.55f)));
            Assert.That(simulation.PlayerPosition, Is.EqualTo(new GridPosition(2, 2)));
            Assert.That(_controller.Moves, Is.EqualTo(1));
            _controller.RequestUndo();
            _controller.Tick(0.16f);
            Assert.That(arrow.gameObject.activeSelf, Is.False);
            Assert.That(_controller.HintStatus, Is.EqualTo(HintStatus.None));
            Assert.That(_controller.Moves, Is.Zero);
            Assert.That(simulation.BoardRevision, Is.EqualTo(2));
            _controller.RequestHint();
            _controller.Tick(0f);
            deadline = Time.realtimeSinceStartup + 10f;
            while (_controller.HintStatus == HintStatus.Calculating && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                _controller.Tick(0f);
            }
            Assert.That(_controller.HintStatus, Is.EqualTo(HintStatus.Available));
            Assert.That(_controller.HintMove, Is.EqualTo(MoveCommand.Left));
            Assert.That(Board.GetComponentInChildren<HintArrow>(), Is.SameAs(arrow));
            Assert.That(arrow.transform.position, Is.EqualTo(new Vector3(2.45f, 1.8f, 2f)));
            Assert.That(simulation.PlayerPosition, Is.EqualTo(new GridPosition(3, 2)));
            Assert.That(simulation.CaptureSnapshot().PlayerPosition, Is.EqualTo(before.PlayerPosition));
            _controller.RequestRestart();
            _controller.Tick(0f);
            Assert.That(Board.childCount, Is.EqualTo(76));
            yield return null;
            Assert.That(arrow == null, Is.True);
            Assert.That(CurrentSimulation, Is.Not.SameAs(simulation));
        }

        [UnityTest]
        public IEnumerator HeldKey_DoesNotRepeatAndSimultaneousDirectionsKeepPriority()
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.UpArrow, Key.LeftArrow));
            for (int i = 0; i < 8; i++) yield return null;
            yield return WaitForMotion();
            AssertActors(new Vector3(3, 0, 3), new Vector3(2, 0, 3), new Vector3(4, 0, 3));
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null;
            yield return null;
            AssertActors(new Vector3(3, 0, 3), new Vector3(2, 0, 3), new Vector3(4, 0, 3));
        }

        [UnityTest]
        public IEnumerator GoalArrival_AnimatesTheBoxVisualAndRestartClearsFeedback()
        {
            SceneContext context = _scene.GetRootGameObjects().Select(root => root.GetComponent<SceneContext>())
                .Single(component => component != null);
            context.Container.Resolve<TickableManager>().Remove(_controller);
            CurrentSimulation.Send(MoveCommand.Left);
            _controller.Tick(0.16f);
            Entity box = CurrentSimulation.World.Single(e => e.Has<BoxTag>() && e.Get<GridPosition>() == new GridPosition(2, 3));
            Assert.That(box.Has<BoxGoalFeedbackComponent>(), Is.True);
            BoxGoalAnimator animator = box.Get<BoxGoalFeedbackComponent>().Animator;
            Transform visual = animator.transform.Find("Visual");
            Vector3 baseline = visual.localPosition;
            Vector3 scale = visual.localScale;
            Transform oldBoard = Board;
            CurrentSimulation.Send(MoveCommand.Up);
            _controller.Tick(0f);
            Assert.That(box.Get<GridPosition>(), Is.EqualTo(new GridPosition(2, 4)));
            Assert.That(animator.IsPlaying, Is.False);
            Assert.That(visual.localPosition, Is.EqualTo(baseline));
            _controller.Tick(0.16f);
            Assert.That(animator.IsPlaying, Is.True);
            _controller.Tick(0.055f);
            Assert.That(visual.localPosition.y, Is.GreaterThan(baseline.y));
            Assert.That(visual.localScale, Is.Not.EqualTo(scale));
            Assert.That(box.Get<TransformComponent>().Transform.position, Is.EqualTo(new Vector3(2, 0, 4)));
            _controller.RequestRestart();
            _controller.Tick(0f);
            Assert.That(oldBoard.gameObject.activeSelf, Is.False);
            Assert.That(visual.localPosition, Is.EqualTo(baseline));
            Assert.That(visual.localScale, Is.EqualTo(scale));
            Assert.That(animator.IsPlaying, Is.False);
            Assert.That(PrimeTween.Tween.GetTweensCount(visual), Is.Zero);
            Assert.That(CurrentSimulation.World.Where(e => e.Has<BoxTag>()).All(e => !e.Get<BoxGoalFeedbackComponent>().Animator.IsPlaying), Is.True);
            yield return null;
            Assert.That(oldBoard == null, Is.True);
        }

        [UnityTest]
        public IEnumerator InternalSkinReplacementAndOverlayRestartUseTheSceneBinding()
        {
            yield return null;
            UIDocument document = _scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<UIDocument>()).Single();
            Simulation original = CurrentSimulation;
            Entity player = original.World.Single(e => e.Has<PlayerTag>());
            _controller.RequestNextSkin();
            _controller.RequestNextSkin();
            float deadline = Time.realtimeSinceStartup + 5f;
            while ((!player.Has<PlayerAnimatorComponent>() || player.Get<ViewSkin>().Id != SkinId.Alternate)
                   && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(player.Get<ViewSkin>().Id, Is.EqualTo(SkinId.Alternate));
            Assert.That(player.Has<PlayerAnimatorComponent>(), Is.True);
            Assert.That(CurrentSimulation, Is.SameAs(original));
            Activate(document.rootVisualElement.Q<Button>("restart"));
            Activate(document.rootVisualElement.Q<Button>("restart"));
            while (CurrentSimulation == original && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(CurrentSimulation, Is.Not.SameAs(original));
            Assert.That(CurrentSimulation.World.Single(e => e.Has<PlayerTag>()).Get<ViewSkin>().Id, Is.EqualTo(SkinId.Alternate));
        }

        private static void Activate(VisualElement target)
        {
            using var evt = NavigationSubmitEvent.GetPooled();
            evt.target = target;
            target.SendEvent(evt);
        }

        [UnityTest]
        public IEnumerator SkinCycle_LoadsAllThreeAnimatorAdaptersWithoutReplacingTheSimulation()
        {
            Simulation simulation = CurrentSimulation;
            Entity player = simulation.World.Single(entity => entity.Has<PlayerTag>());
            foreach (SkinId skin in new[] { SkinId.Default, SkinId.Alternate, SkinId.Punk, SkinId.Default })
            {
                if (player.Get<ViewSkin>().Id != skin)
                {
                    _controller.RequestNextSkin();
                    yield return null;
                }
                float deadline = Time.realtimeSinceStartup + 5f;
                while (!player.Has<PlayerAnimatorComponent>() && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(player.Get<ViewSkin>().Id, Is.EqualTo(skin));
                Assert.That(player.Has<PlayerAnimatorComponent>(), Is.True);
                Assert.That(player.Get<PlayerAnimatorComponent>().Animator != null, Is.True);
                Assert.That(CurrentSimulation, Is.SameAs(simulation));
                Assert.That(player.Has<ViewMove>(), Is.False);
            }
        }

        [UnityTest]
        public IEnumerator SelectionUiUsesKeyboardWithoutLeakingGameplayShortcuts()
        {
            yield return Press(Key.N);
            float deadline = Time.realtimeSinceStartup + 10f;
            while (_controller.Phase != GameplayPhase.SelectingCharacter && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.That(_controller.Phase, Is.EqualTo(GameplayPhase.SelectingCharacter));
            Simulation simulation = CurrentSimulation;
            Entity player = simulation.World.Single(e => e.Has<PlayerTag>());
            GameplaySnapshot before = simulation.CaptureSnapshot();
            yield return Press(Key.R);
            yield return Press(Key.Z);
            yield return Press(Key.K);
            Assert.That(CurrentSimulation, Is.SameAs(simulation));
            Assert.That(simulation.CaptureSnapshot(), Is.EqualTo(before));
            Assert.That(player.Has<TransformComponent>(), Is.False);
            yield return Press(Key.RightArrow);
            yield return Press(Key.Enter);
            Assert.That(_controller.SelectedCharacter, Is.EqualTo(SkinId.Alternate));
            yield return Press(Key.Tab);
            yield return Press(Key.Tab);
            yield return Press(Key.Enter);
            while (_controller.Phase != GameplayPhase.Playing && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.That(_controller.Phase, Is.EqualTo(GameplayPhase.Playing));
            Assert.That(player.Get<ViewSkin>().Id, Is.EqualTo(SkinId.Alternate));
            Assert.That(player.Has<PlayerAnimatorComponent>(), Is.True);
            Assert.That(_controller.Moves, Is.Zero);
            Transform view = player.Get<TransformComponent>().Transform;
            yield return Press(Key.K);
            Assert.That(player.Get<TransformComponent>().Transform, Is.SameAs(view));
            Assert.That(player.Get<ViewSkin>().Id, Is.EqualTo(SkinId.Alternate));
        }

        [UnityTest]
        public IEnumerator SceneUnload_ReleasesControllerWorldAndGeneratedViews()
        {
            SceneContext context = _scene.GetRootGameObjects().Select(root => root.GetComponent<SceneContext>())
                .Single(component => component != null);
            context.Container.Resolve<TickableManager>().Remove(_controller);
            CurrentSimulation.Send(MoveCommand.Left);
            _controller.Tick(0.16f);
            CurrentSimulation.Send(MoveCommand.Up);
            _controller.Tick(0.16f);
            _controller.Tick(0.05f);
            Entity box = CurrentSimulation.World.Single(e => e.Has<BoxTag>() && e.Get<GridPosition>() == new GridPosition(2, 4));
            BoxGoalAnimator animator = box.Get<BoxGoalFeedbackComponent>().Animator;
            Transform visual = animator.transform.Find("Visual");
            Assert.That(animator.IsPlaying, Is.True);
            Assert.That(PrimeTween.Tween.GetTweensCount(visual), Is.GreaterThan(0));
            Simulation previous = CurrentSimulation;
            int disposedWorlds = 0;
            using var subscription = previous.World.SubscribeWorldDisposed(_ => disposedWorlds++);
            Transform previousBoard = Board;
            GameObject host = _levelRoot.gameObject;
            yield return SceneManager.UnloadSceneAsync(_scene);
            Assert.That(host == null, Is.True);
            Assert.That(previousBoard == null, Is.True);
            Assert.That(disposedWorlds, Is.EqualTo(1));
            Assert.That(CurrentSimulation, Is.Null);
            Assert.That(PrimeTween.Tween.GetTweensCount(visual), Is.Zero);
            _scene = default;
        }

        [UnityTest]
        public IEnumerator Restart_ReusesLoadedPrefabsAfterCatalogChanges()
        {
            FieldInfo entries = typeof(ViewCatalog).GetField("<Entries>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            var originalEntries = (List<ViewCatalog.KindEntry>)entries.GetValue(_catalog);
            var invalidEntries = originalEntries.Select(entry => new ViewCatalog.KindEntry
            {
                Kind = entry.Kind, Skins = entry.Skins.Select(skin => new ViewCatalog.SkinEntry
                {
                    Skin = skin.Skin, Address = skin.Address
                }).ToList()
            }).ToList();
            int boxIndex = invalidEntries.FindIndex(entry => entry.Kind == ViewKind.Box);
            Simulation previous = CurrentSimulation;
            try
            {
                invalidEntries[boxIndex].Skins.Single(skin => skin.Skin == SkinId.Default).Address = "invalid";
                entries.SetValue(_catalog, invalidEntries);
                SetRestartInput();
                TickController();
                Assert.That(CurrentSimulation, Is.Not.SameAs(previous));
                Assert.That(Board.childCount, Is.EqualTo(76));
                AssertActors(new Vector3(3, 0, 2), new Vector3(2, 0, 3), new Vector3(4, 0, 3));
                yield return Move(MoveCommand.Left);
                AssertActors(new Vector3(2, 0, 2), new Vector3(2, 0, 3), new Vector3(4, 0, 3));
            }
            finally
            {
                entries.SetValue(_catalog, originalEntries);
            }
        }

        [UnityTest]
        public IEnumerator WorldProjection_AlignsAllEntityViewsUnderAnOffsetParent()
        {
            _levelRoot.position = new Vector3(10, 3, -4);
            _levelRoot.rotation = Quaternion.Euler(0, 37, 0);
            SetRestartInput();
            TickController();

            AssertActors(new Vector3(3, 0, 2), new Vector3(2, 0, 3), new Vector3(4, 0, 3));
            Transform floor = View("Floor (1, 1)");
            Transform wall = View("Wall (0, 0)");
            Transform goal = View("Goal (2, 4)");
            AssertPosition(floor, new Vector3(1, 0, 1));
            AssertPosition(wall, Vector3.zero);
            AssertPosition(goal, new Vector3(2, 0, 4));
            Assert.That(Quaternion.Angle(wall.rotation, Quaternion.identity), Is.LessThan(0.001f));

            Vector3 disturbed = new Vector3(99, 99, 99);
            floor.position = disturbed;
            wall.position = disturbed;
            goal.position = disturbed;
            View("Player (3, 2)").position = disturbed;
            TickController();

            AssertPosition(floor, new Vector3(1, 0, 1));
            AssertPosition(wall, Vector3.zero);
            AssertPosition(goal, new Vector3(2, 0, 4));
            AssertActors(new Vector3(3, 0, 2), new Vector3(2, 0, 3), new Vector3(4, 0, 3));
            yield return null;
        }

        private static void AssertPosition(Transform target, Vector3 expected) =>
            Assert.That(Vector3.Distance(target.position, expected), Is.LessThan(0.0001f));

        // Restart disables the old board immediately; Unity destroys it at the end of the frame.
        private Transform Board => _levelRoot.Cast<Transform>()
            .Single(child => child.name == "Level view" && child.gameObject.activeSelf);

        private Transform View(string name) => Board.Find(name);

        private void AssertActors(Vector3 player, Vector3 firstBox, Vector3 secondBox)
        {
            AssertPosition(View("Player (3, 2)"), player);
            AssertPosition(View("Box (2, 3)"), firstBox);
            AssertPosition(View("Box (4, 3)"), secondBox);
        }

        private IEnumerator Move(MoveCommand command)
        {
            CurrentSimulation.Send(command);
            TickController();
            yield return null;
            yield return WaitForMotion();
        }

        private IEnumerator WaitForMotion()
        {
            float deadline = Time.realtimeSinceStartup + 2f;
            while (CurrentSimulation.World.Any(entity => entity.Has<ViewMove>()) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(CurrentSimulation.World.Any(entity => entity.Has<ViewMove>()), Is.False);
        }

        private void SetRestartInput()
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            InputSystem.Update();
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.R));
            InputSystem.Update();
            _keyboard.MakeCurrent();
        }

        private void TickController()
        {
            try
            {
                _controller.Tick();
            }
            finally
            {
                InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
                InputSystem.Update();
                _keyboard.MakeCurrent();
            }
        }
        private IEnumerator Press(params Key[] keys)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keys));
            yield return null;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null;
            yield return WaitForMotion();
        }
    }
}
#endif
