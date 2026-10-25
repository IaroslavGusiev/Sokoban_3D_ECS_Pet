using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Reflection;
using Cysharp.Threading.Tasks;
using DefaultEcs;
using NUnit.Framework;
using Sokoban.Core.Components;
using Sokoban.Presentation;
using Sokoban.Presentation.Ecs.Components;
using Sokoban.Presentation.Ecs.Model;
using UnityEngine;
using UnityEngine.TestTools;

namespace Sokoban.Tests.PlayMode
{
    public sealed class ViewLayerFlowTests
    {
        private readonly List<ViewLayer> _layers = new();
        private readonly List<World> _worlds = new();
        private readonly List<GameObject> _objects = new();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (ViewLayer layer in _layers) layer.Dispose();
            foreach (World world in _worlds) world.Dispose();
            foreach (GameObject target in _objects)
            {
                if (target != null) UnityEngine.Object.Destroy(target);
            }
            _layers.Clear();
            _worlds.Clear();
            _objects.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ImmediateProvider_BindsEveryEntityAtItsWorldPosition() => UniTask.ToCoroutine(async () =>
        {
            var provider = new FakeViewProvider(_ => CreateObject("Immediate prefab"));
            var fixture = CreateLayer(provider);
            fixture.Layer.Tick(0f);
            Assert.That(fixture.Layer.Status, Is.EqualTo(ViewStatus.Ready));
            Assert.That(fixture.Root.childCount, Is.EqualTo(fixture.Entities.Length));
            foreach (Entity entity in fixture.Entities)
            {
                Assert.That(entity.Has<TransformComponent>(), Is.True);
                Assert.That(entity.Has<ViewPending>(), Is.False);
                GridPosition position = entity.Get<GridPosition>();
                Assert.That(entity.Get<TransformComponent>().Transform.position, Is.EqualTo(new Vector3(position.X, 0, position.Y)));
            }
            await UniTask.Yield();
        });

        [UnityTest]
        public IEnumerator HeldProvider_StaysBindingUntilEveryRequestCompletes() => UniTask.ToCoroutine(async () =>
        {
            GameObject prefab = CreateObject("Held prefab");
            var provider = new FakeViewProvider(_ => prefab) { Hold = true };
            var fixture = CreateLayer(provider);
            fixture.Layer.Tick(0f);
            Assert.That(fixture.Layer.Status, Is.EqualTo(ViewStatus.Binding));
            Assert.That(fixture.Root.childCount, Is.Zero);
            Assert.That(provider.Calls, Is.EqualTo(fixture.Entities.Length));
            for (int index = 0; index < provider.Calls - 1; index++) provider.Complete(index);
            await UniTask.Yield();
            Assert.That(fixture.Layer.Status, Is.EqualTo(ViewStatus.Binding));
            provider.Complete(provider.Calls - 1);
            await UniTask.Yield();
            Assert.That(fixture.Layer.Status, Is.EqualTo(ViewStatus.Ready));
            Assert.That(fixture.Root.childCount, Is.EqualTo(fixture.Entities.Length));
        });

        [UnityTest]
        public IEnumerator PendingRequests_AreNotStartedTwice() => UniTask.ToCoroutine(async () =>
        {
            var provider = new FakeViewProvider(_ => CreateObject("Pending prefab")) { Hold = true };
            var fixture = CreateLayer(provider);
            for (int index = 0; index < 20; index++) fixture.Layer.Tick(0f);
            Assert.That(provider.Calls, Is.EqualTo(fixture.Entities.Length));
            foreach (Entity entity in fixture.Entities) Assert.That(entity.Has<ViewPending>(), Is.True);
            await UniTask.Yield();
        });

        [UnityTest]
        public IEnumerator ProviderFailure_MarksEntitiesAndDoesNotRetry() => UniTask.ToCoroutine(async () =>
        {
            var provider = new FakeViewProvider(_ => null) { Failure = new InvalidOperationException("planned fake failure") };
            var fixture = CreateLayer(provider);
            fixture.Layer.Tick(0f);
            Assert.That(fixture.Layer.Status, Is.EqualTo(ViewStatus.Failed));
            foreach (Entity entity in fixture.Entities)
            {
                Assert.That(entity.Has<ViewFailed>(), Is.True);
                Assert.That(entity.Has<ViewPending>(), Is.False);
                Assert.That(entity.Has<TransformComponent>(), Is.False);
            }
            for (int index = 0; index < 20; index++) fixture.Layer.Tick(0f);
            Assert.That(provider.Calls, Is.EqualTo(fixture.Entities.Length));
            await UniTask.Yield();
        });

