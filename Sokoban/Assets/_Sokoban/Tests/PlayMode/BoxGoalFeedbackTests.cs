#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        public void Occupancy_PersistsAfterArrivalAndClearsWhenTheBoxLeaves()
        {
            using var world = new World();
            Entity box = Box(world);
            Entity goal = world.CreateEntity();
            goal.Set<GoalTag>();
            goal.Set(box.Get<GridPosition>());
            BoxGoalAnimator animator = Bind(box, out Transform visual);
            GameObject marker = visual.Find("OccupiedGoalMarker").gameObject;
            Vector3 position = visual.localPosition;
            Vector3 scale = visual.localScale;
            using var pipeline = Pipeline(world);

            pipeline.Update(0f);
            Assert.That(marker.activeSelf, Is.True, "Initial occupancy does not require an arrival event.");
            Assert.That(animator.IsPlaying, Is.False);

            world.Publish(new BoxEnteredGoal(box, box.Get<GridPosition>()));
            pipeline.Update(0f);
            pipeline.Update(0.055f);
            Assert.That(animator.IsPlaying, Is.True);
            Assert.That(marker.activeSelf, Is.True);
            pipeline.Update(1f);
            AssertRestored(animator, visual, position, scale);
            Assert.That(box.Has<BoxGoalFeedbackState>(), Is.False);
            Assert.That(marker.activeSelf, Is.True, "Tween completion must not clear occupancy.");

            box.Set(new GridPosition(4, 1));
            pipeline.Update(0f);
            Assert.That(marker.activeSelf, Is.False);
        }

        [Test]
        public void Occupancy_FollowsPushAndUndoInBothDirectionsWithoutReplayingArrival()
        {
            LevelDefinition level = LevelParser.Parse("########\n#@$.-$.#\n#------#\n########");
            using var simulation = new Simulation(level, new LevelPopulator(), 60f, new SimulationPipelineFactory());
            var provider = new Provider(CreateView(), CreateObject("Fallback"));
            using var layer = new ViewLayer(new LevelRoot(CreateObject("View parent").transform), simulation.World,
                provider, new ViewPipelineFactory(), new RunOptions(SkinId.Default, 60f, 0.16f));
            int arrivals = 0;
            using var subscription = simulation.World.Subscribe<BoxEnteredGoal>((in BoxEnteredGoal _) => { arrivals++; });
            layer.Tick(0f);
            Entity box = simulation.World.Single(e => e.Has<BoxTag>() && e.Get<GridPosition>().X == 2);
            BoxGoalAnimator animator = box.Get<BoxGoalFeedbackComponent>().Animator;
            GameObject marker = animator.transform.Find("Visual/OccupiedGoalMarker").gameObject;
            Assert.That(marker.activeSelf, Is.False);

            simulation.Send(MoveCommand.Right);
            simulation.Tick(0.16f);
            layer.Tick(0.16f);
            layer.Tick(0.3f);
            Assert.That(marker.activeSelf, Is.True);
            Assert.That(animator.IsPlaying, Is.False);
            Assert.That(arrivals, Is.EqualTo(1));

            simulation.Send(MoveCommand.Right);
            simulation.Tick(0.16f);
            layer.Tick(0.16f);
            Assert.That(marker.activeSelf, Is.False);

            Assert.That(simulation.RequestUndo(), Is.True);
            simulation.Tick(0.16f);
            layer.Tick(0.16f);
            Assert.That(marker.activeSelf, Is.True);
            Assert.That(animator.IsPlaying, Is.False);
            Assert.That(arrivals, Is.EqualTo(1));

            Assert.That(simulation.RequestUndo(), Is.True);
            simulation.Tick(0.16f);
            layer.Tick(0.16f);
            Assert.That(marker.activeSelf, Is.False);
            Assert.That(animator.IsPlaying, Is.False);
            Assert.That(arrivals, Is.EqualTo(1));
        }

        [Test]
        public void Occupancy_DisabledCompositePausesSynchronizationAndDisposalClearsSettledMarkers()
        {
            using var world = new World();
            Entity box = Box(world);
            Entity goal = world.CreateEntity();
            goal.Set<GoalTag>();
            goal.Set(box.Get<GridPosition>());
            BoxGoalAnimator animator = Bind(box, out Transform visual);
            GameObject marker = visual.Find("OccupiedGoalMarker").gameObject;
            var pipeline = Pipeline(world);
            try
            {
                pipeline.IsEnabled = false;
                pipeline.Update(0f);
                Assert.That(marker.activeSelf, Is.False);
                pipeline.IsEnabled = true;
                pipeline.Update(0f);
                Assert.That(marker.activeSelf, Is.True);
                Assert.That(animator.IsPlaying, Is.False);
            }
            finally
            {
                pipeline.Dispose();
            }

            Assert.That(marker.activeSelf, Is.False);
        }

        [Test]
        public void DisabledComposite_BuffersArrivalAndPausesActiveFeedbackUntilReenabled()
        {
            using var world = new World();
            Entity box = Box(world);
            BoxGoalAnimator animator = Bind(box, out Transform visual);
            Vector3 baseline = visual.localPosition;
            Vector3 scale = visual.localScale;
            using var pipeline = Pipeline(world);
            Assert.That(pipeline.IsEnabled, Is.True);
            pipeline.IsEnabled = false;
            world.Publish(new BoxEnteredGoal(box, box.Get<GridPosition>()));
            pipeline.Update(0.05f);
            Assert.That(animator.IsPlaying, Is.False);
            Assert.That(visual.localPosition, Is.EqualTo(baseline));
            pipeline.IsEnabled = true;
            pipeline.Update(0f);
            Assert.That(animator.IsPlaying, Is.True);
            pipeline.Update(0.055f);
            Vector3 position = visual.localPosition;
            Assert.That(position.y, Is.GreaterThan(baseline.y));
            pipeline.IsEnabled = false;
            pipeline.Update(0.3f);
            Assert.That(animator.IsPlaying, Is.True);
            Assert.That(visual.localPosition, Is.EqualTo(position));
            pipeline.IsEnabled = true;
            pipeline.Update(0.22f);
            AssertRestored(animator, visual, baseline, scale);
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
            box.Set(new ViewMove(default, default, 0.16f));
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
            box.Set(new ViewMove(default, default, 0.16f));
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
            if (moving) box.Set(new ViewMove(default, default, 0.16f));
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
                animator.PlayGoalFeedback();
                animator.Tick(0.055f);
                Assert.That(visual.localPosition.y - baseline.y, Is.InRange(0f, 0.121f));
                Assert.That(visual.localScale.x, Is.GreaterThan(scale.x));
            }
            animator.Tick(0.22f);
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
            animator.SetOccupied(true);
            adapter.Uninstall(box);
            Assert.That(visual.Find("OccupiedGoalMarker").gameObject.activeSelf, Is.False);
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
            animator.PlayGoalFeedback();
            animator.SetOccupied(true);
            animator.Tick(0.05f);
            animator.gameObject.SetActive(false);
            Assert.That(visual.Find("OccupiedGoalMarker").gameObject.activeSelf, Is.False);
            AssertRestored(animator, visual, baseline, scale);
            animator.gameObject.SetActive(true);
            animator.PlayGoalFeedback();
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
            var options = new RunOptions(SkinId.Default, 60f, 0.16f);
            LevelDefinition level = LevelParser.Parse("#####\n#@$.#\n#####");
            var simulationPipelineFactory = new SimulationPipelineFactory();
            var viewPipelineFactory = new ViewPipelineFactory();
            var run = new LevelRun(new LevelRoot(parent.transform), level, provider, new LevelPopulator(),
                simulationPipelineFactory, viewPipelineFactory, options);
            run.Send(MoveCommand.Right);
            run.Tick(0.16f);
            run.TickView(0.05f);
            var animator = parent.GetComponentInChildren<BoxGoalAnimator>();
            Transform visual = animator.transform.Find("Visual");
            Vector3 baseline = boxPrefab.transform.Find("Visual").localPosition;
            Vector3 scale = boxPrefab.transform.Find("Visual").localScale;
            Assert.That(animator.IsPlaying, Is.True);
            Assert.That(Tween.GetTweensCount(visual), Is.GreaterThan(0));
            Assert.That(visual.Find("OccupiedGoalMarker").gameObject.activeSelf, Is.True);
            run.Dispose();
            AssertRestored(animator, visual, baseline, scale);
            Assert.That(visual.Find("OccupiedGoalMarker").gameObject.activeSelf, Is.False);
            using var replacement = new LevelRun(new LevelRoot(parent.transform), level, provider, new LevelPopulator(),
                simulationPipelineFactory, viewPipelineFactory, options);
            replacement.TickView(0.1f);
            var nextAnimator = parent.GetComponentInChildren<BoxGoalAnimator>();
            Assert.That(nextAnimator, Is.Not.SameAs(animator));
            Assert.That(nextAnimator.IsPlaying, Is.False);
            Assert.That(nextAnimator.transform.Find("Visual/OccupiedGoalMarker").gameObject.activeSelf, Is.False);
            for (int frame = 0; frame < 3; frame++) yield return null;
            Assert.That(nextAnimator.IsPlaying, Is.False);
        }

        private static ISystem<float> Pipeline(World world) =>
            new BoxGoalFeedbackCompositeSystem(world);

        [UnityTest]
        public IEnumerator ControllerLoading_PausesFeedbackUntilTheRetainedRunIsReplaced()
        {
            var parent = CreateObject("Controller root");
            var prefab = CreateView();
            var provider = new Provider(prefab, CreateObject("Fallback"));
            var input = new Input();
            var source = new HoldingSource();
            GameplaySettings settings = TestGameplaySettings.Create(completionDelay: 10f);
            var catalog = ScriptableObject.CreateInstance<BackgroundCatalog>();
            var characterAssets = new List<UnityEngine.Object>();
            try
            {
                var factory = new LevelRunFactory(new LevelRoot(parent.transform), provider, new LevelPopulator(), new SimulationPipelineFactory(), new ViewPipelineFactory());
                var data = new TestStaticData { Gameplay = settings };
                TestCharacterSelection.Configure(data, characterAssets);
                var backgrounds = TestBackgrounds.Create(data, CreateObject("Background target"), catalog);
                using var controller = new GameplayController(input, source, new HintCoordinator(new HintService(new HintSolver()), provider),
                    backgrounds, factory, data);
                controller.Initialize();
                TestCharacterSelection.Tick(controller, 0f);
                input.Move = MoveCommand.Right;
                TestCharacterSelection.Tick(controller, 0.16f);
                input.Move = default;
                TestCharacterSelection.Tick(controller, 0.05f);
                var animator = parent.GetComponentInChildren<BoxGoalAnimator>();
                Transform visual = animator.transform.Find("Visual");
                Assert.That(animator.IsPlaying, Is.True);
                input.NextLevelPressed = true;
                TestCharacterSelection.Tick(controller, 0f);
                input.NextLevelPressed = false;
                Assert.That(controller.Phase, Is.EqualTo(GameplayPhase.Loading));
                Vector3 position = visual.localPosition;
                Vector3 scale = visual.localScale;
                for (int frame = 0; frame < 4; frame++)
                {
                    yield return null;
                    TestCharacterSelection.Tick(controller, 0.1f);
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
                Object.Destroy(catalog);
                foreach (UnityEngine.Object asset in characterAssets)
                {
                    Object.Destroy(asset);
                }
            }
        }

        [Test]
        public void SerializedTuning_DrivesParameterlessPlayback()
        {
            GameObject root = CreateView(0.4f, 0.3f, 0.2f);
            var animator = root.GetComponent<BoxGoalAnimator>();
            Transform visual = root.transform.Find("Visual");
            Vector3 baseline = visual.localPosition;
            Vector3 scale = visual.localScale;
            animator.PlayGoalFeedback();
            animator.Tick(0.1f);
            Assert.That(visual.localScale.x, Is.EqualTo(scale.x * 1.2f).Within(0.00001f));
            Assert.That(visual.localScale.y, Is.EqualTo(scale.y * 0.8f).Within(0.00001f));
            animator.Tick(0.1f);
            Assert.That(visual.localPosition.y, Is.EqualTo(baseline.y + 0.3f).Within(0.00001f));
            Assert.That(visual.localScale.y, Is.EqualTo(scale.y * 1.1f).Within(0.00001f));
            Assert.That(animator.IsPlaying, Is.True);
            animator.Tick(0.2f);
            AssertRestored(animator, visual, baseline, scale);
        }

        [Test]
        public void SavedBoxPrefab_PreservesAuthoredFeedbackTuning()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AddressablesDependencies/Box/Prefabs/Box.prefab");
            GameObject root = Object.Instantiate(prefab);
            _objects.Add(root);
            var animator = root.GetComponent<BoxGoalAnimator>();
            Transform visual = root.transform.Find("Visual");
            Vector3 baseline = visual.localPosition;
            Vector3 scale = visual.localScale;
            Vector3 rootPosition = root.transform.position;
            animator.PlayGoalFeedback();
            animator.Tick(0.055f);
            Assert.That(visual.localScale.y, Is.EqualTo(scale.y * 0.88f).Within(0.00001f));
            animator.Tick(0.055f);
            Assert.That(visual.localPosition.y, Is.EqualTo(baseline.y + 0.12f).Within(0.00001f));
            Assert.That(animator.IsPlaying, Is.True);
            animator.Tick(0.11f);
            AssertRestored(animator, visual, baseline, scale);
            Assert.That(root.transform.position, Is.EqualTo(rootPosition));
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

        private GameObject CreateView(float durationSeconds = 0.22f, float height = 0.12f, float squashAmount = 0.12f)
        {
            GameObject root = CreateObject("Box root");
            root.SetActive(false);
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            visual.localPosition = new Vector3(-0.004f, 0.02f, 0.001f);
            visual.localScale = Vector3.one * 0.442f;
            var marker = new GameObject("OccupiedGoalMarker");
            marker.transform.SetParent(visual, false);
            marker.SetActive(false);
            var animator = root.AddComponent<BoxGoalAnimator>();
            var animatorData = new SerializedObject(animator);
            animatorData.FindProperty("visual").objectReferenceValue = visual;
            animatorData.FindProperty("occupiedMarker").objectReferenceValue = marker;
            animatorData.FindProperty("durationSeconds").floatValue = durationSeconds;
            animatorData.FindProperty("height").floatValue = height;
            animatorData.FindProperty("squashAmount").floatValue = squashAmount;
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
            public bool UndoPressed => false;
            public MoveCommand Move;
            public bool RestartPressed => false;
            public bool NextLevelPressed { get; set; }
            public bool IsNeutral => true;
            public MoveCommand ReadMove() => Move;
        }
    }
}
#endif
