using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using DefaultEcs;
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
    public sealed class CharacterSelectionTests
    {
        private readonly List<UnityEngine.Object> _objects = new();
        private readonly List<GameplayController> _controllers = new();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameplayController controller in _controllers)
            {
                controller.Dispose();
            }
            _controllers.Clear();
            foreach (UnityEngine.Object target in _objects)
            {
                if (target != null)
                {
                    UnityEngine.Object.Destroy(target);
                }
            }
            _objects.Clear();
            yield return null;
        }

        [Test]
        public void BoardBindsWithoutRequestingAPlayerAndSelectionNeverTicksSimulation()
        {
            Fixture f = Create();
            f.Controller.Initialize();
            Simulation simulation = SimulationOf(f.Controller);
            Entity player = Player(simulation);
            Assert.That(player.Has<PlayerViewDeferred>(), Is.True);
            Assert.That(player.Has<ViewPending>() || player.Has<TransformComponent>(), Is.False);
            Assert.That(simulation.World.Where(e => e.Has<GridPosition>() && e.Has<PlayerTag>() == false)
                .All(e => e.Has<TransformComponent>()), Is.True);
            Assert.That(f.Provider.Players, Is.Empty);
            GameplaySnapshot before = simulation.CaptureSnapshot();
            f.Controller.Tick(20f);
            Assert.That(f.Controller.Phase, Is.EqualTo(GameplayPhase.SelectingCharacter));
            for (int index = 0; index < 5; index++)
            {
                f.Controller.Tick(20f);
            }
            Assert.That(simulation.CaptureSnapshot(), Is.EqualTo(before));
            Assert.That(simulation.ExportRecording().Inputs, Is.Empty);
            Assert.That(f.Controller.Characters, Is.EqualTo(f.Data.Views.Entries.Single().Skins.Select(s => s.Skin)));
            Assert.That(f.Input.Reads, Is.Zero);
            Assert.That(f.Provider.Players, Is.Empty);
        }

        [Test]
        public void ConfirmationLoadsOnlyChosenPlayerOnceAndWaitsForReadinessAndInputRelease()
        {
            Fixture f = Create();
            EnterSelection(f);
            f.Input.IsNeutral = false;
            f.Controller.RequestCharacter(SkinId.Punk);
            f.Controller.RequestConfirmCharacter();
            f.Controller.RequestConfirmCharacter();
            f.Controller.Tick(10f);
            Assert.That(f.Controller.Phase, Is.EqualTo(GameplayPhase.BindingCharacter));
            Assert.That(f.Provider.Players.Select(r => r.Key.Skin), Is.EqualTo(new[] { SkinId.Punk }));
            f.Controller.RequestCharacter(SkinId.Alternate);
            f.Controller.RequestConfirmCharacter();
            f.Controller.Tick(10f);
            Assert.That(f.Provider.Players.Count, Is.EqualTo(1));
            Assert.That(f.Controller.RemainingSeconds, Is.EqualTo(60f));
            f.Provider.Players[0].Source.TrySetResult(f.Provider.Prefab);
            f.Controller.Tick(10f);
            Assert.That(f.Controller.Phase, Is.EqualTo(GameplayPhase.BindingCharacter));
            Assert.That(f.Controller.RemainingSeconds, Is.EqualTo(60f));
            Assert.That(f.Input.Reads, Is.Zero);
            f.Input.IsNeutral = true;
            f.Controller.Tick(0f);
            Assert.That(f.Controller.Phase, Is.EqualTo(GameplayPhase.Playing));
            Entity player = Player(SimulationOf(f.Controller));
            Assert.That(player.Get<ViewSkin>().Id, Is.EqualTo(SkinId.Punk));
            Assert.That(player.Has<TransformComponent>(), Is.True);
            Assert.That(player.Has<PlayerViewDeferred>(), Is.False);
            Assert.That(ActiveBoard(f).Cast<Transform>().Count(t => t.name.StartsWith("Player")), Is.EqualTo(1));
            f.Input.Move = MoveCommand.Right;
            f.Controller.Tick(0.16f);
            Assert.That(f.Controller.Moves, Is.EqualTo(1));
        }

        [Test]
        public void FailureAllowsRetryOrAnotherChoiceWithoutConsumingTime()
        {
            Fixture f = Create();
            EnterSelection(f);
            Confirm(f, SkinId.Punk);
            f.Provider.Players[0].Source.TrySetException(new InvalidOperationException("planned character failure"));
            f.Controller.Tick(15f);
            Assert.That(f.Controller.Phase, Is.EqualTo(GameplayPhase.CharacterFailed));
            Assert.That(f.Controller.CanConfirmCharacter, Is.True);
            Assert.That(f.Controller.RemainingSeconds, Is.EqualTo(60f));
            Assert.That(Player(SimulationOf(f.Controller)).Has<TransformComponent>(), Is.False);
            f.Controller.RequestConfirmCharacter();
            f.Controller.Tick(0f);
            Assert.That(f.Provider.Players[1].Key.Skin, Is.EqualTo(SkinId.Punk));
            f.Provider.Players[1].Source.TrySetException(new InvalidOperationException("planned retry failure"));
            f.Controller.Tick(0f);
            Assert.That(f.Controller.Phase, Is.EqualTo(GameplayPhase.CharacterFailed));
            f.Controller.RequestCharacter(SkinId.Alternate);
            f.Controller.Tick(0f);
            Assert.That(f.Controller.Phase, Is.EqualTo(GameplayPhase.SelectingCharacter));
            Confirm(f, SkinId.Alternate);
            f.Provider.Players[2].Source.TrySetResult(f.Provider.Prefab);
            f.Controller.Tick(0f);
            Assert.That(f.Controller.Phase, Is.EqualTo(GameplayPhase.Playing));
            Assert.That(f.Provider.Players.Select(r => r.Key.Skin), Is.EqualTo(new[] { SkinId.Punk, SkinId.Punk, SkinId.Alternate }));
            Assert.That(Player(SimulationOf(f.Controller)).Get<ViewSkin>().Id, Is.EqualTo(SkinId.Alternate));
            f.Controller.RequestRestart();
            f.Controller.Tick(0f);
            f.Provider.Players[3].Source.TrySetException(new InvalidOperationException("planned restart character failure"));
            f.Controller.Tick(0f);
            Assert.That(f.Controller.Phase, Is.EqualTo(GameplayPhase.CharacterFailed));
            Assert.That(f.Controller.SelectedCharacter, Is.EqualTo(SkinId.Alternate));
            Assert.That(f.Controller.RemainingSeconds, Is.EqualTo(60f));
            Confirm(f, SkinId.Punk);
            f.Provider.Players[4].Source.TrySetResult(f.Provider.Prefab);
            f.Controller.Tick(0f);
            Assert.That(f.Controller.Phase, Is.EqualTo(GameplayPhase.Playing));
        }

        [UnityTest]
        public IEnumerator RestartCancelsObsoleteRequestsAndRejectsLateSuccessOrFailure()
        {
            foreach (bool fail in new[] { false, true })
            {
                Fixture f = Create();
                EnterSelection(f);
                Confirm(f, SkinId.Punk);
                Request obsolete = f.Provider.Players[0];
                f.Controller.RequestRestart();
                f.Controller.Tick(0f);
                Assert.That(obsolete.Token.IsCancellationRequested, Is.True);
                Assert.That(f.Controller.Phase, Is.EqualTo(GameplayPhase.BindingViews));
                Assert.That(f.Provider.Players.Count, Is.EqualTo(2));
                if (fail)
                {
                    obsolete.Source.TrySetException(new InvalidOperationException("late obsolete character failure"));
                }
                else
                {
                    obsolete.Source.TrySetResult(f.Provider.Prefab);
                }
                yield return null;
                f.Controller.Tick(0f);
                Assert.That(Player(SimulationOf(f.Controller)).Has<TransformComponent>(), Is.False);
                f.Provider.Players[1].Source.TrySetResult(f.Provider.Prefab);
                f.Controller.Tick(0f);
                Assert.That(f.Controller.Phase, Is.EqualTo(GameplayPhase.Playing));
                Assert.That(ActiveBoard(f).Cast<Transform>().Count(t => t.name.StartsWith("Player")), Is.EqualTo(1));
            }
        }

        [UnityTest]
        public IEnumerator DisposalObservesIgnoringProviderWithoutCreatingAnOrphan()
        {
            foreach (bool fail in new[] { false, true })
            {
                Fixture f = Create();
                EnterSelection(f);
                Confirm(f, SkinId.Alternate);
                Request pending = f.Provider.Players.Single();
                f.Controller.Dispose();
                _controllers.Remove(f.Controller);
                Assert.That(pending.Token.IsCancellationRequested, Is.True);
                if (fail)
                {
                    pending.Source.TrySetException(new InvalidOperationException("late disposed character failure"));
                }
                else
                {
                    pending.Source.TrySetResult(f.Provider.Prefab);
                }
                yield return null;
                Assert.That(f.Root.childCount, Is.Zero);
            }
        }

        [Test]
        public void NewEntriesAndReplaySelectAgainWhileRestartsReuseConfirmedCharacter()
        {
            Fixture f = Create();
            f.Provider.Immediate = true;
            EnterSelection(f);
            Confirm(f, SkinId.Alternate);
            Assert.That(f.Controller.Phase, Is.EqualTo(GameplayPhase.Playing));
            f.Input.NextLevelPressed = true;
            f.Controller.Tick(0f);
            f.Input.NextLevelPressed = false;
            f.Controller.Tick(0f);
            Assert.That(f.Controller.Phase, Is.EqualTo(GameplayPhase.SelectingCharacter));
            Assert.That(f.Controller.SelectedCharacter, Is.EqualTo(SkinId.Alternate));
            Assert.That(Player(SimulationOf(f.Controller)).Has<TransformComponent>(), Is.False);
            Confirm(f, SkinId.Punk);
            f.Controller.RequestRestart();
            f.Controller.Tick(0f);
            f.Controller.Tick(0f);
            Assert.That(f.Controller.Phase, Is.EqualTo(GameplayPhase.Playing));
            Assert.That(Player(SimulationOf(f.Controller)).Get<ViewSkin>().Id, Is.EqualTo(SkinId.Punk));
            f.Controller.Tick(60f);
            f.Controller.Tick(0f);
            Assert.That(f.Controller.Phase, Is.EqualTo(GameplayPhase.Playing));
            Assert.That(f.Controller.RemainingSeconds, Is.EqualTo(60f));
            SimulationOf(f.Controller).World.Single(e => e.Has<BoxTag>()).Set(new GridPosition(5, 1));
            f.Controller.Tick(0f);
            f.Controller.Tick(0.75f);
            Assert.That(f.Controller.Phase, Is.EqualTo(GameplayPhase.Finished));
            f.Controller.RequestRestart();
            f.Controller.Tick(0f);
            f.Controller.Tick(0f);
            Assert.That(f.Controller.Phase, Is.EqualTo(GameplayPhase.SelectingCharacter));
            Assert.That(f.Controller.SelectedCharacter, Is.EqualTo(SkinId.Punk));
            Assert.That(f.Controller.Results, Is.Empty);
        }

        [Test]
        public void AutomaticProgressionUsesTheCurrentCatalogAndFallsBackWhenThePreviousCharacterIsAbsent()
        {
            Fixture f = Create();
            f.Provider.Immediate = true;
            EnterSelection(f);
            Confirm(f, SkinId.Alternate);
            f.Data.Views.Entries.Single().Skins.RemoveAll(entry => entry.Skin == SkinId.Alternate);
            SimulationOf(f.Controller).World.Single(e => e.Has<BoxTag>()).Set(new GridPosition(5, 1));
            f.Controller.Tick(0f);
            Assert.That(f.Controller.Phase, Is.EqualTo(GameplayPhase.Completed));
            f.Controller.Tick(0.75f);
            f.Controller.Tick(0f);
            Assert.That(f.Controller.Phase, Is.EqualTo(GameplayPhase.SelectingCharacter));
            Assert.That(f.Controller.LevelNumber, Is.EqualTo(2));
            Assert.That(f.Controller.Characters, Is.EqualTo(new[] { SkinId.Default, SkinId.Punk }));
            Assert.That(f.Controller.SelectedCharacter, Is.EqualTo(SkinId.Default));
            Assert.That(Player(SimulationOf(f.Controller)).Has<TransformComponent>(), Is.False);
            Confirm(f, (SkinId)999);
            Assert.That(f.Controller.Phase, Is.EqualTo(GameplayPhase.Playing));
            Assert.That(f.Provider.Players.Select(r => r.Key.Skin), Is.EqualTo(new[] { SkinId.Alternate, SkinId.Default }));
        }

        [Test]
        public void SelectionDoesNotReadGameplayShortcutsAndRestartHasConfirmationPriority()
        {
            Fixture f = Create();
            EnterSelection(f);
            f.Input.Move = MoveCommand.Right;
            f.Input.RestartPressed = true;
            f.Input.NextLevelPressed = true;
            f.Input.UndoPressed = true;
            f.Controller.Tick(10f);
            Assert.That(f.Controller.Phase, Is.EqualTo(GameplayPhase.SelectingCharacter));
            Assert.That(f.Input.Reads, Is.Zero);
            Assert.That(f.Controller.LevelNumber, Is.EqualTo(1));
            f.Controller.RequestConfirmCharacter();
            f.Controller.RequestRestart();
            f.Controller.Tick(0f);
            Assert.That(f.Provider.Players, Is.Empty);
            Assert.That(f.Controller.Phase, Is.EqualTo(GameplayPhase.BindingViews));
        }

        private Fixture Create()
        {
            var root = Object("Run root").transform;
            var provider = new Provider(Object("View prefab"));
            var input = new Input();
            GameplaySettings settings = TestGameplaySettings.Create();
            _objects.Add(settings);
            var data = new TestStaticData { Gameplay = settings };
            TestCharacterSelection.Configure(data, _objects);
            var catalog = ScriptableObject.CreateInstance<BackgroundCatalog>();
            _objects.Add(catalog);
            var backgrounds = TestBackgrounds.Create(data, Object("Background"), catalog);
            var factory = new LevelRunFactory(new LevelRoot(root), provider, new LevelPopulator(), new SimulationPipelineFactory(), new ViewPipelineFactory());
            var controller = new GameplayController(input, new Source(), new HintCoordinator(new HintService(new HintSolver()), provider), backgrounds, factory, data);
            _controllers.Add(controller);
            return new Fixture(controller, provider, input, root, data);
        }

        private GameObject Object(string name)
        {
            var target = new GameObject(name);
            _objects.Add(target);
            return target;
        }

        private static Simulation SimulationOf(GameplayController controller)
        {
            object run = typeof(GameplayController).GetField("_run", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            return (Simulation)run.GetType().GetField("_simulation", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(run);
        }

        private static Entity Player(Simulation simulation) => simulation.World.Single(e => e.Has<PlayerTag>());
        private static Transform ActiveBoard(Fixture f) => f.Root.Cast<Transform>().Single(t => t.gameObject.activeSelf);

        private static void EnterSelection(Fixture f)
        {
            f.Controller.Initialize();
            f.Controller.Tick(0f);
            Assert.That(f.Controller.Phase, Is.EqualTo(GameplayPhase.SelectingCharacter));
        }

        private static void Confirm(Fixture f, SkinId skin)
        {
            f.Controller.RequestCharacter(skin);
            f.Controller.RequestConfirmCharacter();
            f.Controller.Tick(0f);
        }

        private sealed class Fixture
        {
            public GameplayController Controller { get; }
            public Provider Provider { get; }
            public Input Input { get; }
            public Transform Root { get; }
            public TestStaticData Data { get; }

            public Fixture(GameplayController controller, Provider provider, Input input, Transform root, TestStaticData data)
            {
                Controller = controller;
                Provider = provider;
                Input = input;
                Root = root;
                Data = data;
            }
        }

        private sealed class Input : IPlayerInput
        {
            public bool IsNeutral { get; set; } = true;
            public bool RestartPressed { get; set; }
            public bool NextLevelPressed { get; set; }
            public bool UndoPressed { get; set; }
            public MoveCommand Move;
            public int Reads;
            public MoveCommand ReadMove()
            {
                Reads++;
                return Move;
            }
        }

        private sealed class Source : ILevelSource
        {
            public int Count => 2;
            public UniTask<LevelDefinition> LoadAsync(int index, CancellationToken token) =>
                UniTask.FromResult(LevelParser.Parse("#######\n#@-$-.#\n#######"));
        }

        private sealed class Provider : IViewProvider
        {
            public GameObject Prefab { get; }
            public List<Request> Players { get; } = new();
            public bool Immediate;
            public Provider(GameObject prefab) => Prefab = prefab;
            public UniTask<GameObject> LoadAsync(ViewKey key, CancellationToken token)
            {
                if (key.Kind != ViewKind.Player)
                {
                    return UniTask.FromResult(Prefab);
                }
                var request = new Request(key, token);
                Players.Add(request);
                return Immediate ? UniTask.FromResult(Prefab) : request.Source.Task;
            }
        }

        private sealed class Request
        {
            public ViewKey Key { get; }
            public CancellationToken Token { get; }
            public UniTaskCompletionSource<GameObject> Source { get; } = new();
            public Request(ViewKey key, CancellationToken token)
            {
                Key = key;
                Token = token;
            }
        }
    }
}