        [UnityTest]
        public IEnumerator DisposedLayer_IgnoresLateProviderResults() => UniTask.ToCoroutine(async () =>
        {
            GameObject prefab = CreateObject("Late prefab");
            var provider = new FakeViewProvider(_ => prefab) { Hold = true };
            var fixture = CreateLayer(provider);
            fixture.Layer.Tick(0f);
            Transform parent = fixture.Root.parent;
            fixture.Layer.Dispose();
            _layers.Remove(fixture.Layer);
            for (int index = 0; index < provider.Calls; index++) provider.Complete(index);
            await UniTask.NextFrame();
            Assert.That(parent.childCount, Is.Zero);
            foreach (Entity entity in fixture.Entities) Assert.That(entity.Has<TransformComponent>(), Is.False);
            LogAssert.NoUnexpectedReceived();
        });

        [UnityTest]
        public IEnumerator NullPrefab_MarksFailureAndLogsOnce() => UniTask.ToCoroutine(async () =>
        {
            var provider = new FakeViewProvider(_ => null);
            var fixture = CreateLayer(provider, false);
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("NullReferenceException"));
            fixture.Layer.Tick(0f);
            await UniTask.Yield();
            Assert.That(fixture.Layer.Status, Is.EqualTo(ViewStatus.Failed));
            Assert.That(fixture.Entities[0].Has<ViewFailed>(), Is.True);
            Assert.That(fixture.Entities[0].Has<ViewPending>(), Is.False);
            Assert.That(fixture.Entities[0].Has<TransformComponent>(), Is.False);
            fixture.Layer.Tick(0f);
            Assert.That(provider.Calls, Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        });

        private (ViewLayer Layer, Entity[] Entities, Transform Root) CreateLayer(FakeViewProvider provider, bool includeWalls = true)
        {
            var world = new World();
            _worlds.Add(world);
            var entities = new List<Entity>();
            Entity player = world.CreateEntity();
            player.Set<PlayerTag>();
            player.Set(new GridPosition(2, 3));
            entities.Add(player);
            if (includeWalls)
            {
                for (int index = 0; index < 3; index++)
                {
                    Entity wall = world.CreateEntity();
                    wall.Set<WallTag>();
                    wall.Set(new GridPosition(index, 0));
                    entities.Add(wall);
                }
            }
            GameObject parent = CreateObject("Flow root");
            parent.transform.position = new Vector3(10, 4, -5);
            var layer = new ViewLayer(new LevelRoot(parent.transform), world, provider, new ViewFeatures(), new RunOptions(SkinId.Default, 60f, 0.16f));
            _layers.Add(layer);
            return (layer, entities.ToArray(), parent.transform.GetChild(0));
        }

