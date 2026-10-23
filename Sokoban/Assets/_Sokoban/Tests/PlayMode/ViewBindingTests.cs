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
using Sokoban.Core.Components;
using Sokoban.Presentation;
using Sokoban.Presentation.Ecs.Adapters;
using Sokoban.Presentation.Ecs.Components;
using Sokoban.Presentation.Ecs.Model;
using Sokoban.Presentation.Ecs.Systems;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;

namespace Sokoban.Tests.PlayMode
{
    public sealed class ViewBindingTests
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

        [UnityTest]
        public IEnumerator PendingBind_StartsOnceAndUsesLatestPositionAndPrefabRotation()
        {
            using var world = new World();
            using var cancellation = new CancellationTokenSource();
            var provider = new PendingProvider();
            Transform root = CreateObject("Root").transform;
            root.position = new Vector3(20, 3, 40);
            root.rotation = Quaternion.Euler(0, 45, 0);
            GameObject prefab = CreateObject("Optional EntityView prefab");
            prefab.transform.rotation = Quaternion.Euler(0, 23, 0);
            Entity player = world.CreateEntity();
            player.Set<PlayerTag>();
            player.Set(new GridPosition(1, 2));
            Entity ignored = world.CreateEntity();
            ignored.Set(new GridPosition(4, 4));
            using var binding = new ViewBindingSystem(world, new ViewContext(root, provider, cancellation.Token));
            for (int i = 0; i < 5; i++) binding.Update(1f);
            Assert.That(provider.Calls, Is.EqualTo(1));
            Assert.That(player.Has<ViewPending>(), Is.True);
            Assert.That(ignored.Has<ViewPending>(), Is.False);
            player.Set(new GridPosition(5, 6));
            provider.Complete(prefab);
            yield return null;
            Assert.That(player.Has<ViewPending>(), Is.False);
            Assert.That(player.Has<ViewFailed>(), Is.False);
            Transform view = player.Get<TransformComponent>().Transform;
            Assert.That(view.parent, Is.SameAs(root));
            Assert.That(Vector3.Distance(view.position, new Vector3(5, 0, 6)), Is.LessThan(0.0001f));
            Assert.That(view.name, Is.EqualTo("Player (5, 6)"));
            Assert.That(Quaternion.Angle(view.rotation, prefab.transform.rotation), Is.LessThan(0.001f));
            binding.Update(1f);
            Assert.That(provider.Calls, Is.EqualTo(1));
            cancellation.Cancel();
        }

