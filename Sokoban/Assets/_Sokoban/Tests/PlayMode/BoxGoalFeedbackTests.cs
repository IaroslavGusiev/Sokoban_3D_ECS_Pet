#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using DefaultEcs;
using DefaultEcs.System;
using NUnit.Framework;
using PrimeTween;
using Sokoban.Core;
using Sokoban.Core.Commands;
using Sokoban.Core.Components;
using Sokoban.Core.Messages;
using Sokoban.Presentation;
using Sokoban.Presentation.Ecs.Adapters;
using Sokoban.Presentation.Ecs.Components;
using Sokoban.Presentation.Ecs.Model;
using Sokoban.Presentation.Ecs.Systems;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Sokoban.Tests.PlayMode
{
    public sealed class BoxGoalFeedbackTests
    {
        private readonly List<GameObject> _objects = new();
        private float _timeScale;
        private static BoxGoalFeedbackOptions Options => new(0.22f, 0.12f, 0.12f);

        [SetUp]
        public void SetUp() => _timeScale = Time.timeScale;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = _timeScale;
            foreach (GameObject target in _objects)
            {
                if (target != null) Object.Destroy(target);
            }
            _objects.Clear();
            yield return null;
        }

        [Test]
        public void Message_IsBufferedUntilMovementFinishesAndVisualRestoresItsAuthoredPose()
        {
            using var world = new World();
            Entity box = Box(world);
            BoxGoalAnimator animator = Bind(box, out Transform visual);
            Vector3 baseline = visual.localPosition;
            Vector3 scale = visual.localScale;
            Vector3 rootPosition = visual.parent.position;
            using var pipeline = Pipeline(world);
            box.Set(new ViewMove { Duration = 0.16f });
            world.Publish(new BoxEnteredGoal(box, box.Get<GridPosition>()));
            Assert.That(animator.IsPlaying, Is.False);
            pipeline.Update(0.1f);
            Assert.That(visual.localPosition, Is.EqualTo(baseline));
            box.Remove<ViewMove>();
            pipeline.Update(0f);
            Assert.That(Tween.GetTweensCount(visual), Is.GreaterThan(0));
            pipeline.Update(0.055f);
            Assert.That(visual.localPosition.y, Is.GreaterThan(baseline.y));
            Assert.That(visual.localScale, Is.Not.EqualTo(scale));
            Assert.That(visual.parent.position, Is.EqualTo(rootPosition));
            pipeline.Update(1f);
            AssertRestored(animator, visual, baseline, scale);
            Assert.That(box.Has<BoxGoalFeedbackState>(), Is.False);
        }

        [Test]
        public void DelayedViewBinding_RetainsTheArrivalUntilTheVisualExists()
        {
            using var world = new World();
            Entity box = Box(world);
            using var pipeline = Pipeline(world);
            world.Publish(new BoxEnteredGoal(box, box.Get<GridPosition>()));
            pipeline.Update(0f);
            BoxGoalAnimator animator = Bind(box, out Transform visual);
            float baseline = visual.localPosition.y;
            box.Set<ViewPending>();
            pipeline.Update(0.1f);
            Assert.That(animator.IsPlaying, Is.False);
            box.Remove<ViewPending>();
            pipeline.Update(0f);
            pipeline.Update(0.1f);
            Assert.That(visual.localPosition.y, Is.GreaterThan(baseline));
        }

        [Test]
        public void LeavingBeforeVisualArrival_DiscardsThePendingEffect()
        {
            using var world = new World();
            Entity box = Box(world);
            BoxGoalAnimator animator = Bind(box, out Transform visual);
            Vector3 baseline = visual.localPosition;
            using var pipeline = Pipeline(world);
            GridPosition goal = box.Get<GridPosition>();
            box.Set(new ViewMove { Duration = 0.16f });
            world.Publish(new BoxEnteredGoal(box, goal));
            pipeline.Update(0f);
            box.Set(new GridPosition(4, 1));
            pipeline.Update(0f);
            box.Remove<ViewMove>();
            box.Set(goal);
            pipeline.Update(0.1f);
            Assert.That(visual.localPosition, Is.EqualTo(baseline));
            Assert.That(animator.IsPlaying, Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void MovementOrLeavingGoal_InterruptsAndRestoresAnActiveEffect(bool moving)
        {
            using var world = new World();
            Entity box = Box(world);
            BoxGoalAnimator animator = Bind(box, out Transform visual);
            Vector3 baseline = visual.localPosition;
            Vector3 scale = visual.localScale;
            using var pipeline = Pipeline(world);
            world.Publish(new BoxEnteredGoal(box, box.Get<GridPosition>()));
            pipeline.Update(0f);
            pipeline.Update(0.05f);
            if (moving) box.Set(new ViewMove { Duration = 0.16f });
            else box.Set(new GridPosition(4, 1));
            pipeline.Update(0.01f);
            AssertRestored(animator, visual, baseline, scale);
            Assert.That(box.Has<BoxGoalFeedbackState>(), Is.False);
        }

        [Test]
        public void RepeatedPlayback_DoesNotAccumulateOffsetsAndStopsExactlyAtBaseline()
        {
            using var world = new World();
            BoxGoalAnimator animator = Bind(Box(world), out Transform visual);
            Vector3 baseline = visual.localPosition;
            Vector3 scale = visual.localScale;
            for (int index = 0; index < 8; index++)
            {
                animator.PlayGoalFeedback(Options);
                animator.Tick(0.055f);
                Assert.That(visual.localPosition.y - baseline.y, Is.InRange(0f, Options.Height + 0.001f));
                Assert.That(visual.localScale.x, Is.GreaterThan(scale.x));
            }
            animator.Tick(Options.DurationSeconds);
            AssertRestored(animator, visual, baseline, scale);
            animator.StopAndRestore();
            animator.StopAndRestore();
            AssertRestored(animator, visual, baseline, scale);
        }

        [Test]
        public void AdapterUninstall_StopsImmediatelyAndRepeatedCleanupIsSafe()
        {
            using var world = new World();
            Entity box = Box(world);
            BoxGoalAnimator animator = Bind(box, out Transform visual);
            Vector3 baseline = visual.localPosition;
            Vector3 scale = visual.localScale;
            using var pipeline = Pipeline(world);
            world.Publish(new BoxEnteredGoal(box, box.Get<GridPosition>()));
            pipeline.Update(0f);
            pipeline.Update(0.05f);
            var adapter = animator.GetComponent<BoxGoalFeedbackAdapter>();
            adapter.Uninstall(box);
            Assert.That(box.Has<BoxGoalFeedbackComponent>() || box.Has<BoxGoalFeedbackState>(), Is.False);
            AssertRestored(animator, visual, baseline, scale);
            adapter.Uninstall(box);
            animator.StopAndRestore();
            AssertRestored(animator, visual, baseline, scale);
        }

        [Test]
        public void Disposal_RestoresVisualAndDoesNotReplayPendingMessagesIntoReplacementSystem()
        {
            using var world = new World();
            Entity box = Box(world);
            BoxGoalAnimator animator = Bind(box, out Transform visual);
            Vector3 baseline = visual.localPosition;
            Vector3 scale = visual.localScale;
            var pipeline = Pipeline(world);
            world.Publish(new BoxEnteredGoal(box, box.Get<GridPosition>()));
            pipeline.Update(0f);
            pipeline.Update(0.05f);
            world.Publish(new BoxEnteredGoal(box, box.Get<GridPosition>()));
            pipeline.Dispose();
            AssertRestored(animator, visual, baseline, scale);
            using var replacement = Pipeline(world);
            replacement.Update(0.1f);
            Assert.That(animator.IsPlaying, Is.False);
            world.Publish(new BoxEnteredGoal(box, box.Get<GridPosition>()));
            replacement.Update(0f);
            replacement.Update(0.1f);
            Assert.That(visual.localPosition.y, Is.GreaterThan(baseline.y));
        }

        [Test]
        public void DestroyedForeignAndStaleEntityMessages_AreDiscarded()
        {
            using var world = new World();
            using var otherWorld = new World();
            Entity dead = Box(world);
            Entity foreign = Box(otherWorld);
            Entity current = Box(world);
            BoxGoalAnimator animator = Bind(current, out Transform visual);
            using var pipeline = Pipeline(world);
            world.Publish(new BoxEnteredGoal(dead, dead.Get<GridPosition>()));
            world.Publish(new BoxEnteredGoal(foreign, foreign.Get<GridPosition>()));
            world.Publish(new BoxEnteredGoal(current, new GridPosition(99, 99)));
            dead.Dispose();
            Assert.DoesNotThrow(() => pipeline.Update(0f));
            Assert.That(animator.IsPlaying, Is.False);
            Assert.That(Tween.GetTweensCount(visual), Is.Zero);
        }

        [Test]
        public void CoalescedAdjacentGoalArrivals_PlayAtTheLatestValidGoal()
        {
            using var world = new World();
            Entity box = Box(world);
            BoxGoalAnimator animator = Bind(box, out Transform visual);
            using var pipeline = Pipeline(world);
            world.Publish(new BoxEnteredGoal(box, box.Get<GridPosition>()));
            box.Set(new GridPosition(4, 1));
            world.Publish(new BoxEnteredGoal(box, new GridPosition(4, 1)));
            world.Publish(new BoxEnteredGoal(box, new GridPosition(4, 1)));
            pipeline.Update(0f);
            Assert.That(animator.IsPlaying, Is.True);
            Assert.That(box.Get<BoxGoalFeedbackState>().Goal, Is.EqualTo(new GridPosition(4, 1)));
            pipeline.Update(0.055f);
            Assert.That(visual.localPosition.y, Is.GreaterThan(0.02f));
        }

        [UnityTest]
        public IEnumerator SkippedPresentationTicksAndZeroScaledDelta_PauseTheOwnedSequence()
        {
            using var world = new World();
            Entity box = Box(world);
            BoxGoalAnimator animator = Bind(box, out Transform visual);
            Vector3 baseline = visual.localPosition;
            Vector3 scale = visual.localScale;
            using var pipeline = Pipeline(world);
            world.Publish(new BoxEnteredGoal(box, box.Get<GridPosition>()));
            pipeline.Update(0f);
            pipeline.Update(0.05f);
            Vector3 pausedPosition = visual.localPosition;
            Vector3 pausedScale = visual.localScale;
            for (int frame = 0; frame < 4; frame++) yield return null;
            Assert.That(animator.IsPlaying, Is.True);
            Assert.That(visual.localPosition, Is.EqualTo(pausedPosition));
            Assert.That(visual.localScale, Is.EqualTo(pausedScale));
            Time.timeScale = 0f;
            yield return null;
            pipeline.Update(Time.deltaTime);
            Assert.That(Time.deltaTime, Is.Zero);
            Assert.That(visual.localPosition, Is.EqualTo(pausedPosition));
            Assert.That(visual.localScale, Is.EqualTo(pausedScale));
            Time.timeScale = _timeScale;
            pipeline.Update(1f);
            AssertRestored(animator, visual, baseline, scale);
        }

        [UnityTest]
        public IEnumerator DisableAndDestroy_StopOwnedTweensWithoutAnotherEcsTick()
        {
            using var world = new World();
            BoxGoalAnimator animator = Bind(Box(world), out Transform visual);
            Vector3 baseline = visual.localPosition;
            Vector3 scale = visual.localScale;
            animator.PlayGoalFeedback(Options);
            animator.Tick(0.05f);
            animator.gameObject.SetActive(false);
            AssertRestored(animator, visual, baseline, scale);
            animator.gameObject.SetActive(true);
            animator.PlayGoalFeedback(Options);
            animator.Tick(0.05f);
            Object.Destroy(animator.gameObject);
            Assert.That(animator.IsPlaying, Is.False);
            Assert.That(Tween.GetTweensCount(visual), Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RunDisposalDuringTween_StopsImmediatelyAndReplacementRunStartsClean()
        {
            var parent = CreateObject("Run root");
            var boxPrefab = CreateView();
            var fallback = CreateObject("Fallback");
            var provider = new Provider(boxPrefab, fallback);
            var options = new RunOptions(SkinId.Default, 60f, 0.16f, Options);
            LevelDefinition level = LevelParser.Parse("#####\n#@$.#\n#####");
            var run = new LevelRun(new LevelRoot(parent.transform), level, provider, new ViewFeatures(), new LevelPopulator(), options);
            run.Send(MoveCommand.Right);
            run.Tick(0.16f);
            run.TickView(0.05f);
            var animator = parent.GetComponentInChildren<BoxGoalAnimator>();
            Transform visual = animator.transform.Find("Visual");
            Vector3 baseline = boxPrefab.transform.Find("Visual").localPosition;
            Vector3 scale = boxPrefab.transform.Find("Visual").localScale;
            Assert.That(animator.IsPlaying, Is.True);
            Assert.That(Tween.GetTweensCount(visual), Is.GreaterThan(0));
            run.Dispose();
            AssertRestored(animator, visual, baseline, scale);
            using var replacement = new LevelRun(new LevelRoot(parent.transform), level, provider, new ViewFeatures(), new LevelPopulator(), options);
            replacement.TickView(0.1f);
            var nextAnimator = parent.GetComponentInChildren<BoxGoalAnimator>();
            Assert.That(nextAnimator, Is.Not.SameAs(animator));
            Assert.That(nextAnimator.IsPlaying, Is.False);
            for (int frame = 0; frame < 3; frame++) yield return null;
            Assert.That(nextAnimator.IsPlaying, Is.False);
        }

        private static SequentialSystem<float> Pipeline(World world) =>
            new(new BoxGoalFeedbackLifecycleSystem(world), new BoxGoalFeedbackSystem(world, Options));

        [UnityTest]
        public IEnumerator ControllerLoading_PausesFeedbackUntilTheRetainedRunIsReplaced()
        {
            var parent = CreateObject("Controller root");
            var prefab = CreateView();
            var provider = new Provider(prefab, CreateObject("Fallback"));
            var input = new Input();
            var source = new HoldingSource();
            GameplaySettings settings = TestGameplaySettings.Create(completionDelay: 10f);
            try
            {
                var factory = new LevelRunFactory(new LevelRoot(parent.transform), provider, new ViewFeatures(), new LevelPopulator());
                using var controller = new GameplayController(input, source, factory, new TestStaticData { Gameplay = settings });
                controller.Initialize();
                controller.Tick(0f);
                input.Move = MoveCommand.Right;
                controller.Tick(0.16f);
                input.Move = default;
                controller.Tick(0.05f);
                var animator = parent.GetComponentInChildren<BoxGoalAnimator>();
                Transform visual = animator.transform.Find("Visual");
                Assert.That(animator.IsPlaying, Is.True);
                input.NextLevelPressed = true;
                controller.Tick(0f);
                input.NextLevelPressed = false;
                Assert.That(controller.Phase, Is.EqualTo(GameplayPhase.Loading));
                Vector3 position = visual.localPosition;
                Vector3 scale = visual.localScale;
                for (int frame = 0; frame < 4; frame++)
                {
                    yield return null;
                    controller.Tick(0.1f);
                }
                Assert.That(visual.localPosition, Is.EqualTo(position));
                Assert.That(visual.localScale, Is.EqualTo(scale));
                source.Complete();
                Assert.That(controller.Phase, Is.EqualTo(GameplayPhase.BindingViews));
                Assert.That(animator.IsPlaying, Is.False);
                Assert.That(Tween.GetTweensCount(visual), Is.Zero);
                Assert.That(visual.localPosition, Is.EqualTo(prefab.transform.Find("Visual").localPosition));
                Assert.That(visual.localScale, Is.EqualTo(prefab.transform.Find("Visual").localScale));
                Assert.That(parent.GetComponentInChildren<BoxGoalAnimator>().IsPlaying, Is.False);
            }
            finally
            {
                Object.Destroy(settings);
            }
        }

        private static void AssertRestored(BoxGoalAnimator animator, Transform visual, Vector3 position, Vector3 scale)
        {
            Assert.That(animator.IsPlaying, Is.False);
            Assert.That(Tween.GetTweensCount(visual), Is.Zero);
            Assert.That(visual.localPosition, Is.EqualTo(position));
            Assert.That(visual.localScale, Is.EqualTo(scale));
        }

        private static Entity Box(World world)
        {
            Entity box = world.CreateEntity();
            box.Set<BoxTag>();
            box.Set(new GridPosition(3, 1));
            return box;
        }

        private BoxGoalAnimator Bind(Entity box, out Transform visual)
        {
            GameObject root = CreateView();
            root.transform.position = new Vector3(3f, 0f, 1f);
            box.Set(new TransformComponent(root.transform));
            root.GetComponent<EntityView>().Install(box);
            visual = root.transform.Find("Visual");
            return root.GetComponent<BoxGoalAnimator>();
        }

        private GameObject CreateView()
        {
            GameObject root = CreateObject("Box root");
            root.SetActive(false);
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            visual.localPosition = new Vector3(-0.004f, 0.02f, 0.001f);
            visual.localScale = Vector3.one * 0.442f;
            var animator = root.AddComponent<BoxGoalAnimator>();
            var animatorData = new SerializedObject(animator);
            animatorData.FindProperty("visual").objectReferenceValue = visual;
            animatorData.ApplyModifiedPropertiesWithoutUndo();
            var adapter = root.AddComponent<BoxGoalFeedbackAdapter>();
            var adapterData = new SerializedObject(adapter);
            adapterData.FindProperty("animator").objectReferenceValue = animator;
            adapterData.ApplyModifiedPropertiesWithoutUndo();
            var view = root.AddComponent<EntityView>();
            var viewData = new SerializedObject(view);
            var adapters = viewData.FindProperty("adapters");
            adapters.arraySize = 1;
            adapters.GetArrayElementAtIndex(0).objectReferenceValue = adapter;
            viewData.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(true);
            return root;
        }

        private GameObject CreateObject(string name)
        {
            var target = new GameObject(name);
            _objects.Add(target);
            return target;
        }

        private sealed class Provider : IViewProvider
        {
            private readonly GameObject _box;
            private readonly GameObject _fallback;

            public Provider(GameObject box, GameObject fallback)
            {
                _box = box;
                _fallback = fallback;
            }

            public Cysharp.Threading.Tasks.UniTask<GameObject> LoadAsync(ViewKey key, System.Threading.CancellationToken token) =>
                Cysharp.Threading.Tasks.UniTask.FromResult(key.Kind == ViewKind.Box ? _box : _fallback);
        }

        private sealed class HoldingSource : ILevelSource
        {
            private readonly Cysharp.Threading.Tasks.UniTaskCompletionSource<LevelDefinition> _pending = new();
            private static LevelDefinition Level => LevelParser.Parse("#####\n#@$.#\n#####");
            public int Count => 2;

            public Cysharp.Threading.Tasks.UniTask<LevelDefinition> LoadAsync(int index, System.Threading.CancellationToken token) =>
                index == 0 ? Cysharp.Threading.Tasks.UniTask.FromResult(Level) : _pending.Task;

            public void Complete() => _pending.TrySetResult(Level);
        }

        private sealed class Input : IPlayerInput
        {
            public MoveCommand Move;
            public bool RestartPressed => false;
            public bool NextLevelPressed { get; set; }
            public bool NextSkinPressed => false;
            public MoveCommand ReadMove() => Move;
        }
    }
}
#endif
