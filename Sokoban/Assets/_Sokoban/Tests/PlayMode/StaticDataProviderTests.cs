using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Sokoban.Presentation;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;

namespace Sokoban.Tests.PlayMode
{
    public sealed class StaticDataProviderTests
    {
        private readonly List<ScriptableObject> _assets = new();

        [TearDown]
        public void TearDown()
        {
            foreach (ScriptableObject asset in _assets) UnityEngine.Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [UnityTest]
        public IEnumerator ConcurrentLoads_RetainOneBatchUntilOwnerDisposes() => UniTask.ToCoroutine(async () =>
        {
            var operation = new TestOperation<IList<ScriptableObject>>();
            var handle = Addressables.ResourceManager.StartOperation(operation, default);
            int loads = 0;
            var service = new TestAddressablesService(loadLabel: (label, type) =>
            {
                Assert.That(label, Is.EqualTo(StaticDataProvider.ConfigLabel));
                Assert.That(type, Is.EqualTo(typeof(ScriptableObject)));
                loads++;
                return handle;
            });
            var provider = new StaticDataProvider(service);
            try
            {
                UniTask first = provider.LoadAsync(CancellationToken.None);
                UniTask second = provider.LoadAsync(CancellationToken.None);
                IList<ScriptableObject> configs = Configs();
                operation.Finish(configs);
                await first;
                await second;
                await provider.LoadAsync(CancellationToken.None);
                Assert.That(loads, Is.EqualTo(1));
                Assert.That(service.ReleaseCalls, Is.Zero);
                Assert.That(provider.GetGameplaySettings(), Is.SameAs(configs[0]));
                Assert.That(provider.GetViewCatalog(), Is.SameAs(configs[2]));
                Assert.That(provider.GetBackgroundCatalog(), Is.SameAs(configs[3]));
                Assert.That(provider.LevelCount, Is.EqualTo(1));
                Assert.That(provider.GetLevelConfig(0).Address, Is.EqualTo("levels/test"));
            }
            finally { provider.Dispose(); }
            Assert.That(service.ReleaseCalls, Is.EqualTo(1));
            Assert.That(operation.DestroyCalls, Is.EqualTo(1));
        });

        [UnityTest]
        public IEnumerator LaterWaiterCancellation_DoesNotCancelSharedAttempt() => UniTask.ToCoroutine(async () =>
        {
            var operation = new TestOperation<IList<ScriptableObject>>();
            var handle = Addressables.ResourceManager.StartOperation(operation, default);
            var service = new TestAddressablesService(loadLabel: (_, _) => handle);
            using var provider = new StaticDataProvider(service);
            using var cancellation = new CancellationTokenSource();
            UniTask owner = provider.LoadAsync(CancellationToken.None);
            UniTask waiter = provider.LoadAsync(cancellation.Token);
            cancellation.Cancel();
            Assert.That(await waiter.SuppressCancellationThrow(), Is.True);
            Assert.That(service.ReleaseCalls, Is.Zero);
            operation.Finish(Configs());
            await owner;
            Assert.That(provider.LevelCount, Is.EqualTo(1));
        });

        [UnityTest]
        public IEnumerator InitiatingCancellation_ReleasesOnceAndLateResultCannotReplaceRetry() => UniTask.ToCoroutine(async () =>
        {
            var oldOperation = new TestOperation<IList<ScriptableObject>>();
            var oldHandle = Addressables.ResourceManager.StartOperation(oldOperation, default);
            var nextOperation = new TestOperation<IList<ScriptableObject>>();
            var nextHandle = Addressables.ResourceManager.StartOperation(nextOperation, default);
            int loads = 0;
            var service = new TestAddressablesService(loadLabel: (_, _) => ++loads == 1 ? oldHandle : nextHandle);
            using var provider = new StaticDataProvider(service);
            using var cancellation = new CancellationTokenSource();
            UniTask first = provider.LoadAsync(cancellation.Token);
            UniTask waiter = provider.LoadAsync(CancellationToken.None);
            cancellation.Cancel();
            Assert.That(await first.SuppressCancellationThrow(), Is.True);
            Assert.That(await waiter.SuppressCancellationThrow(), Is.True);
            Assert.That(service.ReleaseCalls, Is.EqualTo(1));
            UniTask retry = provider.LoadAsync(CancellationToken.None);
            IList<ScriptableObject> next = Configs();
            nextOperation.Finish(next);
            await retry;
            oldOperation.Finish(Configs());
            await UniTask.NextFrame();
            Assert.That(provider.GetGameplaySettings(), Is.SameAs(next[0]));
            Assert.That(loads, Is.EqualTo(2));
            Assert.That(service.ReleaseCalls, Is.EqualTo(1));
            Assert.That(oldOperation.DestroyCalls, Is.EqualTo(1));
            Assert.That(nextHandle.IsValid(), Is.True);
        });

        [UnityTest]
        public IEnumerator DisposalDuringLoad_CancelsWaitersAndReleasesLateCompletionOnce() => UniTask.ToCoroutine(async () =>
        {
            var operation = new TestOperation<IList<ScriptableObject>>();
            var handle = Addressables.ResourceManager.StartOperation(operation, default);
            var service = new TestAddressablesService(loadLabel: (_, _) => handle);
            var provider = new StaticDataProvider(service);
            UniTask loading = provider.LoadAsync(CancellationToken.None);
            provider.Dispose();
            Assert.That(await loading.SuppressCancellationThrow(), Is.True);
            Assert.That(service.ReleaseCalls, Is.EqualTo(1));
            operation.Finish(Configs());
            await UniTask.NextFrame();
            Assert.That(operation.DestroyCalls, Is.EqualTo(1));
            Assert.That(service.ReleaseCalls, Is.EqualTo(1));
            Assert.Throws<NullReferenceException>(() => provider.GetGameplaySettings());
        });

        [UnityTest]
        public IEnumerator FailedOperation_ReleasesAndAllowsRetry() => UniTask.ToCoroutine(async () =>
        {
            var operation = new TestOperation<IList<ScriptableObject>>();
            var handle = Addressables.ResourceManager.StartOperation(operation, default);
            int loads = 0;
            var service = new TestAddressablesService(loadLabel: (_, _) =>
                ++loads == 1 ? handle : Addressables.ResourceManager.CreateCompletedOperation(Configs(), null));
            using var provider = new StaticDataProvider(service);
            UniTask loading = provider.LoadAsync(CancellationToken.None);
            var failure = new InvalidOperationException("planned config failure");
            operation.Fail(failure);
            Exception observed = null;
            try { await loading; }
            catch (Exception exception) { observed = exception; }
            Assert.That(observed, Is.SameAs(failure));
            Assert.That(service.ReleaseCalls, Is.EqualTo(1));
            await provider.LoadAsync(CancellationToken.None);
            Assert.That(provider.LevelCount, Is.EqualTo(1));
            Assert.That(loads, Is.EqualTo(2));
        });

        [TestCase(0, false)]
        [TestCase(1, false)]
        [TestCase(2, false)]
        [TestCase(3, false)]
        [TestCase(0, true)]
        [TestCase(1, true)]
        [TestCase(2, true)]
        [TestCase(3, true)]
        public void MissingOrDuplicateConfig_ReleasesBatchAndAllowsRetry(int index, bool duplicate)
        {
            IList<ScriptableObject> invalid = Configs();
            if (duplicate) invalid.Add(invalid[index]);
            else invalid.RemoveAt(index);
            int loads = 0;
            var service = new TestAddressablesService(loadLabel: (_, _) =>
                Addressables.ResourceManager.CreateCompletedOperation(++loads == 1 ? invalid : Configs(), null));
            using var provider = new StaticDataProvider(service);
            Assert.Throws<InvalidOperationException>(() => provider.LoadAsync(CancellationToken.None).GetAwaiter().GetResult());
            Assert.That(service.ReleaseCalls, Is.EqualTo(1));
            provider.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(provider.LevelCount, Is.EqualTo(1));
            Assert.That(loads, Is.EqualTo(2));
        }

        [Test]
        public void AlreadyCancelledCaller_DoesNotStartLoading()
        {
            int loads = 0;
            var service = new TestAddressablesService(loadLabel: (_, _) =>
            {
                loads++;
                return default;
            });
            using var provider = new StaticDataProvider(service);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() => provider.LoadAsync(cancellation.Token));
            Assert.That(loads, Is.Zero);
            Assert.That(service.ReleaseCalls, Is.Zero);
        }

        private IList<ScriptableObject> Configs()
        {
            var settings = ScriptableObject.CreateInstance<GameplaySettings>();
            var levels = ScriptableObject.CreateInstance<LevelList>();
            var views = ScriptableObject.CreateInstance<ViewCatalog>();
            var backgrounds = ScriptableObject.CreateInstance<BackgroundCatalog>();
            JsonUtility.FromJsonOverwrite("{\"<Levels>k__BackingField\":[{\"address\":\"levels/test\"}]}", levels);
            _assets.AddRange(new ScriptableObject[] { settings, levels, views, backgrounds });
            return new List<ScriptableObject> { settings, levels, views, backgrounds };
        }
    }
}