        [UnityTest]
        public IEnumerator ViewLayer_CancelsBeforeWorldDisposalAndIgnoresLateResult()
        {
            var world = new World();
            Entity entity = world.CreateEntity();
            entity.Set<BoxTag>();
            entity.Set(new GridPosition(2, 3));
            var provider = new PendingProvider();
            Transform root = CreateObject("Level root").transform;
            var layer = new ViewLayer(new LevelRoot(root), world, provider, new ViewFeatures());
            layer.Tick(0f);
            Assert.That(entity.Has<ViewPending>(), Is.True);
            layer.Dispose();
            Assert.That(provider.Token.IsCancellationRequested, Is.True);
            world.Dispose();
            provider.Complete(CreateObject("Late prefab"));
            yield return null;
            Assert.That(root.childCount, Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Binding_InstallsAdaptersBeforeSettingTheViewTransform()
        {
            using var world = new World();
            using var cancellation = new CancellationTokenSource();
            var provider = new PendingProvider();
            Transform root = CreateObject("Root").transform;
            GameObject prefab = CreateObject("Adapter prefab");
            var adapter = prefab.AddComponent<TransformAdapter>();
            typeof(ComponentAdapter<TransformComponent>).GetField("_value", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(adapter, default(TransformComponent));
            var entityView = prefab.AddComponent<EntityView>();
            typeof(EntityView).GetField("_adapters", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(entityView, new EntityAdapter[] { adapter });
            Entity entity = world.CreateEntity();
            entity.Set<FloorTag>();
            entity.Set<GoalTag>();
            entity.Set(new GridPosition(2, 4));
            using var binding = new ViewBindingSystem(world, new ViewContext(root, provider, cancellation.Token));
            binding.Update(0f);
            Assert.That(provider.Kind, Is.EqualTo(ViewKind.Goal));
            provider.Complete(prefab);
            yield return null;
            Assert.That(entity.Get<TransformComponent>().Transform, Is.SameAs(root.GetChild(0)));
            Assert.That(entity.Get<TransformComponent>().Transform.position, Is.EqualTo(new Vector3(2, 0, 4)));
            cancellation.Cancel();
        }

        [Test]
        public void KindMap_UsesTheSpecifiedTagPriority()
        {
            using var world = new World();
            Entity entity = world.CreateEntity();
            entity.Set<FloorTag>();
            Assert.That(ViewKeyMap.Get(entity).Kind, Is.EqualTo(ViewKind.Floor));
            entity.Set<GoalTag>();
            Assert.That(ViewKeyMap.Get(entity).Kind, Is.EqualTo(ViewKind.Goal));
            entity.Set<PlayerTag>();
            Assert.That(ViewKeyMap.Get(entity).Kind, Is.EqualTo(ViewKind.Player));
            entity.Set<BoxTag>();
            Assert.That(ViewKeyMap.Get(entity).Kind, Is.EqualTo(ViewKind.Box));
            entity.Set<WallTag>();
            Assert.That(ViewKeyMap.Get(entity).Kind, Is.EqualTo(ViewKind.Wall));
            var normal = new ViewKey(ViewKind.Wall, SkinId.Default);
            Assert.That(ViewKeyMap.Get(entity), Is.EqualTo(normal));
            Assert.That(ViewKeyMap.Get(entity).GetHashCode(), Is.EqualTo(normal.GetHashCode()));
            entity.Set(new ViewSkin(SkinId.Alternate));
            var alternate = new ViewKey(ViewKind.Wall, SkinId.Alternate);
            Assert.That(ViewKeyMap.Get(entity) == alternate, Is.True);
            Assert.That(alternate != normal, Is.True);
            Assert.That(alternate.ToString(), Is.EqualTo("Wall/Alternate"));
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator MissingSkin_ResolvesDefaultAndSharesOneCachedHandle() => UniTask.ToCoroutine(async () =>
        {
            var catalog = UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<ViewCatalog>("Assets/_Sokoban/Resources/LevelViewCatalog.asset"));
            typeof(ViewCatalog).GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(catalog, catalog.Entries.Select(entry => new ViewCatalog.KindEntry
                {
                    Kind = entry.Kind, Skins = entry.Skins.Where(skin => skin.Skin == SkinId.Default).ToArray()
                }).ToArray());
            using var assets = new ViewAssets(catalog);
            try
            {
                var alternate = new ViewKey(ViewKind.Player, SkinId.Alternate);
                var normal = new ViewKey(ViewKind.Player, SkinId.Default);
                Assert.That(catalog.Resolve(alternate).Skin, Is.EqualTo(SkinId.Default));
                UniTask<GameObject> first = assets.LoadAsync(alternate, CancellationToken.None);
                UniTask<GameObject> second = assets.LoadAsync(normal, CancellationToken.None);
                Assert.That(await first, Is.SameAs(await second));
                await UniTask.NextFrame();
                var handles = Handles(assets);
                Assert.That(handles.Count, Is.EqualTo(1));
                Assert.That(handles.ContainsKey(normal), Is.True);
                Assert.That(handles[normal].ReferenceCount, Is.EqualTo(1));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                UnityEngine.Object.Destroy(catalog);
            }
        });

        [Test]
        public void Catalog_ValidatesGroupedSkinsAndResolvesExactBeforeDefault()
        {
            var catalog = ScriptableObject.CreateInstance<ViewCatalog>();
            try
            {
                var entries = Enum.GetValues(typeof(ViewKind)).Cast<ViewKind>().Select(kind => new ViewCatalog.KindEntry
                {
                    Kind = kind, Skins = new[] { new ViewCatalog.SkinEntry
                    {
                        Skin = SkinId.Default, Address = $"views/{kind.ToString().ToLowerInvariant()}"
                    } }
                }).ToList();
                ViewCatalog.KindEntry player = entries.Single(entry => entry.Kind == ViewKind.Player);
                ViewCatalog.SkinEntry normal = player.Skins.Single();
                var alternate = new ViewCatalog.SkinEntry
                {
                    Skin = SkinId.Alternate, Address = "views/player_alt"
                };
                player.Skins = new[] { normal, alternate };
                FieldInfo field = typeof(ViewCatalog).GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic);
                field.SetValue(catalog, entries.ToArray());
                Assert.DoesNotThrow(catalog.Validate);
                Assert.That(catalog.Resolve(new ViewKey(ViewKind.Player, SkinId.Alternate)), Is.SameAs(alternate));
                var restored = ScriptableObject.CreateInstance<ViewCatalog>();
                try
                {
                    UnityEditor.EditorJsonUtility.FromJsonOverwrite(UnityEditor.EditorJsonUtility.ToJson(catalog), restored);
                    Assert.DoesNotThrow(restored.Validate);
                    Assert.That(restored.Entries.Count, Is.EqualTo(entries.Count));
                    Assert.That(restored.Entries.Single(entry => entry.Kind == ViewKind.Player).Skins.Length, Is.EqualTo(2));
                    Assert.That(restored.Resolve(new ViewKey(ViewKind.Player, SkinId.Alternate)).Address,
                        Is.EqualTo(alternate.Address));
                    Assert.That(restored.Resolve(new ViewKey(ViewKind.Player, SkinId.Default)).Address,
                        Is.EqualTo(normal.Address));
                    Assert.That(restored.Resolve(new ViewKey(ViewKind.Wall, SkinId.Alternate)).Skin, Is.EqualTo(SkinId.Default));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(restored);
                }
                player.Skins = new[] { normal, alternate, alternate };
                Assert.That(Assert.Throws<InvalidOperationException>(catalog.Validate).Message, Does.Contain("Player/Alternate: duplicated"));
                player.Skins = new[] { alternate };
                Assert.That(Assert.Throws<InvalidOperationException>(catalog.Validate).Message, Does.Contain("Player: missing Default"));
                player.Skins = new[] { normal, alternate };
                foreach (string invalid in new[] { null, "", " \t" })
                {
                    alternate.Address = invalid;
                    Assert.That(Assert.Throws<InvalidOperationException>(catalog.Validate).Message, Does.Contain("Player/Alternate: empty address"));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [UnityTest]
        public IEnumerator SynchronousCatalogFailure_LogsOnceWithoutCachingAHandle() => UniTask.ToCoroutine(async () =>
        {
            var catalog = ScriptableObject.CreateInstance<ViewCatalog>();
            using var assets = new ViewAssets(catalog);
            try
            {
                LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException"));
                bool failed = false;
                try
                {
                    await assets.LoadAsync(new ViewKey(ViewKind.Floor, SkinId.Default), CancellationToken.None);
                }
                catch (InvalidOperationException)
                {
                    failed = true;
                }
                Assert.That(failed, Is.True);
                Assert.That(Handles(assets), Is.Empty);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                UnityEngine.Object.Destroy(catalog);
            }
        });

        [UnityTest]
        public IEnumerator SharedFailure_LogsOnceForCompletionAndAllWaitingCallers() => UniTask.ToCoroutine(async () =>
        {
            var catalog = ScriptableObject.CreateInstance<ViewCatalog>();
            typeof(ViewCatalog).GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(catalog,
                new[] { new ViewCatalog.KindEntry { Kind = ViewKind.Floor, Skins = new[]
                {
                    new ViewCatalog.SkinEntry { Skin = SkinId.Default, Address = Guid.NewGuid().ToString("N") }
                } } });
            using var assets = new ViewAssets(catalog);
            var operation = new FailedOperation();
            AsyncOperationHandle<GameObject> handle = Addressables.ResourceManager.StartOperation(operation, default);
            Type entryType = typeof(ViewAssets).GetNestedType("CachedLoad", BindingFlags.NonPublic);
            object entry = Activator.CreateInstance(entryType, new object[] { handle });
            var cache = (IDictionary)typeof(ViewAssets).GetField("_handles", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(assets);
            cache.Add(new ViewKey(ViewKind.Floor, SkinId.Default), entry);
            handle.Completed += (Action<AsyncOperationHandle<GameObject>>)Delegate.CreateDelegate(
                typeof(Action<AsyncOperationHandle<GameObject>>), entry, "OnCompleted");
            try
            {
                UniTask<GameObject> first = assets.LoadAsync(new ViewKey(ViewKind.Floor, SkinId.Default), CancellationToken.None);
                UniTask<GameObject> second = assets.LoadAsync(new ViewKey(ViewKind.Floor, SkinId.Default), CancellationToken.None);
                LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: planned shared failure"));
                operation.Fail();
                int failures = 0;
                foreach (UniTask<GameObject> task in new[] { first, second })
                {
                    try
                    {
                        await task;
                    }
                    catch (InvalidOperationException)
                    {
                        failures++;
                    }
                }
                Assert.That(failures, Is.EqualTo(2));
                await UniTask.NextFrame(cancellationToken: CancellationToken.None);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                UnityEngine.Object.Destroy(catalog);
            }
        });

        [UnityTest]
        public IEnumerator SharedLoad_CallerCancellationDoesNotCancelOrReleaseTheHandle() => UniTask.ToCoroutine(async () =>
        {
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<ViewCatalog>("Assets/_Sokoban/Resources/LevelViewCatalog.asset");
            var assets = new ViewAssets(catalog);
            using var cancellation = new CancellationTokenSource();
            try
            {
                UniTask<GameObject> cancelled = assets.LoadAsync(new ViewKey(ViewKind.Floor, SkinId.Default), cancellation.Token);
                UniTask<GameObject> remaining = assets.LoadAsync(new ViewKey(ViewKind.Floor, SkinId.Default), CancellationToken.None);
                cancellation.Cancel();
                Assert.That(await cancelled.SuppressCancellationThrow(), Is.EqualTo((true, (GameObject)null)));
                GameObject prefab = await remaining;
                GameObject repeated = await assets.LoadAsync(new ViewKey(ViewKind.Floor, SkinId.Default), CancellationToken.None);
                await UniTask.NextFrame(cancellationToken: CancellationToken.None);
                Assert.That(prefab, Is.SameAs(repeated));
                var handles = Handles(assets);
                Assert.That(handles.Count, Is.EqualTo(1));
                Assert.That(handles[new ViewKey(ViewKind.Floor, SkinId.Default)].IsValid(), Is.True);
                Assert.That(handles[new ViewKey(ViewKind.Floor, SkinId.Default)].ReferenceCount, Is.EqualTo(1));
            }
            finally
            {
                assets.Dispose();
            }
        });

        [UnityTest]
        public IEnumerator FailedKind_LogsOnceAndRetriesAfterCatalogCorrection()
        {
            ViewCatalog catalog = UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<ViewCatalog>("Assets/_Sokoban/Resources/LevelViewCatalog.asset"));
            ViewCatalog.SkinEntry floorSkin = catalog.Entries.Single(entry => entry.Kind == ViewKind.Floor)
                .Skins.Single(skin => skin.Skin == SkinId.Default);
            string original = floorSkin.Address;
            floorSkin.Address = "missing/view";
            using var assets = new ViewAssets(catalog);
            try
            {
                LogAssert.Expect(LogType.Exception, new Regex("InvalidKeyException"));
                AsyncOperationHandle<GameObject> failedHandle = default;
                for (int run = 0; run < 2; run++)
                {
                    using var world = new World();
                    using var cancellation = new CancellationTokenSource();
                    var entities = new List<Entity>();
                    for (int x = 0; x < 1; x++)
                    {
                        Entity floor = world.CreateEntity();
                        floor.Set<FloorTag>();
                        floor.Set(new GridPosition(x, 1));
                        entities.Add(floor);
                    }
                    using var binding = new ViewBindingSystem(world, new ViewContext(CreateObject("Root").transform, assets, cancellation.Token));
                    binding.Update(0f);
                    float deadline = Time.realtimeSinceStartup + 5f;
                    while (entities.Any(entity => entity.Has<ViewPending>()) && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.That(entities.All(entity => !entity.Has<ViewPending>() &&
                        (run == 0 ? entity.Has<ViewFailed>() && !entity.Has<TransformComponent>() :
                        !entity.Has<ViewFailed>() && entity.Has<TransformComponent>())), Is.True);
                    binding.Update(0f);
                    Assert.That(Handles(assets).Count, Is.EqualTo(1));
                    if (run == 0)
                    {
                        failedHandle = Handles(assets)[new ViewKey(ViewKind.Floor, SkinId.Default)];
                        floorSkin.Address = original;
                    }
                    else
                    {
                        Assert.That(failedHandle.IsValid(), Is.False);
                    }
                    cancellation.Cancel();
                    yield return null;
                }
                yield return null;
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                UnityEngine.Object.Destroy(catalog);
            }
        }

        [UnityTest]
        public IEnumerator SameFrameFailure_IsSharedAndRetriedOnlyAfterFrameAdvances() => UniTask.ToCoroutine(async () =>
        {
            var catalog = UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<ViewCatalog>("Assets/_Sokoban/Resources/LevelViewCatalog.asset"));
            ViewCatalog.SkinEntry wall = catalog.Entries.Single(entry => entry.Kind == ViewKind.Wall)
                .Skins.Single(skin => skin.Skin == SkinId.Default);
            string original = wall.Address;
            wall.Address = "missing/view";
            using var assets = new ViewAssets(catalog);
            try
            {
                await assets.LoadAsync(new ViewKey(ViewKind.Floor, SkinId.Default), CancellationToken.None);
                var key = new ViewKey(ViewKind.Wall, SkinId.Default);
                int frame = Time.frameCount;
                Exception firstException = null;
                AsyncOperationHandle<GameObject> failed = default;
                LogAssert.Expect(LogType.Exception, new Regex("InvalidKeyException"));
                for (int call = 0; call < 4; call++)
                {
                    Exception failure = null;
                    try { await assets.LoadAsync(key, CancellationToken.None); }
                    catch (Exception exception) { failure = exception; }
                    Assert.That(failure, Is.TypeOf<InvalidKeyException>());
                    Assert.That(Time.frameCount, Is.EqualTo(frame), "The invalid-key loads must fail synchronously for this regression.");
                    if (call == 0)
                    {
                        firstException = failure;
                        failed = Handles(assets)[key];
                    }
                    else
                    {
                        Assert.That(failure, Is.SameAs(firstException));
                        Assert.That(Handles(assets)[key], Is.EqualTo(failed));
                        Assert.That(failed.IsValid(), Is.True);
                    }
                }
                wall.Address = original;
                await UniTask.NextFrame();
                Assert.That(await assets.LoadAsync(key, CancellationToken.None), Is.Not.Null);
                Assert.That(failed.IsValid(), Is.False);
                Assert.That(Handles(assets)[key].Status, Is.EqualTo(AsyncOperationStatus.Succeeded));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                UnityEngine.Object.Destroy(catalog);
            }
        });

        private static Dictionary<ViewKey, AsyncOperationHandle<GameObject>> Handles(ViewAssets assets)
        {
            var cache = (IDictionary)typeof(ViewAssets).GetField("_handles", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(assets);
            var handles = new Dictionary<ViewKey, AsyncOperationHandle<GameObject>>();
            foreach (DictionaryEntry entry in cache)
            {
                handles.Add((ViewKey)entry.Key, (AsyncOperationHandle<GameObject>)entry.Value.GetType().GetField("Handle").GetValue(entry.Value));
            }
            return handles;
        }

        private sealed class FailedOperation : AsyncOperationBase<GameObject>
        {
            protected override void Execute() { }
            public void Fail() => Complete(null, false, new InvalidOperationException("planned shared failure"));
        }
#endif

        private GameObject CreateObject(string name)
        {
            var target = new GameObject(name);
            _objects.Add(target);
            return target;
        }

        private sealed class PendingProvider : IViewProvider
        {
            private readonly UniTaskCompletionSource<GameObject> _pending = new();
            public int Calls { get; private set; }
            public ViewKind Kind { get; private set; }
            public CancellationToken Token { get; private set; }

            public UniTask<GameObject> LoadAsync(ViewKey key, CancellationToken ct)
            {
                Calls++;
                Kind = key.Kind;
                Token = ct;
                return _pending.Task;
            }

            public void Complete(GameObject prefab) => _pending.TrySetResult(prefab);
        }
    }
}
