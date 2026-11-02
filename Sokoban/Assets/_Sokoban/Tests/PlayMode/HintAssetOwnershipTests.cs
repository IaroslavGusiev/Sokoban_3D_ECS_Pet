#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Sokoban.Core;
using Sokoban.Core.Commands;
using Sokoban.Presentation;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;

namespace Sokoban.Tests.PlayMode
{
    public sealed class HintAssetOwnershipTests
    {
        private readonly List<GameObject> _objects = new();
        private readonly List<LevelRun> _runs = new();
        private readonly List<HintCoordinator> _hints = new();
        private readonly List<TestOperation<GameObject>> _arrowLoads = new();
        private readonly List<AsyncOperationHandle<GameObject>> _arrowHandles = new();
        private ViewAssets _assets;
        private TestAddressablesService _addressables;
        private GameObject _actorPrefab;
        private GameObject _arrowPrefab;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (HintCoordinator hints in _hints)
            {
                hints.Dispose();
            }
            foreach (LevelRun run in _runs)
            {
                run.Dispose();
            }
            _assets?.Dispose();
            foreach (GameObject target in _objects)
            {
                if (target != null)
                {
                    UnityEngine.Object.Destroy(target);
                }
            }
            _hints.Clear();
            _runs.Clear();
            _objects.Clear();
            _arrowLoads.Clear();
            _arrowHandles.Clear();
            _assets = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator SharedCacheAndFactoriesKeepRunsIndependentAndCancellationRetainsTheHandle()
        {
            InitializeAssets();
            var root = CreateObject("Shared run parent").transform;
            var factory = new LevelRunFactory(new LevelRoot(root), _assets, new LevelPopulator(),
                viewPipelineFactory: new ViewPipelineFactory(),
                simulationPipelineFactory: new SimulationPipelineFactory());
            LevelDefinition level = LevelParser.Parse("########\n#@-$--.#\n########");
            LevelRun first = CreateRun(factory, level);
            LevelRun second = CreateRun(factory, level);
            var service = new ImmediateService();
            HintCoordinator firstHints = CreateHints(service);
            HintCoordinator secondHints = CreateHints(service);
            firstHints.Start(first, GameplayPhase.Playing);
            secondHints.Start(second, GameplayPhase.Playing);
            firstHints.Update(first, GameplayPhase.Playing);
            secondHints.Update(second, GameplayPhase.Playing);
            Assert.That(_arrowLoads.Count, Is.EqualTo(1));
            Assert.That(firstHints.Status, Is.EqualTo(HintStatus.Calculating));
            Assert.That(secondHints.Status, Is.EqualTo(HintStatus.Calculating));
            firstHints.Dispose();
            first.Dispose();
            _runs.Remove(first);
            Assert.That(_addressables.ReleaseCalls, Is.Zero);
            Assert.That(_arrowHandles[0].IsValid(), Is.True);
            _arrowLoads[0].Finish(_arrowPrefab);
            yield return null;
            secondHints.Update(second, GameplayPhase.Playing);
            Assert.That(firstHints.Status, Is.EqualTo(HintStatus.None));
            Assert.That(secondHints.Status, Is.EqualTo(HintStatus.Available));
            Assert.That(root.GetComponentsInChildren<HintArrow>(true).Length, Is.EqualTo(1));
            Assert.That(second.Result.Moves, Is.Zero);
            Assert.That(second.BoardRevision, Is.Zero);
            Assert.That(_arrowHandles[0].ReferenceCount, Is.EqualTo(1));
            Assert.That(_arrowLoads[0].DestroyCalls, Is.Zero);
            secondHints.Dispose();
            second.Dispose();
            _runs.Remove(second);
            yield return null;
            Assert.That(root.childCount, Is.Zero);
            Assert.That(_addressables.ReleaseCalls, Is.Zero);
            int cachedHandles = _addressables.LoadHandles.Count;
            _assets.Dispose();
            _assets = null;
            Assert.That(_addressables.ReleaseCalls, Is.EqualTo(cachedHandles));
            Assert.That(_arrowLoads[0].DestroyCalls, Is.EqualTo(1));
            Assert.That(_arrowHandles[0].IsValid(), Is.False);
        }

