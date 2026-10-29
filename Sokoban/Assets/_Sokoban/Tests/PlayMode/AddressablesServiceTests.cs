using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DefaultEcs;
using NUnit.Framework;
using Sokoban.Core;
using Sokoban.Core.Components;
using Sokoban.Presentation;
using Sokoban.Presentation.Ecs.Model;
using Sokoban.Presentation.Ecs.Components;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;

namespace Sokoban.Tests.PlayMode
{
    public sealed class AddressablesServiceTests
    {
        private readonly List<UnityEngine.Object> _assets = new();
        private readonly IAddressablesService _service = new AddressablesService();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (UnityEngine.Object asset in _assets) UnityEngine.Object.Destroy(asset);
            _assets.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator CancelledWaiter_DoesNotReleaseHandleOrCancelAnotherWaiter() => UniTask.ToCoroutine(async () =>
        {
            var operation = new PendingText();
            var handle = Addressables.ResourceManager.StartOperation(operation, default);
            using var cancellation = new CancellationTokenSource();
            try
            {
                UniTask<TextAsset> owner = _service.AwaitAsync(handle, CancellationToken.None);
                UniTask<TextAsset> waiter = _service.AwaitAsync(handle, cancellation.Token);
                cancellation.Cancel();
                Assert.That((await waiter.SuppressCancellationThrow()).IsCanceled, Is.True);
                Assert.That(handle.IsValid(), Is.True);
                Assert.That(operation.DestroyCalls, Is.Zero);
                TextAsset text = Text("result");
                operation.Finish(text);
                Assert.That(await owner, Is.SameAs(text));
                Assert.That(operation.DestroyCalls, Is.Zero);
            }
            finally { _service.Release(handle); }
            Assert.That(operation.DestroyCalls, Is.EqualTo(1));
        });

        [UnityTest]
        public IEnumerator AlreadyCancelledToken_WinsForACompletedHandle() => UniTask.ToCoroutine(async () =>
        {
            var operation = new PendingText();
            var handle = Addressables.ResourceManager.StartOperation(operation, default);
            operation.Finish(Text("result"));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            try
            {
                bool cancelled = await AwaitCancelled(handle, cancellation.Token);
                Assert.That(cancelled, Is.True);
                Assert.That(handle.IsValid(), Is.True);
                Assert.That(operation.DestroyCalls, Is.Zero);
            }
            finally { _service.Release(handle); }
            Assert.That(operation.DestroyCalls, Is.EqualTo(1));
        });

        [UnityTest]
        public IEnumerator Failure_PropagatesWithoutTakingOwnerReleaseResponsibility() => UniTask.ToCoroutine(async () =>
        {
            var operation = new PendingText();
            var handle = Addressables.ResourceManager.StartOperation(operation, default);
            Exception error = new InvalidOperationException("planned text load failure");
            try
            {
                UniTask<TextAsset> loading = _service.AwaitAsync(handle, CancellationToken.None);
                operation.Fail(error);
                Exception observed = null;
                try { await loading; }
                catch (Exception exception) { observed = exception; }
                await UniTask.NextFrame();
                Assert.That(observed, Is.SameAs(error));
                Assert.That(handle.IsValid(), Is.True);
                Assert.That(operation.DestroyCalls, Is.Zero);
            }
            finally { _service.Release(handle); }
            Assert.That(operation.DestroyCalls, Is.EqualTo(1));
            Assert.DoesNotThrow(() => _service.Release(handle));
        });

        [Test]
        public void ReleaseDuringLoad_PreservesExecutionUntilCompletion()
        {
            var operation = new PendingText();
            var handle = Addressables.ResourceManager.StartOperation(operation, default);
            _service.Release(handle);
            Assert.That(operation.DestroyCalls, Is.Zero);
            operation.Finish(Text("late result"));
            Assert.That(operation.DestroyCalls, Is.EqualTo(1));
            Assert.That(handle.IsValid(), Is.False);
            Assert.DoesNotThrow(() => _service.Release(default(AsyncOperationHandle<TextAsset>)));
        }