        [UnityTest]
        public IEnumerator BoundSkinSwitch_UninstallsOldAdaptersAndReplacesExactlyOneView() => UniTask.ToCoroutine(async () =>
        {
            GameObject normal = CreateSkinPrefab("Default skin");
            var adapter = normal.AddComponent<TestSkinAdapter>();
            typeof(ComponentAdapter<TestSkinMarker>).GetField("Value", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(adapter, new TestSkinMarker { Value = 7 });
            var view = normal.AddComponent<EntityView>();
            typeof(EntityView).GetField("adapters", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(view, new List<EntityAdapter> { adapter });
            GameObject alternate = CreateSkinPrefab("Alternate skin");
            var provider = new FakeViewProvider(key => key.Skin == SkinId.Default ? normal : alternate);
            var fixture = CreateLayer(provider, false);
            fixture.Layer.Tick(0f);
            Entity player = fixture.Entities[0];
            GameObject previous = player.Get<TransformComponent>().Transform.gameObject;
            Assert.That(player.Get<TestSkinMarker>().Value, Is.EqualTo(7));
            fixture.Layer.SetPlayerSkin(SkinId.Alternate);
            fixture.Layer.Tick(0f);
            Assert.That(player.Has<TestSkinMarker>(), Is.False);
            Assert.That(player.Has<SkinChangeRequest>(), Is.False);
            await UniTask.NextFrame();
            Assert.That(previous == null, Is.True);
            Assert.That(fixture.Layer.Status, Is.EqualTo(ViewStatus.Ready));
            Assert.That(fixture.Root.childCount, Is.EqualTo(1));
            Assert.That(player.Get<TransformComponent>().Transform.Find(alternate.name), Is.Not.Null);
            Assert.That(provider.Calls, Is.EqualTo(2));
        });

        [UnityTest]
        public IEnumerator PendingSkinSwitch_DropsStalePrefabBeforeBindingLatestKey() => UniTask.ToCoroutine(async () =>
        {
            GameObject normal = CreateSkinPrefab("Default skin");
            GameObject alternate = CreateSkinPrefab("Alternate skin");
            var provider = new FakeViewProvider(key => key.Skin == SkinId.Default ? normal : alternate) { Hold = true };
            var fixture = CreateLayer(provider, false);
            Entity player = fixture.Entities[0];
            fixture.Layer.Tick(0f);
            fixture.Layer.SetPlayerSkin(SkinId.Alternate);
            fixture.Layer.Tick(0f);
            Assert.That(provider.Calls, Is.EqualTo(1));
            Assert.That(player.Has<ViewPending>(), Is.True);
            provider.Complete(0);
            await UniTask.NextFrame();
            Assert.That(fixture.Root.childCount, Is.Zero);
            Assert.That(player.Has<TransformComponent>(), Is.False);
            Assert.That(player.Has<ViewPending>(), Is.False);
            provider.Hold = false;
            fixture.Layer.Tick(0f);
            Assert.That(provider.KeyAt(1), Is.EqualTo(new ViewKey(ViewKind.Player, SkinId.Alternate)));
            Assert.That(fixture.Layer.Status, Is.EqualTo(ViewStatus.Ready));
            Assert.That(fixture.Root.childCount, Is.EqualTo(1));
            Assert.That(player.Get<TransformComponent>().Transform.Find(alternate.name), Is.Not.Null);
        });

        [UnityTest]
        public IEnumerator PendingSkinSwitch_DropsStaleFailureAndRequestsLatestKey() => UniTask.ToCoroutine(async () =>
        {
            GameObject alternate = CreateSkinPrefab("Alternate skin");
            var provider = new FakeViewProvider(_ => alternate) { Hold = true };
            var fixture = CreateLayer(provider, false);
            Entity player = fixture.Entities[0];
            fixture.Layer.Tick(0f);
            fixture.Layer.SetPlayerSkin(SkinId.Alternate);
            fixture.Layer.Tick(0f);
            provider.Fail(0);
            await UniTask.NextFrame();
            Assert.That(player.Has<ViewFailed>(), Is.False);
            Assert.That(player.Has<ViewPending>(), Is.False);
            provider.Hold = false;
            fixture.Layer.Tick(0f);
            Assert.That(fixture.Layer.Status, Is.EqualTo(ViewStatus.Ready));
            Assert.That(provider.KeyAt(1).Skin, Is.EqualTo(SkinId.Alternate));
            Assert.That(fixture.Root.childCount, Is.EqualTo(1));
            Assert.That(player.Get<TransformComponent>().Transform.Find(alternate.name), Is.Not.Null);
            LogAssert.NoUnexpectedReceived();
        });

        [UnityTest]
        public IEnumerator HeldFailure_ForUnchangedKeyMarksFailedWithoutRetry() => UniTask.ToCoroutine(async () =>
        {
            var provider = new FakeViewProvider(_ => null) { Hold = true };
            var fixture = CreateLayer(provider, false);
            Entity player = fixture.Entities[0];
            fixture.Layer.Tick(0f);
            provider.Fail(0);
            await UniTask.NextFrame();
            Assert.That(player.Has<ViewFailed>(), Is.True);
            Assert.That(player.Has<ViewPending>(), Is.False);
            Assert.That(player.Has<TransformComponent>(), Is.False);
            Assert.That(fixture.Layer.Status, Is.EqualTo(ViewStatus.Failed));
            fixture.Layer.Tick(0f);
            Assert.That(provider.Calls, Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        });

        [UnityTest]
        public IEnumerator FailedSkin_CanBeClearedByAnotherSkinRequest() => UniTask.ToCoroutine(async () =>
        {
            GameObject normal = CreateSkinPrefab("Default skin");
            var provider = new FakeViewProvider(_ => normal);
            var fixture = CreateLayer(provider, false);
            Entity player = fixture.Entities[0];
            fixture.Layer.Tick(0f);
            provider.Failure = new InvalidOperationException("planned skin failure");
            fixture.Layer.SetPlayerSkin(SkinId.Alternate);
            fixture.Layer.Tick(0f);
            Assert.That(fixture.Layer.Status, Is.EqualTo(ViewStatus.Failed));
            Assert.That(player.Has<TransformComponent>(), Is.False);
            provider.Failure = null;
            fixture.Layer.SetPlayerSkin(SkinId.Default);
            fixture.Layer.Tick(0f);
            await UniTask.NextFrame();
            Assert.That(player.Has<ViewFailed>(), Is.False);
            Assert.That(player.Get<ViewSkin>().Id, Is.EqualTo(SkinId.Default));
            Assert.That(fixture.Layer.Status, Is.EqualTo(ViewStatus.Ready));
            Assert.That(fixture.Root.childCount, Is.EqualTo(1));
        });

        [UnityTest]
        public IEnumerator SkinSwitch_DoesNotChangeSimulationPositionsOrCompletion() => UniTask.ToCoroutine(async () =>
        {
            GameObject prefab = CreateSkinPrefab("Skin");
            var level = Sokoban.Core.LevelParser.Parse("#####\n#@$.#\n#####");
            using var simulation = new Sokoban.Core.Simulation(level, new Sokoban.Core.LevelPopulator(), 60f);
            using var layer = new ViewLayer(new LevelRoot(CreateObject("Simulation root").transform), simulation.World,
                new FakeViewProvider(_ => prefab), new ViewFeatures(), new RunOptions(SkinId.Default, 60f, 0.16f));
            var before = new Dictionary<Entity, GridPosition>();
            foreach (Entity entity in simulation.World) before.Add(entity, entity.Get<GridPosition>());
            layer.Tick(0f);
            layer.SetPlayerSkin(SkinId.Alternate);
            layer.Tick(0f);
            await UniTask.NextFrame();
            foreach (Entity entity in simulation.World) Assert.That(entity.Get<GridPosition>(), Is.EqualTo(before[entity]));
            Assert.That(simulation.IsCompleted, Is.False);
        });

        private GameObject CreateSkinPrefab(string name)
        {
            GameObject prefab = CreateObject(name);
            CreateObject(name).transform.SetParent(prefab.transform, false);
            return prefab;
        }

        private GameObject CreateObject(string name)
        {
            var target = new GameObject(name);
            _objects.Add(target);
            return target;
        }

        private sealed class FakeViewProvider : IViewProvider
        {
            private readonly Func<ViewKey, GameObject> _prefab;
            private readonly List<(ViewKey Key, UniTaskCompletionSource<GameObject> Completion)> _requests = new();
            public bool Hold { get; set; }
            public Exception Failure { get; set; }
            public int Calls => _requests.Count;

            public FakeViewProvider(Func<ViewKey, GameObject> prefab) => _prefab = prefab;

            public UniTask<GameObject> LoadAsync(ViewKey key, CancellationToken ct)
            {
                var completion = new UniTaskCompletionSource<GameObject>();
                _requests.Add((key, completion));
                if (Failure != null) completion.TrySetException(Failure);
                else if (!Hold) completion.TrySetResult(_prefab(key));
                return completion.Task.AttachExternalCancellation(ct);
            }

            public void Complete(int index)
            {
                var request = _requests[index];
                request.Completion.TrySetResult(_prefab(request.Key));
            }

            public ViewKey KeyAt(int index) => _requests[index].Key;
            public void Fail(int index) => _requests[index].Completion.TrySetException(new InvalidOperationException("planned stale failure"));
        }
    }
}
