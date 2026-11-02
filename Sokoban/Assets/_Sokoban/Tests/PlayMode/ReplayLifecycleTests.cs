#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Sokoban.Core;
using Sokoban.Core.Commands;
using Sokoban.Core.Components;
using Sokoban.Presentation;
using Sokoban.Presentation.Ecs.Components;
using UnityEngine;
using UnityEngine.TestTools;

namespace Sokoban.Tests.PlayMode
{
    public sealed class ReplayLifecycleTests
    {
        private readonly List<GameObject> _objects = new();
        private readonly List<ReplaySession> _sessions = new();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (ReplaySession session in _sessions)
            {
                session.Dispose();
            }
            foreach (GameObject target in _objects)
            {
                if (target != null)
                {
                    UnityEngine.Object.Destroy(target);
                }
            }
            _sessions.Clear();
            _objects.Clear();
            yield return null;
        }

        [TestCase(0.03125f)]
        [TestCase(0.125f)]
        [TestCase(0.5f)]
        public void DifferentFramePartitionsMatchCoreReplayIncludingUndoBurstsZeroTicksAndPendingEndpoint(float frame)
        {
            using var source = Source();
            source.Send(MoveCommand.Right);
            source.Send(MoveCommand.Left);
            source.Tick(0.25f);
            source.RequestUndo();
            source.RequestUndo();
            source.Send(MoveCommand.Right);
            source.Tick(0f);
            source.Send(MoveCommand.Right);
            source.Tick(0.125f);
            source.Send(MoveCommand.Right);
            source.Tick(0.25f);
            source.Send((MoveCommand)99);
            source.Tick(0f);
            source.Tick(0.125f);
            source.Send(MoveCommand.Left); // Endpoint must remain pending: no final flush tick.
            SimulationRecording recording = source.ExportRecording();
            ReplaySession session = Create(recording, new Provider(Object("Prefab")));
            session.Tick(0f);
            Simulation actual = SimulationOf(session);
            using var expected = new SimulationReplay(recording);
            double clock = 0d;
            double tickTime = 0d;
            int input = 0;
            for (int update = 0; update < 40; update++)
            {
                clock += frame;
                session.Tick(frame);
                while (input < recording.Inputs.Count)
                {
                    SimulationInput next = recording.Inputs[input];
                    if (next.Kind == SimulationInputKind.Tick && tickTime + next.DeltaTime > clock)
                    {
                        break;
                    }
                    expected.Step();
                    input++;
                    if (next.Kind == SimulationInputKind.Tick)
                    {
                        tickTime += next.DeltaTime;
                    }
                }
                Assert.That(actual.CaptureSnapshot(), Is.EqualTo(expected.Simulation.CaptureSnapshot()), "Rendering update " + update);
            }
            Assert.That(session.Status, Is.EqualTo(ReplayStatus.Finished));
            Assert.That(actual.CaptureSnapshot(), Is.EqualTo(source.CaptureSnapshot()));
            Assert.That(actual.World.Single(e => e.Has<PlayerTag>()).Has<MoveRequest>(), Is.True);
            Assert.Throws<InvalidOperationException>(() => actual.ExportRecording());
        }

        [Test]
        public void CatchUpIsBoundedAndRetainsEveryInputWithoutCollapsingZeroTicks()
        {
            using var source = Source();
            for (int index = 0; index < 400; index++)
            {
                source.Send(MoveCommand.Left);
                source.Tick(0f);
            }
            ReplaySession session = Create(source.ExportRecording(), new Provider(Object("Prefab")));
            session.Tick(0f);
            Simulation simulation = SimulationOf(session);
            int requests = 0;
            using var observer = simulation.World.SubscribeComponentAdded<MoveRequest>((in DefaultEcs.Entity entity, in MoveRequest request) => requests++);
            session.Tick(10f);
            Assert.That(session.Status, Is.EqualTo(ReplayStatus.Playing));
            Assert.That(requests, Is.InRange(1, 399));
            for (int update = 0; update < 8 && session.Status != ReplayStatus.Finished; update++)
            {
                session.Tick(0f);
            }
            Assert.That(session.Status, Is.EqualTo(ReplayStatus.Finished));
            Assert.That(requests, Is.EqualTo(400));
            Assert.That(simulation.CaptureSnapshot(), Is.EqualTo(source.CaptureSnapshot()));
        }