        [UnityTest]
        public IEnumerator FailedArrowHandleIsReleasedOnRetryWhileGameplayStaysUsable()
        {
            InitializeAssets();
            var factory = new LevelRunFactory(new LevelRoot(CreateObject("Retry root").transform), _assets,
                new LevelPopulator(),
                viewPipelineFactory: new ViewPipelineFactory(),
                simulationPipelineFactory: new SimulationPipelineFactory());
            LevelRun run = CreateRun(factory, LevelParser.Parse("########\n#@-$--.#\n########"));
            HintCoordinator hints = CreateHints(new ImmediateService());
            hints.Start(run, GameplayPhase.Playing);
            hints.Update(run, GameplayPhase.Playing);
            LogAssert.Expect(LogType.Exception, new Regex("planned arrow asset failure"));
            _arrowLoads[0].Fail(new InvalidOperationException("planned arrow asset failure"));
            yield return null;
            hints.Update(run, GameplayPhase.Playing);
            Assert.That(hints.Status, Is.EqualTo(HintStatus.Failed));
            Assert.That(hints.CanRequest(run, GameplayPhase.Playing), Is.True);
            Assert.That(run.ViewStatus, Is.EqualTo(ViewStatus.Ready));
            run.Send(MoveCommand.Right);
            run.Tick(0.16f);
            Assert.That(run.Result.Moves, Is.EqualTo(1));
            Assert.That(run.RemainingSeconds, Is.EqualTo(59.84f).Within(0.0001f));
            hints.Update(run, GameplayPhase.Playing);
            yield return null; // ViewAssets retries failed handles only in a later frame.
            hints.Start(run, GameplayPhase.Playing);
            hints.Update(run, GameplayPhase.Playing);
            Assert.That(_arrowLoads.Count, Is.EqualTo(2));
            Assert.That(_arrowLoads[0].DestroyCalls, Is.EqualTo(1));
            Assert.That(_addressables.ReleaseCalls, Is.EqualTo(1));
            _arrowLoads[1].Finish(_arrowPrefab);
            yield return null;
            hints.Update(run, GameplayPhase.Playing);
            Assert.That(hints.Status, Is.EqualTo(HintStatus.Available));
            Assert.That(run.Result.Moves, Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator OperationObservesAnArrowFailureAfterCancellationAndDisposesItsSource()
        {
            var provider = new IgnoringProvider();
            var operation = new HintOperation(new ImmediateService(), LevelParser.Parse("#####\n#@$.#\n#####"));
            operation.LoadArrow(provider);
            operation.Cancel();
            Assert.That(provider.Token.IsCancellationRequested, Is.True);
            provider.Source.TrySetException(new InvalidOperationException("late arrow observation"));
            yield return null;
            Assert.That(operation.ArrowTask.IsCompleted, Is.True);
            Assert.That(operation.ArrowTask.IsFaulted, Is.False);
            Assert.That(operation.ArrowTask.GetAwaiter().GetResult().Error.Message, Is.EqualTo("late arrow observation"));
            Assert.DoesNotThrow(operation.Cancel);
        }

        private void InitializeAssets()
        {
            _actorPrefab = CreateObject("Actor asset");
            _arrowPrefab = CreateObject("Arrow asset");
            _arrowPrefab.AddComponent<HintArrow>();
            _arrowPrefab.SetActive(false);
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<ViewCatalog>("Assets/Addressables/Config/LevelViewCatalog.asset");
            string arrowAddress = catalog.Resolve(new ViewKey(ViewKind.HintArrow, SkinId.Default)).Address;
            _addressables = new TestAddressablesService(load: (address, _) =>
            {
                var operation = new TestOperation<GameObject>();
                var handle = Addressables.ResourceManager.StartOperation(operation, default);
                if (address == arrowAddress)
                {
                    _arrowLoads.Add(operation);
                    _arrowHandles.Add(handle);
                }
                else
                {
                    operation.Finish(_actorPrefab);
                }
                return handle;
            });
            _assets = new ViewAssets(new TestStaticData { Views = catalog }, _addressables);
        }

        private LevelRun CreateRun(LevelRunFactory factory, LevelDefinition level)
        {
            LevelRun run = factory.Create(level, new RunOptions(SkinId.Default, 60f, 0.16f));
            _runs.Add(run);
            Assert.That(run.CanHint, Is.True);
            return run;
        }

        private HintCoordinator CreateHints(IHintService service)
        {
            var hints = new HintCoordinator(service, _assets);
            _hints.Add(hints);
            return hints;
        }

        private GameObject CreateObject(string name)
        {
            var target = new GameObject(name);
            _objects.Add(target);
            return target;
        }

        private sealed class ImmediateService : IHintService
        {
            public Task<HintResult> CalculateAsync(LevelDefinition board, CancellationToken token) =>
                Task.FromResult(new HintResult(HintOutcome.Found, MoveCommand.Right));
        }

        private sealed class IgnoringProvider : IViewProvider
        {
            public CancellationToken Token { get; private set; }
            public UniTaskCompletionSource<GameObject> Source { get; } = new();
            public UniTask<GameObject> LoadAsync(ViewKey key, CancellationToken token)
            {
                Token = token;
                return Source.Task;
            }
        }
    }
}
#endif