        [UnityTest]
        public IEnumerator AddressAndLabelLoads_UseBuiltContentAndRetainIndependentOwnership() => UniTask.ToCoroutine(async () =>
        {
            var first = _service.Load<TextAsset>("levels/level-01");
            var second = _service.Load<TextAsset>("levels/level-01");
            try
            {
                TextAsset text = await _service.AwaitAsync(first, CancellationToken.None);
                Assert.That(await _service.AwaitAsync(second, CancellationToken.None), Is.SameAs(text));
            }
            finally { _service.Release(first); }
            try
            {
                Assert.That(second.IsValid(), Is.True);
                Assert.That(await _service.AwaitAsync(second, CancellationToken.None), Is.Not.Null);
            }
            finally { _service.Release(second); }

            var configs = _service.LoadByLabel<ScriptableObject>(StaticDataProvider.ConfigLabel);
            try
            {
                IList<ScriptableObject> loaded = await _service.AwaitAsync(configs, CancellationToken.None);
                Assert.That(loaded.Count, Is.EqualTo(4));
                Assert.That(loaded, Has.Exactly(1).InstanceOf<GameplaySettings>());
                Assert.That(loaded, Has.Exactly(1).InstanceOf<LevelList>());
                Assert.That(loaded, Has.Exactly(1).InstanceOf<ViewCatalog>());
                Assert.That(loaded, Has.Exactly(1).InstanceOf<BackgroundCatalog>());
            }
            finally { _service.Release(configs); }
        });

        [UnityTest]
        public IEnumerator BackgroundCatalog_LoadsEveryCubemapFromBuiltContent() => UniTask.ToCoroutine(async () =>
        {
            using var data = new StaticDataProvider(_service);
            await data.LoadAsync(CancellationToken.None);
            BackgroundCatalog catalog = data.GetBackgroundCatalog();
            Assert.That(catalog.Presets.Count, Is.EqualTo(10));
            foreach (BackgroundPreset preset in catalog.Presets)
            {
                var handle = _service.Load<Cubemap>(preset.Address);
                try
                {
                    Cubemap cubemap = await _service.AwaitAsync(handle, CancellationToken.None);
                    Assert.That(cubemap, Is.Not.Null, preset.Name);
                    Assert.That(cubemap.mipmapCount, Is.GreaterThan(1), preset.Name);
                }
                finally
                {
                    _service.Release(handle);
                }
            }
        });

        [UnityTest]
        public IEnumerator GoalViewsFromBuiltContentKeepOccupancyPerInstance() => UniTask.ToCoroutine(async () =>
        {
            using var data = new StaticDataProvider(_service);
            await data.LoadAsync(CancellationToken.None);
            using var views = new ViewAssets(data, _service);
            using var world = new World();
            Entity goal = world.CreateEntity();
            goal.Set<FloorTag>();
            goal.Set<GoalTag>();
            goal.Set(new GridPosition(1, 1));
            Entity occupied = world.CreateEntity();
            occupied.Set<BoxTag>();
            occupied.Set(new GridPosition(1, 1));
            Entity empty = world.CreateEntity();
            empty.Set<BoxTag>();
            empty.Set(new GridPosition(2, 1));
            var root = new GameObject("Built goal views");
            _assets.Add(root);
            using var layer = new ViewLayer(new LevelRoot(root.transform), world, views, new ViewPipelineFactory(),
                new RunOptions(SkinId.Default, 60f, 0.16f));
            for (int frame = 0; frame < 300 && layer.Status == ViewStatus.Binding; frame++)
            {
                layer.Tick(0f);
                await UniTask.NextFrame();
            }

            Assert.That(layer.Status, Is.EqualTo(ViewStatus.Ready));
            var first = occupied.Get<BoxGoalFeedbackComponent>().Animator;
            var second = empty.Get<BoxGoalFeedbackComponent>().Animator;
            Transform firstMarker = first.transform.Find("Visual/OccupiedGoalMarker");
            Transform secondMarker = second.transform.Find("Visual/OccupiedGoalMarker");
            Assert.That(firstMarker.gameObject.activeSelf, Is.True);
            Assert.That(secondMarker.gameObject.activeSelf, Is.False);
            Assert.That(firstMarker.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null);
            Material shared = firstMarker.GetComponent<MeshRenderer>().sharedMaterial;
            Assert.That(shared.shader.isSupported, Is.True);
            Assert.That(secondMarker.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(shared));

            occupied.Set(new GridPosition(3, 1));
            empty.Set(new GridPosition(1, 1));
            layer.Tick(0.16f);
            Assert.That(firstMarker.gameObject.activeSelf, Is.False);
            Assert.That(secondMarker.gameObject.activeSelf, Is.True);
            Assert.That(first.IsPlaying || second.IsPlaying, Is.False);
            Assert.That(firstMarker.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(shared));
        });