        [Test]
        public void PauseFreezesCoreInterpolationAndAnimatorSpeedThenResumeContinues()
        {
            using var source = Source();
            source.Send(MoveCommand.Right);
            source.Tick(0.25f);
            source.Tick(1f);
            GameObject prefab = Object("Animated prefab");
            prefab.AddComponent<Animator>().speed = 0.7f;
            ReplaySession session = Create(source.ExportRecording(), new Provider(prefab));
            session.Tick(0f);
            session.Tick(0.25f);
            Simulation simulation = SimulationOf(session);
            var player = simulation.World.Single(e => e.Has<PlayerTag>());
            Transform view = player.Get<TransformComponent>().Transform;
            var animator = view.GetComponent<Animator>();
            animator.speed = 0.7f;
            GameplaySnapshot snapshot = simulation.CaptureSnapshot();
            Vector3 position = view.position;
            session.TogglePause();
            session.TogglePause();
            Assert.That(animator.speed, Is.EqualTo(0.7f));
            session.TogglePause();
            Assert.That(animator.speed, Is.Zero);
            session.Tick(100f);
            Assert.That(simulation.CaptureSnapshot(), Is.EqualTo(snapshot));
            Assert.That(view.position, Is.EqualTo(position));
            Assert.That(session.Status, Is.EqualTo(ReplayStatus.Paused));
            session.TogglePause();
            Assert.That(animator.speed, Is.EqualTo(0.7f));
            session.Tick(0.5f);
            Assert.That(view.position.x, Is.GreaterThan(position.x));
            session.Tick(0.5f);
            Assert.That(session.Status, Is.EqualTo(ReplayStatus.Finished));
            session.Tick(1f);
            Assert.That(simulation.CaptureSnapshot(), Is.EqualTo(source.CaptureSnapshot()));
        }

