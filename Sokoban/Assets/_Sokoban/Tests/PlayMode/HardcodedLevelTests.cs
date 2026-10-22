// These scene integration tests run in the Editor's Play Mode without adding the scene to build settings.
#if UNITY_EDITOR
using System.Collections;
using DefaultEcs;
using Sokoban.Core.Components;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Sokoban.Core.Commands;
using Sokoban.Unity;
using Sokoban.Core;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
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
            ITickable tickable = context.Container.ResolveAll<ITickable>().Single();
            Assert.That(tickable, Is.TypeOf<GameplayController>());
            _controller = context.Container.Resolve<GameplayController>();
            Assert.That(tickable, Is.SameAs(_controller));
            _levelRoot = context.Container.Resolve<LevelRoot>().Transform;
            _catalog = context.Container.Resolve<ViewCatalog>();
            Assert.That(Board.childCount, Is.EqualTo(76));
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
        public IEnumerator KeyboardSolution_SynchronizesActorsAndPreservesGoalFloors()
        {
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
            Assert.That(View("Box (4, 3)").position, Is.EqualTo(new Vector3(4, 0, 5)));
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
            _controller.Current.Send(MoveCommand.Up);
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
        public IEnumerator DisposingController_ReleasesCurrentWorldAndViews()
        {
            Transform previousBoard = Board;
            yield return Move(MoveCommand.Left);
            SceneContext context = _scene.GetRootGameObjects().Select(root => root.GetComponent<SceneContext>())
                .Single(component => component != null);
            context.Container.Resolve<TickableManager>().Remove(_controller);
            context.Container.Resolve<DisposableManager>().Remove(_controller);
            _controller.Dispose();
            Assert.That(previousBoard.gameObject.activeSelf, Is.False);
            Assert.That(_controller.Current, Is.Null);
            yield return null;
            Assert.That(previousBoard == null, Is.True);
            Assert.That(_levelRoot.childCount, Is.Zero);

        }

        [UnityTest]
        public IEnumerator QueuedMove_WaitsForTickAndUpdatesBothActorsBeforeTickReturns()
        {
            _controller.Current.Send(MoveCommand.Left);
            AssertActors(new Vector3(3, 0, 2), new Vector3(2, 0, 3), new Vector3(4, 0, 3));
            TickController();
            AssertActors(new Vector3(2, 0, 2), new Vector3(2, 0, 3), new Vector3(4, 0, 3));

            _controller.Current.Send(MoveCommand.Up);
            TickController();
            AssertActors(new Vector3(2, 0, 3), new Vector3(2, 0, 4), new Vector3(4, 0, 3));
            TickController();
            AssertActors(new Vector3(2, 0, 3), new Vector3(2, 0, 4), new Vector3(4, 0, 3));
            yield return null;
        }

        [UnityTest]
        public IEnumerator HeldKey_DoesNotRepeatAndSimultaneousDirectionsKeepPriority()
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.UpArrow, Key.LeftArrow));
            for (int i = 0; i < 8; i++) yield return null;
            AssertActors(new Vector3(3, 0, 3), new Vector3(2, 0, 3), new Vector3(4, 0, 3));
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null;
            yield return null;
            AssertActors(new Vector3(3, 0, 3), new Vector3(2, 0, 3), new Vector3(4, 0, 3));
        }

        [UnityTest]
        public IEnumerator SceneUnload_ReleasesControllerWorldAndGeneratedViews()
        {
            Simulation previous = _controller.Current;
            int disposedWorlds = 0;
            using var subscription = previous.World.SubscribeWorldDisposed(_ => disposedWorlds++);
            Transform previousBoard = Board;
            GameObject host = _levelRoot.gameObject;
            yield return SceneManager.UnloadSceneAsync(_scene);
            Assert.That(host == null, Is.True);
            Assert.That(previousBoard == null, Is.True);
            Assert.That(disposedWorlds, Is.EqualTo(1));
            Assert.That(_controller.Current, Is.Null);
            _scene = default;
        }

        [UnityTest]
        public IEnumerator FailedViewConstruction_ReleasesTheSimulationAndPartialBoardBeforeRetry()
        {
            FieldInfo boxPrefab = typeof(ViewCatalog).GetField("_boxPrefab", BindingFlags.Instance | BindingFlags.NonPublic);
            var originalPrefab = (GameObject)boxPrefab.GetValue(_catalog);
            var invalidPrefab = new GameObject("Missing EntityView");
            // Failed construction is outside the normal Zenject tick lifecycle.
            SceneContext context = _scene.GetRootGameObjects().Select(root => root.GetComponent<SceneContext>())
                .Single(component => component != null);
            context.Container.Resolve<TickableManager>().Remove(_controller);
            try
            {
                boxPrefab.SetValue(_catalog, invalidPrefab);

                SetRestartInput();
                Assert.Throws<System.InvalidOperationException>(() => TickController());
                Assert.That(_controller.Current, Is.Null);
                Assert.That(_levelRoot.Cast<Transform>().All(child => !child.gameObject.activeSelf), Is.True);
                yield return null;
                Assert.That(_levelRoot.childCount, Is.Zero);

                boxPrefab.SetValue(_catalog, originalPrefab);
                _controller.Initialize();
                Assert.That(Board.childCount, Is.EqualTo(76));
                AssertActors(new Vector3(3, 0, 2), new Vector3(2, 0, 3), new Vector3(4, 0, 3));
                yield return Move(MoveCommand.Left);
                AssertActors(new Vector3(2, 0, 2), new Vector3(2, 0, 3), new Vector3(4, 0, 3));
            }
            finally
            {
                boxPrefab.SetValue(_catalog, originalPrefab);
                Object.Destroy(invalidPrefab);
            }
        }

        [UnityTest]
        public IEnumerator WorldProjection_AlignsTheBoardUnderAnOffsetParentAndLeavesOrdinaryFloorsStatic()
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

            AssertPosition(floor, disturbed);
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
            _controller.Current.Send(command);
            TickController();
            yield return null;
            yield return null;
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
        }
    }
}
#endif