        [UnityTest]
        public IEnumerator ArrowLoadsThroughViewAssetsFromBuiltContent() => UniTask.ToCoroutine(async () =>
        {
            using var data = new StaticDataProvider(_service);
            await data.LoadAsync(CancellationToken.None);
            using var views = new ViewAssets(data, _service);
            var key = new ViewKey(ViewKind.HintArrow, SkinId.Default);
            Assert.That(data.GetViewCatalog().Resolve(key).Address, Is.EqualTo("views/hint_arrow"));
            GameObject arrow = await views.LoadAsync(key, CancellationToken.None);
            Assert.That(await views.LoadAsync(key, CancellationToken.None), Is.SameAs(arrow));
            Assert.That(arrow.GetComponent<HintArrow>(), Is.Not.Null);
            Assert.That(arrow.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(arrow.GetComponentsInChildren<EntityView>(true), Is.Empty);
            Assert.That(arrow.GetComponentsInChildren<MeshRenderer>(true).Length, Is.EqualTo(2));
        });

        [UnityTest]
        public IEnumerator LevelSource_ParsesAndReleasesSuccessfulText() => UniTask.ToCoroutine(async () =>
        {
            var operation = new PendingText();
            var handle = Addressables.ResourceManager.StartOperation(operation, default);
            string requested = null;
            var service = new TestAddressablesService(load: (address, type) =>
            {
                requested = address;
                Assert.That(type, Is.EqualTo(typeof(TextAsset)));
                return handle;
            });
            var source = new AddressableLevelSource(new TestStaticData(), service);
            var loading = source.LoadAsync(1, CancellationToken.None);
            operation.Finish(Text("#####\n#@$.#\n#####"));
            Assert.That((await loading).Width, Is.EqualTo(5));
            Assert.That(requested, Is.EqualTo("levels/level-02"));
            Assert.That(service.ReleaseCalls, Is.EqualTo(1));
            Assert.That(operation.DestroyCalls, Is.EqualTo(1));
        });

        [UnityTest]
        public IEnumerator LevelSource_ParseFailureStillReleasesText() => UniTask.ToCoroutine(async () =>
        {
            var operation = new PendingText();
            var handle = Addressables.ResourceManager.StartOperation(operation, default);
            var service = new TestAddressablesService(load: (_, _) => handle);
            var source = new AddressableLevelSource(new TestStaticData(), service);
            var loading = source.LoadAsync(0, CancellationToken.None);
            operation.Finish(Text("invalid"));
            Exception error = null;
            try { await loading; }
            catch (Exception exception) { error = exception; }
            Assert.That(error, Is.TypeOf<LevelFormatException>());
            Assert.That(service.ReleaseCalls, Is.EqualTo(1));
            Assert.That(operation.DestroyCalls, Is.EqualTo(1));
        });

        [UnityTest]
        public IEnumerator LevelSource_CancellationReleasesAndIgnoresLateText() => UniTask.ToCoroutine(async () =>
        {
            var operation = new PendingText();
            var handle = Addressables.ResourceManager.StartOperation(operation, default);
            var service = new TestAddressablesService(load: (_, _) => handle);
            var source = new AddressableLevelSource(new TestStaticData(), service);
            using var cancellation = new CancellationTokenSource();
            var loading = source.LoadAsync(0, cancellation.Token);
            cancellation.Cancel();
            Assert.That((await loading.SuppressCancellationThrow()).IsCanceled, Is.True);
            Assert.That(service.ReleaseCalls, Is.EqualTo(1));
            Assert.That(operation.DestroyCalls, Is.Zero);
            operation.Finish(Text("invalid late text"));
            await UniTask.NextFrame();
            Assert.That(operation.DestroyCalls, Is.EqualTo(1));
            Assert.That(handle.IsValid(), Is.False);
        });

        private async UniTask<bool> AwaitCancelled(AsyncOperationHandle<TextAsset> handle, CancellationToken token)
        {
            try { await _service.AwaitAsync(handle, token); }
            catch (OperationCanceledException) { return true; }
            return false;
        }

        private TextAsset Text(string value)
        {
            var text = new TextAsset(value);
            _assets.Add(text);
            return text;
        }

        private sealed class PendingText : AsyncOperationBase<TextAsset>
        {
            public int DestroyCalls;
            protected override void Execute() { }
            public void Finish(TextAsset text) => Complete(text, true, (Exception)null);
            public void Fail(Exception error) => Complete(null, false, error);
            protected override void Destroy() => DestroyCalls++;
        }
    }
}