        [UnityTest]
        public IEnumerator LoadingDoesNotAccumulateClockAndDisposalRejectsIgnoringLateResults()
        {
            using var source = Source();
            source.Send(MoveCommand.Right);
            source.Tick(0.25f);
            var provider = new Provider(Object("Delayed prefab")) { Pending = new UniTaskCompletionSource<GameObject>() };
            ReplaySession session = Create(source.ExportRecording(), provider);
            session.Tick(30f);
            Assert.That(session.Status, Is.EqualTo(ReplayStatus.Binding));
            provider.Pending.TrySetResult(provider.Prefab);
            yield return null;
            session.Tick(40f);
            Assert.That(session.Status, Is.EqualTo(ReplayStatus.Playing));
            session.Tick(0f);
            Assert.That(SimulationOf(session).PlayerPosition, Is.EqualTo(new GridPosition(1, 1)));
            Assert.That(SimulationOf(session).RemainingSeconds, Is.EqualTo(60f));
            foreach (bool fail in new[] { false, true })
            {
                var late = new Provider(provider.Prefab) { Pending = new UniTaskCompletionSource<GameObject>() };
                Transform root = Object("Late root").transform;
                ReplaySession abandoned = Create(source.ExportRecording(), late, root);
                abandoned.Dispose();
                Assert.That(late.Tokens.All(token => token.IsCancellationRequested), Is.True);
                if (fail)
                {
                    late.Pending.TrySetException(new InvalidOperationException("obsolete replay load"));
                }
                else
                {
                    late.Pending.TrySetResult(late.Prefab);
                }
                yield return null;
                Assert.That(root.childCount, Is.Zero);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator StepFailureDisposesWorldBeforeSafePresentationCleanupAndNeverTicksItAgain()
        {
            using var source = Source();
            source.Send(MoveCommand.Right);
            source.Tick(0.25f);
            Transform root = Object("Fault root").transform;
            ReplaySession session = Create(source.ExportRecording(), new Provider(Object("Prefab")), root);
            session.Tick(0f);
            Simulation simulation = SimulationOf(session);
            int disposals = 0;
            using var disposed = simulation.World.SubscribeWorldDisposed(_ => disposals++);
            using var fault = simulation.World.SubscribeComponentChanged<LevelTimer>(
                (in DefaultEcs.Entity entity, in LevelTimer before, in LevelTimer after) => throw new InvalidOperationException("planned replay step fault"));
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("planned replay step fault"));
            Assert.DoesNotThrow(() => session.Tick(0.25f));
            Assert.That(session.Status, Is.EqualTo(ReplayStatus.Failed));
            Assert.That(disposals, Is.EqualTo(1));
            Assert.DoesNotThrow(() => session.Tick(1f));
            session.Dispose();
            yield return null;
            Assert.That(root.childCount, Is.Zero);
            Assert.That(disposals, Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PauseFreezesActiveGoalFeedbackAndDisposalRestoresItsExactPose()
        {
            using var source = new Simulation(LevelParser.Parse("#########\n#@$.--$.#\n#########"), new LevelPopulator(), 60f, new SimulationPipelineFactory());
            source.Send(MoveCommand.Right);
            source.Tick(0.25f);
            source.Tick(1f);
            GameObject prefab = Object("Feedback prefab");
            prefab.SetActive(false);
            var visual = new GameObject("Visual").transform;
            visual.SetParent(prefab.transform, false);
            visual.localPosition = new Vector3(0f, 0.03f, 0f);
            var feedback = prefab.AddComponent<BoxGoalAnimator>();
            var data = new UnityEditor.SerializedObject(feedback);
            data.FindProperty("visual").objectReferenceValue = visual;
            data.ApplyModifiedPropertiesWithoutUndo();
            var adapter = prefab.AddComponent<Sokoban.Presentation.Ecs.Adapters.BoxGoalFeedbackAdapter>();
            data = new UnityEditor.SerializedObject(adapter);
            data.FindProperty("animator").objectReferenceValue = feedback;
            data.ApplyModifiedPropertiesWithoutUndo();
            var entityView = prefab.AddComponent<Sokoban.Presentation.Ecs.Model.EntityView>();
            data = new UnityEditor.SerializedObject(entityView);
            data.FindProperty("adapters").arraySize = 1;
            data.FindProperty("adapters").GetArrayElementAtIndex(0).objectReferenceValue = adapter;
            data.ApplyModifiedPropertiesWithoutUndo();
            prefab.SetActive(true);
            ReplaySession session = Create(source.ExportRecording(), new Provider(prefab));
            session.Tick(0f);
            session.Tick(0.25f);
            session.Tick(0.2f);
            session.Tick(0.055f);
            var box = SimulationOf(session).World.Single(e => e.Has<BoxTag>() && e.Get<GridPosition>() == new GridPosition(3, 1));
            BoxGoalAnimator animator = box.Get<BoxGoalFeedbackComponent>().Animator;
            Transform animated = animator.transform.Find("Visual");
            Assert.That(animator.IsPlaying, Is.True);
            Assert.That(animated.localPosition.y, Is.GreaterThan(visual.localPosition.y));
            Vector3 pose = animated.localPosition;
            Vector3 scale = animated.localScale;
            session.TogglePause();
            yield return null;
            yield return null;
            session.Tick(10f);
            Assert.That(animated.localPosition, Is.EqualTo(pose));
            Assert.That(animated.localScale, Is.EqualTo(scale));
            session.Dispose();
            Assert.That(animator.IsPlaying, Is.False);
            Assert.That(PrimeTween.Tween.GetTweensCount(animated), Is.Zero);
            Assert.That(animated.localPosition, Is.EqualTo(visual.localPosition));
            Assert.That(animated.localScale, Is.EqualTo(visual.localScale));
        }

        private ReplaySession Create(SimulationRecording recording, Provider provider, Transform root = null)
        {
            var session = new ReplaySession(recording, new LevelRoot(root != null ? root : Object("Replay root").transform), provider,
                new ViewPipelineFactory(), new RunOptions(SkinId.Alternate, recording.TimeLimitSeconds, 0.16f));
            _sessions.Add(session);
            return session;
        }

        // Like the existing controller fixtures, observe the owned Core instance without
        // adding mutable-world access or a test-only API to the production session.
        private static Simulation SimulationOf(ReplaySession session) =>
            ((SimulationReplay)typeof(ReplaySession).GetField("_replay", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session)).Simulation;

        private static Simulation Source() =>
            new(LevelParser.Parse("#########\n#@-$---.#\n#########"), new LevelPopulator(), 60f, new SimulationPipelineFactory());

        private GameObject Object(string name)
        {
            var target = new GameObject(name);
            _objects.Add(target);
            return target;
        }

        private sealed class Provider : IViewProvider
        {
            public GameObject Prefab { get; }
            public UniTaskCompletionSource<GameObject> Pending;
            public List<CancellationToken> Tokens { get; } = new();
            public Provider(GameObject prefab) => Prefab = prefab;
            public UniTask<GameObject> LoadAsync(ViewKey key, CancellationToken token)
            {
                Tokens.Add(token);
                return Pending == null ? UniTask.FromResult(Prefab) : Pending.Task;
            }
        }
    }
}
#endif
