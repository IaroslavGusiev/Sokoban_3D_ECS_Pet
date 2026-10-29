using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using DefaultEcs;
using NUnit.Framework;
using Sokoban.Core;
using Sokoban.Core.Commands;
using Sokoban.Core.Components;
using Sokoban.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Sokoban.Tests.PlayMode
{
    public sealed class HintLifecycleTests
    {
        private readonly List<Object> _objects = new();
        private readonly List<GameplayController> _controllers = new();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameplayController controller in _controllers) controller.Dispose();
            foreach (Object target in _objects) if (target != null) Object.Destroy(target);
            _controllers.Clear();
            _objects.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator MovementInvalidatesWorkWhileClockAndInputRemainResponsive()
        {
            Fixture f = Create();
            Start(f);
            Assert.That(f.Controller.CanHint, Is.False);
            f.Controller.RequestHint();
            TestCharacterSelection.Tick(f.Controller, 1f);
            Assert.That(f.Controller.RemainingSeconds, Is.EqualTo(59f));
            Assert.That(f.Service.Calls.Count, Is.EqualTo(1));
            f.Input.Move = MoveCommand.Right;
            TestCharacterSelection.Tick(f.Controller, 0.16f);
            f.Input.Move = default;
            Assert.That(f.Controller.Moves, Is.EqualTo(1));
            Assert.That(SimulationOf(f).BoardRevision, Is.EqualTo(1));
            Assert.That(f.Service.Calls[0].Token.IsCancellationRequested, Is.True);
            f.Service.Calls[0].Source.SetResult(new HintResult(HintOutcome.Found, MoveCommand.Up));
            yield return null;
            TestCharacterSelection.Tick(f.Controller, 0f);
            Assert.That(f.Controller.HintStatus, Is.EqualTo(HintStatus.None));
            Assert.That(ActiveArrows(f), Is.Zero);
        }

        [UnityTest]
        public IEnumerator UndoInvalidatesAnInFlightRequest()
        {
            Fixture f = Create();
            Move(f, MoveCommand.Right);
            Start(f);
            f.Controller.RequestUndo();
            TestCharacterSelection.Tick(f.Controller, 0.16f);
            Assert.That(f.Controller.Moves, Is.Zero);
            Assert.That(SimulationOf(f).BoardRevision, Is.EqualTo(2));
            f.Service.Calls[0].Source.SetResult(new HintResult(HintOutcome.Found, MoveCommand.Up));
            yield return null;
            TestCharacterSelection.Tick(f.Controller, 0f);
            Assert.That(f.Controller.HintStatus, Is.EqualTo(HintStatus.None));
            Assert.That(f.Service.Calls[0].Token.IsCancellationRequested, Is.True);
            Assert.That(ActiveArrows(f), Is.Zero);
        }

        [UnityTest]
        public IEnumerator OldResultCannotReplaceANewerRequestAfterReturningToTheSameBoard() => OldCompletion(false);

        [UnityTest]
        public IEnumerator OldFailureCannotReplaceANewerRequestAfterReturningToTheSameBoard() => OldCompletion(true);

        private IEnumerator OldCompletion(bool failure)
        {
            Fixture f = Create();
            Start(f);
            LevelDefinition oldBoard = f.Service.Calls[0].Board;
            Move(f, MoveCommand.Right);
            f.Controller.RequestUndo();
            TestCharacterSelection.Tick(f.Controller, 0.16f);
            Assert.That(SimulationOf(f).CaptureSnapshot().PlayerPosition, Is.EqualTo(oldBoard.PlayerStart));
            Assert.That(SimulationOf(f).BoardRevision, Is.EqualTo(2));
            Start(f);
            Assert.That(f.Service.Calls.Count, Is.EqualTo(2));
            if (failure) f.Service.Calls[0].Source.SetException(new InvalidOperationException("old hint failure"));
            else f.Service.Calls[0].Source.SetResult(new HintResult(HintOutcome.Found, MoveCommand.Up));
            yield return null;
            TestCharacterSelection.Tick(f.Controller, 0f);
            Assert.That(f.Controller.HintStatus, Is.EqualTo(HintStatus.Calculating));
            Assert.That(ActiveArrows(f), Is.Zero);
            f.Service.Calls[1].Source.SetResult(new HintResult(HintOutcome.Found, MoveCommand.Right));
            yield return Apply(f);
            Assert.That(f.Controller.HintMove, Is.EqualTo(MoveCommand.Right));
            Assert.That(ActiveArrows(f), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator RestartRejectsTheOldRunResultAndReleasesItsVisualRoot()
        {
            Fixture f = Create();
            Start(f);
            Simulation previous = SimulationOf(f);
            Transform previousRoot = f.Root.GetChild(0);
            int disposals = 0;
            using var subscription = previous.World.SubscribeWorldDisposed(_ => disposals++);
            f.Controller.RequestRestart();
            TestCharacterSelection.Tick(f.Controller, 0f);
            Assert.That(disposals, Is.EqualTo(1));
            Assert.That(previousRoot.gameObject.activeSelf, Is.False);
            TestCharacterSelection.Tick(f.Controller, 0f);
            Start(f);
            f.Service.Calls[0].Source.SetResult(new HintResult(HintOutcome.Found, MoveCommand.Up));
            yield return null;
            TestCharacterSelection.Tick(f.Controller, 0f);
            Assert.That(previousRoot == null, Is.True);
            Assert.That(f.Controller.HintStatus, Is.EqualTo(HintStatus.Calculating));
            f.Service.Calls[1].Source.SetResult(new HintResult(HintOutcome.Found, MoveCommand.Right));
            yield return Apply(f);
            Assert.That(SimulationOf(f).World, Is.Not.SameAs(previous.World));
            Assert.That(f.Controller.HintMove, Is.EqualTo(MoveCommand.Right));
            Assert.That(ActiveArrows(f), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator CompletionRejectsLateSuccess()
        {
            Fixture f = Create(level: LevelParser.Parse("#####\n#@$.#\n#####"));
            Start(f);
            Move(f, MoveCommand.Right);
            Assert.That(f.Controller.Phase, Is.EqualTo(GameplayPhase.Completed));
            f.Service.Calls[0].Source.SetResult(new HintResult(HintOutcome.Found, MoveCommand.Right));
            yield return null;
            TestCharacterSelection.Tick(f.Controller, 0f);
            Assert.That(f.Controller.HintStatus, Is.EqualTo(HintStatus.None));
            Assert.That(f.Controller.CanHint, Is.False);
            Assert.That(ActiveArrows(f), Is.Zero);
        }

        [UnityTest]
        public IEnumerator TimeoutRejectsLateFailureAfterAutomaticRestart()
        {
            Fixture f = Create(limit: 1f);
            Start(f);
            TestCharacterSelection.Tick(f.Controller, 1f);
            f.Service.Calls[0].Source.SetException(new InvalidOperationException("late timeout failure"));
            yield return null;
            TestCharacterSelection.Tick(f.Controller, 0f);
            Assert.That(f.Controller.Phase, Is.EqualTo(GameplayPhase.Playing));
            Assert.That(f.Controller.HintStatus, Is.EqualTo(HintStatus.None));
            Assert.That(f.Controller.RemainingSeconds, Is.EqualTo(1f));
            Assert.That(ActiveArrows(f), Is.Zero);
        }

        [UnityTest]
        public IEnumerator DisposalObservesCancellationIgnoringFailureWithoutTouchingOldWorldOrViews()
        {
            Fixture f = Create();
            Start(f);
            f.Controller.Dispose();
            _controllers.Remove(f.Controller);
            Assert.That(f.Service.Calls[0].Token.IsCancellationRequested, Is.True);
            f.Service.Calls[0].Source.SetException(new InvalidOperationException("late disposed failure"));
            yield return null;
            Assert.That(f.Controller.HintStatus, Is.EqualTo(HintStatus.None));
            Assert.That(f.Root.childCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator OperationObservesLateFailureAndCanCancelAfterItsSourceIsDisposed()
        {
            var service = new ControlledService();
            var operation = new HintOperation(service, LevelParser.Parse("#####\n#@$.#\n#####"));
            operation.Cancel();
            Assert.That(service.Calls[0].Token.IsCancellationRequested, Is.True);
            service.Calls[0].Source.SetException(new InvalidOperationException("late disposed failure"));
            yield return null;
            Assert.That(operation.Task.IsCompleted, Is.True);
            Assert.That(operation.Task.IsFaulted, Is.False);
            Assert.That(operation.Task.GetAwaiter().GetResult().Error.Message, Is.EqualTo("late disposed failure"));
            Assert.DoesNotThrow(() => operation.Cancel());
        }

        [UnityTest]
        public IEnumerator OutcomesAndCurrentFailureHaveDistinctStatuses()
        {
            Fixture f = Create();
            HintOutcome[] outcomes = { HintOutcome.NoSolution, HintOutcome.BudgetExhausted, HintOutcome.AlreadySolved };
            HintStatus[] statuses = { HintStatus.NoSolution, HintStatus.BudgetExhausted, HintStatus.AlreadySolved };
            for (var index = 0; index < outcomes.Length; index++)
            {
                Start(f);
                f.Service.Calls[index].Source.SetResult(new HintResult(outcomes[index]));
                yield return Apply(f);
                Assert.That(f.Controller.HintStatus, Is.EqualTo(statuses[index]));
                Assert.That(f.Controller.CanHint, Is.True);
                Assert.That(ActiveArrows(f), Is.Zero);
            }
            Start(f);
            f.Service.Calls[3].Source.SetException(new InvalidOperationException("planned hint failure"));
            LogAssert.Expect(LogType.Exception, new Regex("planned hint failure"));
            yield return Apply(f);
            Assert.That(f.Controller.HintStatus, Is.EqualTo(HintStatus.Failed));
            Assert.That(f.Controller.CanHint, Is.True);
        }

        [UnityTest]
        public IEnumerator DisplayedArrowIsReusedAndIdleOrBlockedMovesPreserveIt()
        {
            Fixture f = Create();
            int loads = f.Provider.Requests;
            Start(f);
            f.Service.Calls[0].Source.SetResult(new HintResult(HintOutcome.Found, MoveCommand.Right));
            yield return Apply(f);
            HintArrow arrow = f.Root.GetComponentInChildren<HintArrow>();
            Assert.That(Vector3.Distance(arrow.transform.forward, Vector3.right), Is.LessThan(0.00001f));
            Assert.That(arrow.transform.position, Is.EqualTo(new Vector3(1.55f, 1.8f, 1f)));
            Move(f, MoveCommand.Left);
            TestCharacterSelection.Tick(f.Controller, 0.25f);
            Assert.That(f.Controller.HintStatus, Is.EqualTo(HintStatus.Available));
            Assert.That(SimulationOf(f).BoardRevision, Is.Zero);
            f.Controller.RequestNextSkin();
            TestCharacterSelection.Tick(f.Controller, 0f);
            Assert.That(SimulationOf(f).BoardRevision, Is.Zero);
            Move(f, MoveCommand.Right);
            Assert.That(arrow.gameObject.activeSelf, Is.False);
            Assert.That(f.Controller.HintStatus, Is.EqualTo(HintStatus.None));
            Start(f);
            f.Service.Calls[1].Source.SetResult(new HintResult(HintOutcome.Found, MoveCommand.Left));
            yield return Apply(f);
            Assert.That(f.Root.GetComponentInChildren<HintArrow>(), Is.SameAs(arrow));
            Assert.That(f.Root.GetComponentsInChildren<HintArrow>(true).Length, Is.EqualTo(1));
            Assert.That(f.Provider.Requests, Is.EqualTo(loads + 2), "Only the first arrow load and the intentional skin change requested views; the arrow instance is reused.");
        }

        [UnityTest]
        public IEnumerator UnavailableClicksAreDiscardedDuringInterpolationAndBinding()
        {
            Fixture f = Create();
            f.Input.Move = MoveCommand.Right;
            TestCharacterSelection.Tick(f.Controller, 0.04f);
            f.Input.Move = default;
            Assert.That(f.Controller.CanHint, Is.False);
            f.Controller.RequestHint();
            TestCharacterSelection.Tick(f.Controller, 0.12f);
            TestCharacterSelection.Tick(f.Controller, 0f);
            Assert.That(f.Service.Calls, Is.Empty);
            Assert.That(f.Controller.CanHint, Is.True);
            f.Provider.Pending = new UniTaskCompletionSource<GameObject>();
            f.Controller.RequestNextSkin();
            TestCharacterSelection.Tick(f.Controller, 0f);
            Assert.That(f.Controller.CanHint, Is.False);
            f.Controller.RequestHint();
            TestCharacterSelection.Tick(f.Controller, 0f);
            f.Provider.Pending.TrySetResult(f.Provider.Prefab);
            TestCharacterSelection.Tick(f.Controller, 0f);
            Assert.That(f.Service.Calls, Is.Empty);
            Assert.That(f.Controller.CanHint, Is.True);
            Start(f);
            f.Service.Calls[0].Source.SetCanceled();
            yield return Apply(f);
            Assert.That(f.Controller.HintStatus, Is.EqualTo(HintStatus.None));
        }

        [UnityTest]
        public IEnumerator RealServiceRunsTheSearchAndAppliesItsResultWithoutMovingThePlayer()
        {
            Fixture f = Create(service: new HintService(new HintSolver()));
            f.Controller.RequestHint();
            TestCharacterSelection.Tick(f.Controller, 0f);
            float deadline = Time.realtimeSinceStartup + 10f;
            while (f.Controller.HintStatus == HintStatus.Calculating && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                TestCharacterSelection.Tick(f.Controller, 0f);
            }
            Assert.That(f.Controller.HintStatus, Is.EqualTo(HintStatus.Available));
            Assert.That(f.Controller.HintMove, Is.EqualTo(MoveCommand.Right));
            Assert.That(f.Controller.Moves, Is.Zero);
            Assert.That(SimulationOf(f).BoardRevision, Is.Zero);
            Assert.That(ActiveArrows(f), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DelayedArrowKeepsTheHintPendingAndRepeatedRequestsCoalesce()
        {
            Fixture f = Create();
            yield return BeginArrow(f);
            Assert.That(f.Controller.HintStatus, Is.EqualTo(HintStatus.Calculating));
            Assert.That(f.Controller.HintMove, Is.EqualTo(default(MoveCommand)));
            Assert.That(f.Controller.CanHint, Is.False);
            for (int index = 0; index < 3; index++)
            {
                f.Controller.RequestHint();
                TestCharacterSelection.Tick(f.Controller, 1f);
            }
            Assert.That(f.Service.Calls.Count, Is.EqualTo(1));
            Assert.That(f.Provider.ArrowLoads.Count, Is.EqualTo(1));
            Assert.That(f.Controller.RemainingSeconds, Is.EqualTo(57f));
            Assert.That(f.Controller.Moves, Is.Zero);
            f.Provider.ArrowLoads[0].Source.TrySetResult(f.Provider.ArrowPrefab);
            yield return null;
            Assert.That(ActiveArrows(f), Is.Zero, "Loading alone must never apply a display outside the controller tick.");
            yield return Apply(f);
            Assert.That(ActiveArrows(f), Is.EqualTo(1));
            Assert.That(f.Controller.HintMove, Is.EqualTo(MoveCommand.Right));
        }

        [UnityTest]
        public IEnumerator MovementInvalidatesADelayedArrow()
        {
            Fixture f = Create();
            yield return BeginArrow(f);
            Move(f, MoveCommand.Right);
            Assert.That(f.Provider.ArrowLoads[0].Token.IsCancellationRequested, Is.True);
            f.Provider.ArrowLoads[0].Source.TrySetResult(f.Provider.ArrowPrefab);
            yield return null;
            TestCharacterSelection.Tick(f.Controller, 0f);
            Assert.That(f.Controller.HintStatus, Is.EqualTo(HintStatus.None));
            Assert.That(ActiveArrows(f), Is.Zero);
        }

        [UnityTest]
        public IEnumerator UndoInvalidatesADelayedArrow()
        {
            Fixture f = Create();
            Move(f, MoveCommand.Right);
            yield return BeginArrow(f);
            f.Controller.RequestUndo();
            TestCharacterSelection.Tick(f.Controller, 0.16f);
            Assert.That(f.Controller.Moves, Is.Zero);
            Assert.That(f.Provider.ArrowLoads[0].Token.IsCancellationRequested, Is.True);
            f.Provider.ArrowLoads[0].Source.TrySetResult(f.Provider.ArrowPrefab);
            yield return null;
            TestCharacterSelection.Tick(f.Controller, 0f);
            Assert.That(f.Controller.HintStatus, Is.EqualTo(HintStatus.None));
            Assert.That(ActiveArrows(f), Is.Zero);
        }

        [UnityTest]
        public IEnumerator ReplacementRejectsTheOldArrowWhileTheNewRequestLoads()
        {
            Fixture f = Create();
            yield return BeginArrow(f);
            Transform previousRoot = f.Root.GetChild(0);
            f.Controller.RequestRestart();
            TestCharacterSelection.Tick(f.Controller, 0f);
            TestCharacterSelection.Tick(f.Controller, 0f);
            yield return BeginArrow(f, 1);
            Assert.That(f.Provider.ArrowLoads[0].Token.IsCancellationRequested, Is.True);
            f.Provider.ArrowLoads[0].Source.TrySetResult(f.Provider.ArrowPrefab);
            yield return null;
            TestCharacterSelection.Tick(f.Controller, 0f);
            Assert.That(previousRoot == null, Is.True);
            Assert.That(ActiveArrows(f), Is.Zero);
            Assert.That(f.Controller.HintStatus, Is.EqualTo(HintStatus.Calculating));
            f.Provider.ArrowLoads[1].Source.TrySetResult(f.Provider.ArrowPrefab);
            yield return Apply(f);
            Assert.That(ActiveArrows(f), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ExplicitInvalidationRejectsAnOlderLoadOnTheSameRunAndRevision()
        {
            Fixture f = Create();
            yield return BeginArrow(f);
            f.Hints.Invalidate();
            yield return BeginArrow(f, 1);
            f.Provider.ArrowLoads[0].Source.TrySetException(new InvalidOperationException("stale arrow failure"));
            yield return null;
            TestCharacterSelection.Tick(f.Controller, 0f);
            Assert.That(f.Controller.HintStatus, Is.EqualTo(HintStatus.Calculating));
            Assert.That(ActiveArrows(f), Is.Zero);
            Assert.That(SimulationOf(f).BoardRevision, Is.Zero);
            f.Provider.ArrowLoads[1].Source.TrySetResult(f.Provider.ArrowPrefab);
            yield return Apply(f);
            Assert.That(ActiveArrows(f), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator UnavailableViewsInvalidateADelayedArrowEvenAfterRebinding()
        {
            Fixture f = Create();
            yield return BeginArrow(f);
            f.Provider.Pending = new UniTaskCompletionSource<GameObject>();
            f.Controller.RequestNextSkin();
            TestCharacterSelection.Tick(f.Controller, 0f);
            Assert.That(f.Provider.ArrowLoads[0].Token.IsCancellationRequested, Is.True);
            Assert.That(f.Controller.HintStatus, Is.EqualTo(HintStatus.None));
            f.Provider.Pending.TrySetResult(f.Provider.Prefab);
            TestCharacterSelection.Tick(f.Controller, 0f);
            f.Provider.ArrowLoads[0].Source.TrySetResult(f.Provider.ArrowPrefab);
            yield return null;
            TestCharacterSelection.Tick(f.Controller, 0f);
            Assert.That(f.Controller.CanHint, Is.True);
            Assert.That(ActiveArrows(f), Is.Zero);
        }

        [UnityTest]
        public IEnumerator DisposalRejectsLateArrowSuccess() => ArrowAfterDisposal(false);

        [UnityTest]
        public IEnumerator DisposalObservesLateArrowFailure() => ArrowAfterDisposal(true);

        private IEnumerator ArrowAfterDisposal(bool failure)
        {
            Fixture f = Create();
            yield return BeginArrow(f);
            f.Controller.Dispose();
            _controllers.Remove(f.Controller);
            Assert.That(f.Provider.ArrowLoads[0].Token.IsCancellationRequested, Is.True);
            if (failure) f.Provider.ArrowLoads[0].Source.TrySetException(new InvalidOperationException("late arrow failure"));
            else f.Provider.ArrowLoads[0].Source.TrySetResult(f.Provider.ArrowPrefab);
            yield return null;
            Assert.That(f.Root.childCount, Is.Zero);
            Assert.That(f.Controller.HintStatus, Is.EqualTo(HintStatus.None));
            Assert.That(f.Controller.CanHint, Is.False);
        }

        [UnityTest]
        public IEnumerator ArrowFailureLeavesGameplayUsableAndAllowsRetry()
        {
            Fixture f = Create();
            yield return BeginArrow(f);
            f.Provider.ArrowLoads[0].Source.TrySetException(new InvalidOperationException("arrow load failure"));
            yield return Apply(f);
            Assert.That(f.Controller.HintStatus, Is.EqualTo(HintStatus.Failed));
            Assert.That(f.Controller.Phase, Is.EqualTo(GameplayPhase.Playing));
            Assert.That(f.Controller.CanHint, Is.True);
            Assert.That(ActiveArrows(f), Is.Zero);
            Move(f, MoveCommand.Right);
            Assert.That(f.Controller.Moves, Is.EqualTo(1));
            yield return BeginArrow(f, 1);
            f.Provider.ArrowLoads[1].Source.TrySetResult(f.Provider.ArrowPrefab);
            yield return Apply(f);
            Assert.That(f.Controller.HintStatus, Is.EqualTo(HintStatus.Available));
        }

        [UnityTest]
        public IEnumerator RestartAndNextLevelKeepPriorityOverQueuedHints()
        {
            Fixture f = Create();
            f.Controller.RequestHint();
            f.Controller.RequestRestart();
            TestCharacterSelection.Tick(f.Controller, 0f);
            TestCharacterSelection.Tick(f.Controller, 0f);
            Assert.That(f.Service.Calls, Is.Empty);
            f.Controller.RequestHint();
            f.Input.NextLevelPressed = true;
            TestCharacterSelection.Tick(f.Controller, 0f);
            f.Input.NextLevelPressed = false;
            TestCharacterSelection.Tick(f.Controller, 0f);
            Assert.That(f.Service.Calls, Is.Empty);
            Assert.That(f.Controller.LevelNumber, Is.EqualTo(2));
            yield return null;
        }

        private IEnumerator BeginArrow(Fixture f, int requestIndex = 0)
        {
            f.Provider.DelayArrow = true;
            Start(f);
            f.Service.Calls[requestIndex].Source.SetResult(new HintResult(HintOutcome.Found, MoveCommand.Right));
            for (int frame = 0; frame < 20 && f.Provider.ArrowLoads.Count <= requestIndex; frame++)
            {
                yield return null;
                TestCharacterSelection.Tick(f.Controller, 0f);
            }
            Assert.That(f.Provider.ArrowLoads.Count, Is.EqualTo(requestIndex + 1));
        }

#if UNITY_EDITOR
        [TestCase(MoveCommand.Up, 0f, 1f)]
        [TestCase(MoveCommand.Down, 0f, -1f)]
        [TestCase(MoveCommand.Left, -1f, 0f)]
        [TestCase(MoveCommand.Right, 1f, 0f)]
        public void SavedArrowPrefab_HasNoColliderAndFacesTheRecommendedCell(MoveCommand move, float x, float z)
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Sokoban/Prefabs/HintArrow.prefab");
            var arrow = Object.Instantiate(prefab).GetComponent<HintArrow>();
            _objects.Add(arrow.gameObject);
            arrow.Show(new GridPosition(2, 3), move);
            Assert.That(arrow.gameObject.activeSelf, Is.True);
            Assert.That(Vector3.Distance(arrow.transform.forward, new Vector3(x, 0f, z)), Is.LessThan(0.00001f));
            Assert.That(Vector3.Distance(arrow.transform.position, new Vector3(2f + 0.55f * x, 1.8f, 3f + 0.55f * z)), Is.LessThan(0.00001f));
            Assert.That(arrow.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(arrow.GetComponentsInChildren<MeshRenderer>().Length, Is.EqualTo(2));
        }
#endif

        private IEnumerator Apply(Fixture f)
        {
            for (var frame = 0; frame < 20 && f.Controller.HintStatus == HintStatus.Calculating; frame++)
            {
                yield return null;
                TestCharacterSelection.Tick(f.Controller, 0f);
            }
            Assert.That(f.Controller.HintStatus, Is.Not.EqualTo(HintStatus.Calculating));
        }

        private static void Start(Fixture f)
        {
            Assert.That(f.Controller.CanHint, Is.True);
            f.Controller.RequestHint();
            TestCharacterSelection.Tick(f.Controller, 0f);
            Assert.That(f.Controller.HintStatus, Is.EqualTo(HintStatus.Calculating));
        }

        private static void Move(Fixture f, MoveCommand move)
        {
            f.Input.Move = move;
            TestCharacterSelection.Tick(f.Controller, 0.16f);
            f.Input.Move = default;
        }

        private static int ActiveArrows(Fixture f) => f.Root.GetComponentsInChildren<HintArrow>().Length;

        private static Simulation SimulationOf(Fixture f)
        {
            object run = typeof(GameplayController).GetField("_run", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(f.Controller);
            return (Simulation)run.GetType().GetField("_simulation", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(run);
        }

        private Fixture Create(float limit = 60f, LevelDefinition level = null, IHintService service = null)
        {
            var input = new Input();
            var controlled = new ControlledService();
            var provider = new Provider { Prefab = CreateObject("Actor prefab") };
            var root = CreateObject("Hint run root").transform;
            var prefab = CreateObject("HintArrow prefab").AddComponent<HintArrow>();
            prefab.gameObject.SetActive(false);
            provider.ArrowPrefab = prefab.gameObject;
            var factory = new LevelRunFactory(new LevelRoot(root), provider, new LevelPopulator(), new SimulationPipelineFactory(), new ViewPipelineFactory());
            GameplaySettings settings = TestGameplaySettings.Create(limit);
            _objects.Add(settings);
            level ??= LevelParser.Parse("########\n#@-$--.#\n########");
            var hints = new HintCoordinator(service ?? controlled, provider);
            var data = new TestStaticData { Gameplay = settings };
            TestCharacterSelection.Configure(data, _objects);
            var catalog = ScriptableObject.CreateInstance<BackgroundCatalog>();
            _objects.Add(catalog);
            var backgrounds = TestBackgrounds.Create(data, CreateObject("Background target"), catalog);
            var controller = new GameplayController(input, new Source(level), hints, backgrounds, factory, data);
            _controllers.Add(controller);
            controller.Initialize();
            TestCharacterSelection.Tick(controller, 0f);
            return new Fixture(controller, hints, input, controlled, provider, root);
        }

        private GameObject CreateObject(string name)
        {
            var target = new GameObject(name);
            _objects.Add(target);
            return target;
        }

        private sealed class Fixture
        {
            public GameplayController Controller { get; }
            public HintCoordinator Hints { get; }
            public Input Input { get; }
            public ControlledService Service { get; }
            public Provider Provider { get; }
            public Transform Root { get; }

            public Fixture(GameplayController controller, HintCoordinator hints, Input input, ControlledService service, Provider provider, Transform root)
            {
                Controller = controller;
                Hints = hints;
                Input = input;
                Service = service;
                Provider = provider;
                Root = root;
            }
        }

        private sealed class Input : IPlayerInput
        {
            public MoveCommand Move;
            public MoveCommand ReadMove() => Move;
            public bool RestartPressed => false;
            public bool NextLevelPressed { get; set; }
            public bool IsNeutral => true;
            public bool UndoPressed => false;
        }

        private sealed class Source : ILevelSource
        {
            private readonly LevelDefinition _level;
            public int Count => 2;
            public Source(LevelDefinition level) => _level = level;
            public UniTask<LevelDefinition> LoadAsync(int index, CancellationToken token) => UniTask.FromResult(_level);
        }

        private sealed class Provider : IViewProvider
        {
            public GameObject Prefab;
            public GameObject ArrowPrefab;
            public bool DelayArrow;
            public List<ArrowLoad> ArrowLoads { get; } = new();
            public int Requests;
            public UniTaskCompletionSource<GameObject> Pending;
            public UniTask<GameObject> LoadAsync(ViewKey key, CancellationToken token)
            {
                Requests++;
                if (key.Kind != ViewKind.HintArrow) return Pending?.Task ?? UniTask.FromResult(Prefab);
                var load = new ArrowLoad(token);
                ArrowLoads.Add(load);
                return DelayArrow ? load.Source.Task : UniTask.FromResult(ArrowPrefab);
            }

            public sealed class ArrowLoad
            {
                public CancellationToken Token { get; }
                public UniTaskCompletionSource<GameObject> Source { get; } = new();
                public ArrowLoad(CancellationToken token) => Token = token;
            }
        }

        public sealed class ControlledService : IHintService
        {
            public List<Call> Calls { get; } = new();
            public Task<HintResult> CalculateAsync(LevelDefinition board, CancellationToken token)
            {
                var call = new Call(board, token);
                Calls.Add(call);
                return call.Source.Task;
            }

            public sealed class Call
            {
                public LevelDefinition Board { get; }
                public CancellationToken Token { get; }
                public TaskCompletionSource<HintResult> Source { get; } = new();
                public Call(LevelDefinition board, CancellationToken token)
                {
                    Board = board;
                    Token = token;
                }
            }
        }
    }
}
