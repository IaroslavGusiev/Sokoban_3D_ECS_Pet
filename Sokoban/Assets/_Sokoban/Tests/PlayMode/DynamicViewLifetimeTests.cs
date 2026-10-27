using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using DefaultEcs;
using NUnit.Framework;
using PrimeTween;
using Sokoban.Core.Components;
using Sokoban.Presentation;
using Sokoban.Presentation.Ecs.Components;
using Sokoban.Presentation.Ecs.Model;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Sokoban.Tests.PlayMode
{
    public sealed class DynamicViewLifetimeTests
    {
        private readonly List<World> _worlds = new();
        private readonly List<ViewLayer> _layers = new();
        private readonly List<ViewAssets> _caches = new();
        private readonly List<GameObject> _objects = new();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (ViewLayer layer in _layers)
            {
                layer.Dispose();
            }

            foreach (World world in _worlds)
            {
                world.Dispose();
            }

            foreach (ViewAssets cache in _caches)
            {
                cache.Dispose();
            }

            foreach (GameObject target in _objects)
            {
                if (target != null)
                {
                    Object.Destroy(target);
                }
            }

            _layers.Clear();
            _worlds.Clear();
            _caches.Clear();
            _objects.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator CreationBindsDuringAnExistingRunWithAdaptersAndNoDuplicateRequests()
        {
            GameObject prefab = CreatePrefab();
            var provider = new Provider { Immediate = prefab };
            Fixture f = Create(provider);
            Entity first = Floor(f.World, 2, 3);
            f.Layer.Tick(0f);
            Assert.That(first.Has<TestViewLifetimeMarker>(), Is.True);
            Assert.That(View(first).position, Is.EqualTo(new Vector3(2, 0, 3)));
            Assert.That(View(first).GetComponent<TestViewLifetimeAdapter>().InstallCalls, Is.EqualTo(1));
            Entity second = Floor(f.World, 5, 7);
            Assert.That(f.Layer.Status, Is.EqualTo(ViewStatus.Binding));
            f.Layer.Tick(0f);
            for (int tick = 0; tick < 10; tick++)
            {
                f.Layer.Tick(0f);
            }

            Assert.That(View(second).position, Is.EqualTo(new Vector3(5, 0, 7)));
            Assert.That(View(second).parent, Is.SameAs(f.Root));
            Assert.That(f.Root.childCount, Is.EqualTo(2));
            Assert.That(provider.Calls.Count, Is.EqualTo(2));
            Assert.That(f.Layer.Status, Is.EqualTo(ViewStatus.Ready));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RemovingTheLastBoundEntityCleansItsViewOnTheNextTickOnlyOnce()
        {
            var provider = new Provider { Immediate = CreatePrefab() };
            Fixture f = Create(provider);
            Entity entity = Floor(f.World, 1, 1);
            f.Layer.Tick(0f);
            Transform view = View(entity);
            var adapter = view.GetComponent<TestViewLifetimeAdapter>();
            entity.Dispose();
            Assert.That(view.gameObject.activeSelf, Is.True);
            f.Layer.Tick(0f);
            Assert.That(view.gameObject.activeSelf, Is.False);
            Assert.That(adapter.DisableCalls, Is.EqualTo(1));
            Assert.That(adapter.UninstallCalls, Is.Zero, "Destroyed entities must never be passed to adapters.");
            f.Layer.Dispose();
            _layers.Remove(f.Layer);
            yield return null;
            Assert.That(view == null, Is.True);
            Assert.That(adapter.DestroyCalls, Is.EqualTo(1));
            Assert.That(adapter.DisableCalls, Is.EqualTo(1));
            Assert.That(f.Parent.childCount, Is.Zero);
            Assert.That(f.World.CreateEntity().IsAlive, Is.True);
        }

        [UnityTest]
        public IEnumerator RemovalBeforeCompletionRejectsLateSuccess() => RemovedPending(false);

        [UnityTest]
        public IEnumerator RemovalBeforeCompletionObservesLateFailure() => RemovedPending(true);

        [UnityTest]
        public IEnumerator BoundEntityRecreationGetsFreshOwnershipBeforeDeferredViewDestruction()
        {
            var provider = new Provider { Immediate = CreatePrefab() };
            Fixture f = Create(provider);
            Entity removed = Floor(f.World, 1, 1);
            f.Layer.Tick(0f);
            Transform oldView = View(removed);
            ViewOwnership oldOwnership = removed.Get<ViewOwnership>();
            removed.Dispose();
            Entity replacement = Floor(f.World, 4, 6);
            f.Layer.Tick(0f);
            Assert.That(oldView.gameObject.activeSelf, Is.False);
            Assert.That(replacement.Get<ViewOwnership>(), Is.Not.SameAs(oldOwnership));
            Assert.That(View(replacement), Is.Not.SameAs(oldView));
            Assert.That(View(replacement).position, Is.EqualTo(new Vector3(4, 0, 6)));
            Assert.That(replacement.Has<TestViewLifetimeMarker>(), Is.True);
            yield return null;
            f.Layer.Tick(0f);
            Assert.That(oldView == null, Is.True);
            Assert.That(f.Root.childCount, Is.EqualTo(1));
            Assert.That(provider.Calls.Count, Is.EqualTo(2));
        }

        private IEnumerator RemovedPending(bool fail)
        {
            var provider = new Provider();
            Fixture f = Create(provider);
            Entity entity = Floor(f.World, 1, 2);
            f.Layer.Tick(0f);
            ViewLoadOperation operation = entity.Get<ViewOwnership>().Request;
            entity.Dispose();
            f.Layer.Tick(0f);
            Assert.That(provider.Calls[0].Token.IsCancellationRequested, Is.True);
            Assert.That(f.Layer.Status, Is.EqualTo(ViewStatus.Ready));
            if (fail)
            {
                provider.Fail(0);
            }
            else
            {
                provider.Complete(0, CreatePrefab());
            }

            yield return null;
            Assert.That(operation.Task.IsCompleted, Is.True);
            Assert.That(operation.Task.IsFaulted, Is.False);
            Assert.That(operation.Task.GetAwaiter().GetResult().Error != null, Is.EqualTo(fail));
            Assert.DoesNotThrow(operation.Cancel);
            f.Layer.Tick(0f);
            Assert.That(f.Root.childCount, Is.Zero);
            Assert.That(provider.Calls.Count, Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RecreatedEntityRejectsAnOlderPendingSuccess() => RecreatedPending(false);

        [UnityTest]
        public IEnumerator RecreatedEntityRejectsAnOlderPendingFailure() => RecreatedPending(true);

        private IEnumerator RecreatedPending(bool fail)
        {
            var provider = new Provider();
            Fixture f = Create(provider);
            Entity removed = Floor(f.World, 1, 1);
            f.Layer.Tick(0f);
            ViewOwnership oldOwnership = removed.Get<ViewOwnership>();
            removed.Dispose();
            Entity replacement = Floor(f.World, 7, 8);
            f.Layer.Tick(0f);
            Assert.That(replacement, Is.Not.EqualTo(removed));
            Assert.That(replacement.Get<ViewOwnership>(), Is.Not.SameAs(oldOwnership));
            Assert.That(provider.Calls[0].Token.IsCancellationRequested, Is.True);
            Assert.That(provider.Calls[1].Token.IsCancellationRequested, Is.False);
            provider.Complete(1, CreatePrefab());
            yield return null;
            Assert.That(replacement.Has<TransformComponent>(), Is.False, "Async completion alone must not apply a view.");
            f.Layer.Tick(0f);
            Transform current = View(replacement);
            if (fail)
            {
                provider.Fail(0);
            }
            else
            {
                provider.Complete(0, CreatePrefab());
            }

            yield return null;
            f.Layer.Tick(0f);
            Assert.That(View(replacement), Is.SameAs(current));
            Assert.That(current.position, Is.EqualTo(new Vector3(7, 0, 8)));
            Assert.That(replacement.Has<TestViewLifetimeMarker>(), Is.True);
            Assert.That(replacement.Has<ViewFailed>(), Is.False);
            Assert.That(replacement.Has<ViewPending>(), Is.False);
            Assert.That(f.Root.childCount, Is.EqualTo(1));
            Assert.That(provider.Calls.Count, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator OverlappingOldEntityAndSkinRequestsCannotOverwriteTheCurrentView()
        {
            var provider = new Provider();
            Fixture f = Create(provider);
            Entity old = Floor(f.World, 1, 1);
            f.Layer.Tick(0f);
            old.Dispose();
            Entity current = Floor(f.World, 3, 4);
            f.Layer.Tick(0f);
            current.Set(new SkinChangeRequest(SkinId.Alternate));
            f.Layer.Tick(0f);
            provider.Complete(1, CreatePrefab());
            yield return null;
            f.Layer.Tick(0f);
            Assert.That(current.Has<ViewPending>(), Is.False);
            Assert.That(current.Has<TransformComponent>(), Is.False);
            f.Layer.Tick(0f);
            Assert.That(provider.Calls[2].Key.Skin, Is.EqualTo(SkinId.Alternate));
            provider.Complete(2, CreatePrefab());
            yield return null;
            f.Layer.Tick(0f);
            Transform alternate = View(current);
            provider.Complete(0, CreatePrefab());
            yield return null;
            f.Layer.Tick(0f);
            Assert.That(View(current), Is.SameAs(alternate));
            Assert.That(f.Root.childCount, Is.EqualTo(1));
            Assert.That(current.Get<ViewSkin>().Id, Is.EqualTo(SkinId.Alternate));
        }

        [UnityTest]
        public IEnumerator BoundSkinSwitchAndRemovalUseOneCleanupOwner()
        {
            var provider = new Provider { Immediate = CreatePrefab() };
            Fixture f = Create(provider);
            Entity entity = Floor(f.World, 1, 2);
            f.Layer.Tick(0f);
            var old = View(entity).GetComponent<TestViewLifetimeAdapter>();
            entity.Set(new SkinChangeRequest(SkinId.Alternate));
            f.Layer.Tick(0f);
            var current = View(entity).GetComponent<TestViewLifetimeAdapter>();
            Assert.That(current, Is.Not.SameAs(old));
            Assert.That(old.UninstallCalls, Is.EqualTo(1));
            Assert.That(old.DisableCalls, Is.EqualTo(1));
            entity.Dispose();
            f.Layer.Tick(0f);
            f.Layer.Dispose();
            _layers.Remove(f.Layer);
            yield return null;
            Assert.That(old.UninstallCalls, Is.EqualTo(1));
            Assert.That(old.DestroyCalls, Is.EqualTo(1));
            Assert.That(current.UninstallCalls, Is.Zero);
            Assert.That(current.DisableCalls, Is.EqualTo(1));
            Assert.That(current.DestroyCalls, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator RunDisposalUninstallsLiveAdaptersAndObservesPendingFailureAfterWorldDisposal()
        {
            var provider = new Provider { Immediate = CreatePrefab() };
            Fixture f = Create(provider);
            Entity bound = Floor(f.World, 1, 2);
            f.Layer.Tick(0f);
            var adapter = View(bound).GetComponent<TestViewLifetimeAdapter>();
            provider.Immediate = null;
            Entity pending = Floor(f.World, 3, 4);
            f.Layer.Tick(0f);
            f.Layer.Dispose();
            _layers.Remove(f.Layer);
            Assert.That(bound.Has<TestViewLifetimeMarker>(), Is.False);
            Assert.That(bound.Has<TransformComponent>(), Is.False);
            Assert.That(pending.Has<ViewPending>(), Is.False);
            Assert.That(provider.Calls[1].Token.IsCancellationRequested, Is.True);
            Assert.That(adapter.UninstallCalls, Is.EqualTo(1));
            f.World.Dispose();
            _worlds.Remove(f.World);
            provider.Fail(1);
            yield return null;
            Assert.That(f.Parent.childCount, Is.Zero);
            Assert.That(adapter.DestroyCalls, Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator RemovingAViewWithActiveFeedbackStopsAndRestoresTheVisual()
        {
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AddressablesDependencies/Box/Prefabs/Box.prefab");
            var provider = new Provider { Immediate = prefab };
            Fixture f = Create(provider);
            // A floor is a controlled viewable entity, not a dynamic gameplay box.
            Entity entity = Floor(f.World, 2, 3);
            f.Layer.Tick(0f);
            var animator = View(entity).GetComponent<BoxGoalAnimator>();
            Transform visual = animator.transform.Find("Visual");
            Vector3 position = visual.localPosition;
            Vector3 scale = visual.localScale;
            animator.PlayGoalFeedback();
            animator.Tick(0.055f);
            Assert.That(animator.IsPlaying, Is.True);
            Assert.That(Tween.GetTweensCount(visual), Is.GreaterThan(0));
            entity.Dispose();
            f.Layer.Tick(0f);
            Assert.That(animator.IsPlaying, Is.False);
            Assert.That(Tween.GetTweensCount(visual), Is.Zero);
            Assert.That(visual.localPosition, Is.EqualTo(position));
            Assert.That(visual.localScale, Is.EqualTo(scale));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RemovingOneSharedWaiterKeepsTheOtherAndReleasesOnlyAtCacheDisposal()
        {
            var operation = new TestOperation<GameObject>();
            var handle = UnityEngine.AddressableAssets.Addressables.ResourceManager.StartOperation(operation, default);
            var addressables = new TestAddressablesService(load: (_, _) => handle);
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<ViewCatalog>("Assets/Addressables/Config/LevelViewCatalog.asset");
            var cache = new ViewAssets(new TestStaticData { Views = catalog }, addressables);
            _caches.Add(cache);
            Fixture f = Create(cache);
            Entity removed = Floor(f.World, 1, 1);
            Entity survivor = Floor(f.World, 3, 3);
            f.Layer.Tick(0f);
            Assert.That(addressables.LoadHandles.Count, Is.EqualTo(1));
            removed.Dispose();
            f.Layer.Tick(0f);
            Assert.That(addressables.ReleaseCalls, Is.Zero);
            Assert.That(handle.IsValid(), Is.True);
            Assert.That(operation.DestroyCalls, Is.Zero);
            operation.Finish(CreatePrefab());
            yield return null;
            f.Layer.Tick(0f);
            Assert.That(survivor.Has<TransformComponent>(), Is.True);
            Assert.That(f.Root.childCount, Is.EqualTo(1));
            Assert.That(handle.ReferenceCount, Is.EqualTo(1));
            survivor.Dispose();
            f.Layer.Tick(0f);
            f.Layer.Dispose();
            _layers.Remove(f.Layer);
            yield return null;
            Assert.That(f.Parent.childCount, Is.Zero);
            Assert.That(addressables.ReleaseCalls, Is.Zero);
            cache.Dispose();
            _caches.Remove(cache);
            Assert.That(addressables.ReleaseCalls, Is.EqualTo(1));
            Assert.That(operation.DestroyCalls, Is.EqualTo(1));
        }
#endif

        private Fixture Create(IViewProvider provider)
        {
            var world = new World();
            _worlds.Add(world);
            Transform parent = CreateObject("Dynamic parent").transform;
            parent.position = new Vector3(10, 4, 20);
            var layer = new ViewLayer(new LevelRoot(parent), world, provider, new ViewPipelineFactory(),
                new RunOptions(SkinId.Default, 60f, 0.16f));
            _layers.Add(layer);
            layer.Tick(0f);
            return new Fixture(world, layer, parent);
        }

        private GameObject CreatePrefab()
        {
            GameObject prefab = CreateObject("Dynamic prefab");
            var adapter = prefab.AddComponent<TestViewLifetimeAdapter>();
            var view = prefab.AddComponent<EntityView>();
            typeof(EntityView).GetField("adapters", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(view, new List<EntityAdapter> { adapter });
            return prefab;
        }

        private GameObject CreateObject(string name)
        {
            var target = new GameObject(name);
            _objects.Add(target);
            return target;
        }

        private static Entity Floor(World world, int x, int y)
        {
            Entity entity = world.CreateEntity();
            entity.Set<FloorTag>();
            entity.Set(new GridPosition(x, y));
            return entity;
        }

        private static Transform View(Entity entity) =>
            entity.Get<TransformComponent>().Transform;

        private sealed class Fixture
        {
            public World World { get; }
            public ViewLayer Layer { get; }
            public Transform Parent { get; }
            public Transform Root =>
                Parent.GetChild(0);

            public Fixture(World world, ViewLayer layer, Transform parent)
            {
                World = world;
                Layer = layer;
                Parent = parent;
            }
        }

        private sealed class Provider : IViewProvider
        {
            public GameObject Immediate;
            public List<Call> Calls { get; } = new();

            public UniTask<GameObject> LoadAsync(ViewKey key, CancellationToken token)
            {
                var call = new Call(key, token);
                Calls.Add(call);
                return Immediate != null ? UniTask.FromResult(Immediate) : call.Source.Task;
            }

            public void Complete(int index, GameObject prefab) =>
                Calls[index].Source.TrySetResult(prefab);

            public void Fail(int index) =>
                Calls[index].Source.TrySetException(new InvalidOperationException("planned late view failure"));

            public sealed class Call
            {
                public ViewKey Key { get; }
                public CancellationToken Token { get; }
                public UniTaskCompletionSource<GameObject> Source { get; } = new();

                public Call(ViewKey key, CancellationToken token)
                {
                    Key = key;
                    Token = token;
                }
            }
        }
    }
}
