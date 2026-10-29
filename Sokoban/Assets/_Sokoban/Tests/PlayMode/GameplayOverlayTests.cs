#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Sokoban.Core;
using Sokoban.Core.Commands;
using Sokoban.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Sokoban.Tests.PlayMode
{
    public sealed class GameplayOverlayTests
    {
        private const string Assets = "Assets/_Sokoban/Code/Unity/GameplayDebugOverlay/";
        private GameObject _object;
        private UIDocument _document;
        private GameplayDebugOverlay _overlay;
        private PanelSettings _panel;
        private Model _model;
        private Keyboard _keyboard;
        private Mouse _mouse;
        private InputSettings.EditorInputBehaviorInPlayMode _editorInput;
        private InputSettings.BackgroundBehavior _backgroundInput;

        private VisualElement Root =>
            _document.rootVisualElement;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            _backgroundInput = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
            _model = new Model();
            _object = new GameObject("Overlay fixture");
            _object.SetActive(false);
            _document = _object.AddComponent<UIDocument>();
            _panel = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>(Assets + "OverlayPanelSettings.asset"));
            _panel.sortingOrder = 100f;
            _document.panelSettings = _panel;
            _document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Assets + "GameplayOverlay.uxml");
            _overlay = _object.AddComponent<GameplayDebugOverlay>();
            _overlay.Construct(_model, _model);
            _object.SetActive(true);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_object != null)
            {
                Object.Destroy(_object);
            }
            if (_panel != null)
            {
                Object.Destroy(_panel);
            }
            InputSystem.RemoveDevice(_keyboard);
            InputSystem.RemoveDevice(_mouse);
            InputSystem.settings.editorInputBehaviorInPlayMode = _editorInput;
            InputSystem.settings.backgroundBehavior = _backgroundInput;
            yield return null;
        }

        [UnityTest]
        public IEnumerator ControlsForwardRequestsAndRecheckAvailabilityAtActivation()
        {
            foreach (string name in new[] { "restart", "undo", "hint", "nextBackground" })
            {
                Submit(Root.Q<Button>(name));
            }
            Assert.That(_model.Requests, Is.EqualTo(new[] { 1, 0, 1, 1, 1 }));

            // Availability can change between the visual refresh and input dispatch.
            _model.Available = false;
            foreach (string name in new[] { "restart", "undo", "hint", "nextBackground" })
            {
                Submit(Root.Q<Button>(name));
            }
            Assert.That(_model.Requests, Is.EqualTo(new[] { 1, 0, 1, 1, 1 }));
            yield return null;
            Assert.That(Root.Q<Button>("restart").enabledSelf, Is.False);
            Assert.That(Root.Q<Button>("hint").enabledSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator OwnerDisableAndDocumentRecreationDoNotDuplicateOrRetainCallbacks()
        {
            for (int index = 0; index < 3; index++)
            {
                Button button = Root.Q<Button>("restart");
                _overlay.enabled = false;
                Submit(button);
                Assert.That(_model.Requests[0], Is.EqualTo(index));
                _overlay.enabled = true;
                yield return null;
                Submit(Root.Q<Button>("restart"));
                Assert.That(_model.Requests[0], Is.EqualTo(index + 1));
            }

            VisualElement previousRoot = Root;
            Button previous = Root.Q<Button>("restart");
            _document.enabled = false;
            yield return null;
            _document.enabled = true;
            yield return null;
            yield return null;
            Assert.That(Root, Is.Not.SameAs(previousRoot));
            Submit(previous);
            Assert.That(_model.Requests[0], Is.EqualTo(3));
            Submit(Root.Q<Button>("restart"));
            Assert.That(_model.Requests[0], Is.EqualTo(4));

            Object.Destroy(_object);
            yield return null;
            Submit(previous);
            Assert.That(_model.Requests[0], Is.EqualTo(4));
        }

        [UnityTest]
        public IEnumerator FailedResultsBindingReleasesEarlierControlsAndAllowsCleanRebinding()
        {
            foreach (string missing in new[] { "playAgain", "rowTime", "confirmCharacter", "characterPortrait" })
            {
                VisualElement required = missing == "characterPortrait"
                    ? Root.Q<TemplateContainer>("characterChoiceTemplate").Q(missing)
                    : Root.Q(missing);
                VisualElement parent = required.parent;
                int index = parent.IndexOf(required);
                int[] before = (int[])_model.Requests.Clone();
                required.RemoveFromHierarchy();
                try
                {
                    var error = Assert.Throws<System.InvalidOperationException>(() => _overlay.Construct(_model, _model));
                    Assert.That(error.Message, Does.Contain(missing));
                }
                finally
                {
                    _overlay.enabled = false;
                }

                Assert.That(Root.Q("overlayRoot").ClassListContains("hidden"), Is.True);
                // Make controls interactive again so hiding cannot mask a leaked callback.
                Root.Q("overlayRoot").RemoveFromClassList("hidden");
                yield return null;
                foreach (string name in new[] { "restart", "undo", "hint", "nextBackground" })
                {
                    Submit(Root.Q<Button>(name));
                }
                Assert.That(_model.Requests, Is.EqualTo(before));

                parent.Insert(index, required);
                _overlay.enabled = true;
                yield return null;
                foreach (string name in new[] { "restart", "undo", "hint", "nextBackground" })
                {
                    Submit(Root.Q<Button>(name));
                }
                Assert.That(_model.Requests, Is.EqualTo(new[] { before[0] + 1, before[1],
                    before[2] + 1, before[3] + 1, before[4] + 1 }));

                _model.Phase = GameplayPhase.Finished;
                yield return null;
                Submit(Root.Q<Button>("playAgain"));
                Assert.That(_model.Requests[0], Is.EqualTo(before[0] + 2));
                _model.Phase = GameplayPhase.Playing;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator EnterAndSpaceActivateTheFocusedButtonExactlyOnce()
        {
            Root.Q<Button>("restart").Focus();
            yield return Press(Key.Enter);
            Assert.That(_model.Requests[0], Is.EqualTo(1));
            yield return Press(Key.Space);
            Assert.That(_model.Requests[0], Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator TabAndShiftTabNavigateWhileArrowsRemainGameplayMovement()
        {
            Button restart = Root.Q<Button>("restart");
            restart.Focus();
            yield return Press(Key.Tab);
            Assert.That(Root.focusController.focusedElement, Is.SameAs(Root.Q<Button>("undo")));
            yield return Press(Key.LeftShift, Key.Tab);
            Assert.That(Root.focusController.focusedElement, Is.SameAs(restart));

            var input = new KeyboardMoveInput();
            foreach (Key key in new[] { Key.UpArrow, Key.DownArrow, Key.LeftArrow, Key.RightArrow, Key.W, Key.A, Key.S, Key.D })
            {
                InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
                yield return null;
                _keyboard.MakeCurrent();
                Assert.That(input.ReadMove(), Is.Not.EqualTo(default(MoveCommand)), key.ToString());
                Assert.That(Root.focusController.focusedElement, Is.SameAs(restart), key.ToString());
                InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
                yield return null;
            }
            Assert.That(_model.Requests, Is.EqualTo(new[] { 0, 0, 0, 0, 0 }));
        }

        [UnityTest]
        public IEnumerator FocusDoesNotConsumeOrDuplicateRestartAndUndoShortcuts()
        {
            Root.Q<Button>("hint").Focus();
            var input = new KeyboardMoveInput();
            foreach (Key key in new[] { Key.R, Key.Z })
            {
                InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
                yield return null;
                _keyboard.MakeCurrent();
                Assert.That(key == Key.R ? input.RestartPressed : input.UndoPressed, Is.True);
                Assert.That(_model.Requests, Is.EqualTo(new[] { 0, 0, 0, 0, 0 }));
                InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator MouseClickRequestsOnceAndTransparentBoardAreaDoesNotPickUI()
        {
            Button button = Root.Q<Button>("restart");
            var observed = new List<string>();
            Root.RegisterCallback<PointerDownEvent>(evt => observed.Add($"down {evt.position} target {((VisualElement)evt.target).name}"), TrickleDown.TrickleDown);
            Root.RegisterCallback<PointerUpEvent>(evt => observed.Add($"up {evt.position} target {((VisualElement)evt.target).name}"), TrickleDown.TrickleDown);
            Vector2 center = button.worldBound.center;
            float scale = Screen.width / Root.worldBound.width;
            Vector2 pointer = new(center.x * scale, Screen.height - center.y * scale);
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = pointer });
            yield return null;
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = pointer, buttons = 1 });
            yield return null;
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = pointer, buttons = 0 });
            yield return null;
            yield return null;
            Assert.That(_model.Requests[0], Is.EqualTo(1), $"Pointer {pointer}; button {button.worldBound}; events {string.Join(", ", observed)}");
            Assert.That(Root.panel.Pick(Root.worldBound.center), Is.Null);
            Assert.That(Root.panel.Pick(new Vector2(20f, Root.worldBound.height - 20f)), Is.Null,
                "Empty space below a side card must also pass through.");
        }

        [UnityTest]
        public IEnumerator LoadingFailureRecoveryAndHintOutcomesRetainTheSameControls()
        {
            Button hint = Root.Q<Button>("hint");
            foreach (GameplayPhase phase in new[] { GameplayPhase.Loading, GameplayPhase.BindingViews, GameplayPhase.ViewsFailed, GameplayPhase.Playing })
            {
                _model.Phase = phase;
                _model.Available = phase != GameplayPhase.Loading;
                yield return null;
                Assert.That(Root.Q<Button>("hint"), Is.SameAs(hint));
                Assert.That(hint.enabledSelf, Is.EqualTo(_model.CanHint));
            }
            foreach (HintStatus status in new[] { HintStatus.Calculating, HintStatus.Available, HintStatus.NoSolution,
                         HintStatus.BudgetExhausted, HintStatus.Failed, HintStatus.AlreadySolved })
            {
                _model.HintStatus = status;
                yield return null;
                int before = _model.Requests[3];
                Submit(hint);
                Assert.That(_model.Requests[3], Is.EqualTo(before + (status == HintStatus.Calculating ? 0 : 1)));
                Assert.That(Root.Q<Label>("hintMessage").text, Is.Not.Empty);
            }
        }

        [UnityTest]
        public IEnumerator ResultsPersistWithoutRebuildingAndPlayAgainStillRequestsRestart()
        {
            _model.Results.Add(new LevelResult(6, 10f, 50f));
            _model.Phase = GameplayPhase.Completed;
            yield return null;
            VisualElement first = Root.Q("resultRows")[0];
            _model.Phase = GameplayPhase.Finished;
            _model.Moves = 20;
            yield return null;
            Assert.That(Root.Q("resultRows")[0], Is.SameAs(first));
            _model.Results.Add(new LevelResult(12, 20f, 40f));
            yield return null;
            Assert.That(Root.Q("resultRows").childCount, Is.EqualTo(2));
            Assert.That(Root.Q<Label>("totalMoves").text, Is.EqualTo("18"));
            Assert.That(Root.Q<Label>("totalTime").text, Is.EqualTo($"{30f:F1} s"));
            Submit(Root.Q<Button>("playAgain"));
            Assert.That(_model.Requests[0], Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator OverflowResultsScrollWithoutCompressingRowsOrCoveringControls()
        {
            _model.Phase = GameplayPhase.Finished;
            for (int index = 0; index < 20; index++)
            {
                _model.Results.Add(new LevelResult(index + 1, index + 2, 0f));
            }
            Root.Q("overlayRoot").AddToClassList("compact");
            yield return null;
            yield return null;
            ScrollView scroll = Root.Q<ScrollView>("resultsScroll");
            Assert.That(scroll.verticalScroller.highValue, Is.GreaterThan(0f));
            Assert.That(scroll.contentViewport.worldBound.height, Is.LessThanOrEqualTo(scroll.worldBound.height));
            VisualElement rows = Root.Q("resultRows");
            Assert.That(rows[1].worldBound.yMin, Is.GreaterThanOrEqualTo(rows[0].worldBound.yMax));
            Assert.That(scroll.worldBound.Overlaps(Root.Q("controlsPanel").worldBound), Is.False);
            Assert.That(scroll.worldBound.Overlaps(Root.Q("playAgain").worldBound), Is.False);
            Vector2 center = scroll.contentViewport.worldBound.center;
            float scale = Screen.width / Root.worldBound.width;
            Vector2 pointer = new(center.x * scale, Screen.height - center.y * scale);
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = pointer });
            yield return null;
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = pointer, scroll = new Vector2(0f, -120f) });
            yield return null;
            yield return null;
            Assert.That(scroll.scrollOffset.y, Is.GreaterThan(0f), "The visible result rows must receive wheel input.");
            scroll.scrollOffset = new Vector2(0f, scroll.verticalScroller.highValue);
            yield return null;
            Assert.That(rows[19].worldBound.yMax, Is.LessThanOrEqualTo(scroll.contentViewport.worldBound.yMax + 1f));
        }

        [UnityTest]
        public IEnumerator CompletedNoticeIsSeparateAndDoesNotRequestOrBlockProgression()
        {
            _model.Results.Add(new LevelResult(6, 10f, 50f));
            _model.Phase = GameplayPhase.Completed;
            yield return null;
            yield return null;
            Assert.That(Root.Q("completed").resolvedStyle.display, Is.Not.EqualTo(DisplayStyle.None));
            Assert.That(Root.Q("results").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(Root.Q("completed").Query<Button>().ToList(), Is.Empty);
            Assert.That(Root.panel.Pick(Root.worldBound.center), Is.Null);
            Assert.That(_model.Requests, Is.EqualTo(new[] { 0, 0, 0, 0, 0 }));

            _model.Phase = GameplayPhase.Playing;
            yield return null;
            Assert.That(Root.Q("completed").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
        }

        [UnityTest]
        public IEnumerator FinishedColumnsAlignAndTimeValuesStayOnOneLine()
        {
            _model.Phase = GameplayPhase.Finished;
            _model.Results.Add(new LevelResult(6, 10f, 50f));
            _model.Results.Add(new LevelResult(12, 20f, 40f));
            yield return null;
            yield return null;
            VisualElement header = Root.Q("resultHeader");
            VisualElement row = Root.Q("resultRows")[1];
            Assert.That(row.Q<Label>("rowLevel").text, Is.EqualTo("02"));
            Assert.That(row.Q<Label>("rowMoves").text, Is.EqualTo("12"));
            Assert.That(row.Q<Label>("rowTime").text, Is.EqualTo($"{20f:F1} s"));
            Assert.That(row.Q("rowLevel").worldBound.xMin,
                Is.EqualTo(header.Q(className: "result-level").worldBound.xMin).Within(1f));
            Assert.That(row.Q("rowMoves").worldBound.xMax,
                Is.EqualTo(header.Q(className: "result-moves").worldBound.xMax).Within(1f));
            Assert.That(row.Q("rowTime").worldBound.xMax,
                Is.EqualTo(header.Q(className: "result-time").worldBound.xMax).Within(1f));
            Assert.That(row.Q("rowTime").worldBound.height,
                Is.EqualTo(row.Q("rowMoves").worldBound.height).Within(1f));
            Assert.That(Root.Q("resultsPanel").worldBound.Overlaps(Root.Q("controlsPanel").worldBound), Is.False);
        }

        [UnityTest]
        public IEnumerator PlayAgainUsesTheExistingRequestAndReleasesItsCallback()
        {
            _model.Phase = GameplayPhase.Finished;
            yield return null;
            Button playAgain = Root.Q<Button>("playAgain");
            playAgain.Focus();
            yield return Press(Key.Enter);
            yield return Press(Key.Space);
            Assert.That(_model.Requests[0], Is.EqualTo(2));
            _overlay.enabled = false;
            Submit(playAgain);
            Assert.That(_model.Requests[0], Is.EqualTo(2));
            _overlay.enabled = true;
            yield return null;
            Submit(Root.Q<Button>("playAgain"));
            Assert.That(_model.Requests[0], Is.EqualTo(3));
            _model.Available = false;
            Submit(Root.Q<Button>("playAgain"));
            Assert.That(_model.Requests[0], Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator BackgroundFailureAndRetryUseTheSameRequestBoundary()
        {
            Root.Q<Foldout>("debug").value = true;
            _model.BackgroundStatus = BackgroundStatus.Failed;
            yield return null;
            Submit(Root.Q<Button>("nextBackground"));
            Assert.That(_model.Requests[4], Is.EqualTo(1));
            _model.BackgroundStatus = BackgroundStatus.Loading;
            yield return null;
            Submit(Root.Q<Button>("nextBackground"));
            Assert.That(_model.Requests[4], Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator CharacterChoicesSupportKeyboardAndMouseWithoutGameplayNavigationSuppression()
        {
            _model.Phase = GameplayPhase.SelectingCharacter;
            yield return null;
            yield return null;
            var choices = Root.Q("characterChoices").Query<Button>().ToList();
            Assert.That(choices.Count, Is.EqualTo(_model.Characters.Count));
            Assert.That(Root.focusController.focusedElement, Is.SameAs(choices[0]));
            yield return Press(Key.RightArrow);
            Assert.That(Root.focusController.focusedElement, Is.SameAs(choices[1]));
            yield return Press(Key.Enter);
            Assert.That(_model.SelectedCharacter, Is.EqualTo(SkinId.Alternate));
            Assert.That(_model.Requests[1], Is.EqualTo(1));
            yield return Press(Key.Tab);
            yield return Press(Key.Tab);
            Assert.That(Root.focusController.focusedElement, Is.SameAs(Root.Q<Button>("confirmCharacter")));
            yield return Press(Key.Space);
            Assert.That(_model.Confirmations, Is.EqualTo(1));

            Vector2 center = choices[2].worldBound.center;
            float scale = Screen.width / Root.worldBound.width;
            Vector2 pointer = new(center.x * scale, Screen.height - center.y * scale);
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = pointer });
            yield return null;
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = pointer, buttons = 1 });
            yield return null;
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = pointer, buttons = 0 });
            yield return null;
            yield return null;
            Assert.That(_model.SelectedCharacter, Is.EqualTo(SkinId.Punk));
            Assert.That(_model.Requests[1], Is.EqualTo(2));
            Assert.That(Root.Q<Button>("skin"), Is.Null);

            _model.Available = false;
            Submit(Root.Q<Button>("confirmCharacter"));
            Assert.That(_model.Confirmations, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator CharacterCallbacksAndScheduledFocusAreReleasedWithTheirOwner()
        {
            _model.Phase = GameplayPhase.SelectingCharacter;
            yield return null;
            yield return null;
            Button previous = Root.Q("characterChoices").Query<Button>().ToList()[1];
            Button confirm = Root.Q<Button>("confirmCharacter");
            _overlay.enabled = false;
            Root.Q("overlayRoot").RemoveFromClassList("hidden");
            yield return null;
            Submit(previous);
            Submit(confirm);
            Assert.That(_model.Requests[1], Is.Zero);
            Assert.That(_model.Confirmations, Is.Zero);
            _overlay.enabled = true;
            yield return null;
            Submit(Root.Q("characterChoices").Query<Button>().ToList()[1]);
            Assert.That(_model.Requests[1], Is.EqualTo(1));
            _model.Phase = GameplayPhase.BindingCharacter;
            yield return null;
            Assert.That(Root.Q<Button>("confirmCharacter").enabledSelf, Is.False);
            _model.Phase = GameplayPhase.CharacterFailed;
            yield return null;
            Assert.That(Root.Q<Button>("confirmCharacter").enabledSelf, Is.True);
            Submit(Root.Q<Button>("confirmCharacter"));
            Assert.That(_model.Confirmations, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SelectionModalOwnsPickingAndHidesGameplayAcrossSelectionLoadingAndFailure()
        {
            foreach (GameplayPhase phase in new[] { GameplayPhase.SelectingCharacter, GameplayPhase.BindingCharacter, GameplayPhase.CharacterFailed })
            {
                _model.Phase = phase;
                yield return null;
                yield return null;
                Assert.That(Root.Q("statusPanel").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
                Assert.That(Root.Q("controlsScroll").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
                Assert.That(Root.panel.Pick(new Vector2(5f, 5f)), Is.SameAs(Root.Q("characterSelection")));
                Rect window = Root.Q("selectionWindow").worldBound;
                Assert.That(window.center.x, Is.EqualTo(Root.worldBound.center.x).Within(1f));
                Assert.That(window.center.y, Is.EqualTo(Root.worldBound.center.y).Within(1f));
                Assert.That(Root.worldBound.Contains(window.min), Is.True);
                Assert.That(Root.worldBound.Contains(window.max), Is.True);
                Assert.That(Root.Q<Button>("confirmCharacter").worldBound.yMax, Is.LessThan(window.yMax));
            }

            _model.Phase = GameplayPhase.Playing;
            yield return null;
            yield return null;
            Assert.That(Root.Q("statusPanel").resolvedStyle.display, Is.Not.EqualTo(DisplayStyle.None));
            Assert.That(Root.Q("controlsScroll").resolvedStyle.display, Is.Not.EqualTo(DisplayStyle.None));
            Assert.That(Root.panel.Pick(Root.worldBound.center), Is.Null);
        }

        [UnityTest]
        public IEnumerator ClonedCardsUseSerializedPortraitsAndKeepSelectionIndependentOfFocus()
        {
            _model.Phase = GameplayPhase.SelectingCharacter;
            yield return null;
            yield return null;
            var portraits = Root.Q("characterPortraits").Query<CharacterPortrait>().ToList();
            var choices = Root.Q("characterChoices").Query<Button>().ToList();
            for (int index = 0; index < choices.Count; index++)
            {
                SkinId skin = _model.Characters[index];
                CharacterPortrait source = portraits.Find(portrait => portrait.Skin == skin);
                Assert.That(source.Portrait, Is.Not.Null);
                Assert.That(choices[index].Q<Image>("characterPortrait").image, Is.SameAs(source.Portrait));
            }
            VisualElement previous = choices[0];
            choices[1].Focus();
            yield return null;
            Assert.That(previous.ClassListContains("selected"), Is.True);
            Assert.That(choices[1].ClassListContains("selected"), Is.False);
            Submit(choices[1]);
            yield return null;
            Assert.That(previous.ClassListContains("selected"), Is.False);
            Assert.That(choices[1].ClassListContains("selected"), Is.True);
            Assert.That(Root.Q("characterChoices").Query<Button>().ToList()[0], Is.SameAs(previous));

            string[] dependencies = AssetDatabase.GetDependencies(Assets + "GameplayOverlay.uxml", true);
            foreach (CharacterPortrait portrait in portraits)
            {
                Assert.That(dependencies, Does.Contain(AssetDatabase.GetAssetPath(portrait.Portrait)));
            }
        }

        [UnityTest]
        public IEnumerator SelectionAndGameplayTextProduceVisibleGlyphs()
        {
            foreach (GameplayPhase phase in new[] { GameplayPhase.SelectingCharacter, GameplayPhase.BindingCharacter,
                         GameplayPhase.CharacterFailed, GameplayPhase.Playing })
            {
                _model.Phase = phase;
                yield return null;
                yield return null;

                var text = new List<TextElement>();
                if (phase == GameplayPhase.Playing)
                {
                    text.Add(Root.Q<Label>("level"));
                    text.Add(Root.Q<Label>("remaining"));
                    text.Add(Root.Q<Label>("moves"));
                    text.Add(Root.Q<Button>("restart").Q<Label>("restartTitle"));
                }
                else
                {
                    text.Add(Root.Q<Label>(className: "selection-title"));
                    text.AddRange(Root.Q("characterChoices").Query<Label>("characterName").ToList());
                    text.Add(Root.Q("characterChoices").Q(className: "selected").Q(className: "selection-indicator").Q<Label>());
                    text.Add(Root.Q<Label>("characterFeedback"));
                    text.Add(Root.Q<Button>("confirmCharacter"));
                }

                Texture2D visible = null;
                Texture2D hidden = null;
                var colors = new List<StyleColor>();
                try
                {
                    foreach (TextElement element in text)
                    {
                        Assert.That(element.resolvedStyle.unityFontDefinition.fontAsset, Is.Not.Null, element.text);
                        Assert.That(element.worldBound.width, Is.GreaterThan(0f), element.text);
                        Assert.That(element.worldBound.height, Is.GreaterThan(0f), element.text);
                        colors.Add(element.style.color);
                    }

                    yield return new WaitForEndOfFrame();
                    visible = ScreenCapture.CaptureScreenshotAsTexture();
                    foreach (TextElement element in text)
                    {
                        element.style.color = Color.clear;
                    }
                    yield return null;
                    yield return new WaitForEndOfFrame();
                    hidden = ScreenCapture.CaptureScreenshotAsTexture();
                    Color32[] before = visible.GetPixels32();
                    Color32[] after = hidden.GetPixels32();
                    float scale = visible.width / Root.worldBound.width;
                    foreach (TextElement element in text)
                    {
                        Rect bounds = element.worldBound;
                        int changed = 0;
                        int minX = Mathf.Clamp(Mathf.FloorToInt(bounds.xMin * scale), 0, visible.width);
                        int maxX = Mathf.Clamp(Mathf.CeilToInt(bounds.xMax * scale), 0, visible.width);
                        int minY = Mathf.Clamp(visible.height - Mathf.CeilToInt(bounds.yMax * scale), 0, visible.height);
                        int maxY = Mathf.Clamp(visible.height - Mathf.FloorToInt(bounds.yMin * scale), 0, visible.height);
                        for (int y = minY; y < maxY; y++)
                        {
                            for (int x = minX; x < maxX; x++)
                            {
                                int index = y * visible.width + x;
                                if (Mathf.Abs(before[index].r - after[index].r)
                                    + Mathf.Abs(before[index].g - after[index].g)
                                    + Mathf.Abs(before[index].b - after[index].b) > 24)
                                {
                                    changed++;
                                }
                            }
                        }
                        Assert.That(changed, Is.GreaterThan(8), $"{phase}: '{element.text}' has no visible glyph pixels.");
                    }
                }
                finally
                {
                    for (int index = 0; index < colors.Count; index++)
                    {
                        text[index].style.color = colors[index];
                    }
                    if (visible != null)
                    {
                        Object.Destroy(visible);
                    }
                    if (hidden != null)
                    {
                        Object.Destroy(hidden);
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator RebuildingChoicesReleasesOldBindingsWithoutDuplicatingNewOnes()
        {
            _model.Phase = GameplayPhase.SelectingCharacter;
            yield return null;
            yield return null;
            Button previous = Root.Q(UiIds.Selection.Choices).Query<Button>().ToList()[1];
            _model.Characters.RemoveAt(0);
            _model.SelectedCharacter = SkinId.Alternate;
            yield return null;
            yield return null;
            var choices = Root.Q(UiIds.Selection.Choices).Query<Button>().ToList();
            Assert.That(choices.Count, Is.EqualTo(2));
            Assert.That(choices[0], Is.Not.SameAs(previous));
            Root.Add(previous);
            try
            {
                Submit(previous);
                Assert.That(_model.Requests[1], Is.Zero, "Detached choices must release their callbacks, even if attached again.");
                Submit(choices[0]);
                Assert.That(_model.Requests[1], Is.EqualTo(1));
                _model.Available = false;
                Submit(choices[1]);
                Assert.That(_model.Requests[1], Is.EqualTo(1), "Availability is rechecked before the next visual refresh.");
            }
            finally
            {
                previous.RemoveFromHierarchy();
            }
        }

        [UnityTest]
        public IEnumerator HudPreservesFormattingTenthsAndIndependentLabelCaches()
        {
            Label level = Root.Q<Label>(UiIds.Hud.Level);
            Label remaining = Root.Q<Label>(UiIds.Hud.Remaining);
            Assert.That(level.text, Is.EqualTo("01 / 03"));
            Assert.That(remaining.text, Is.EqualTo($"{60f:F1} s"));
            level.text = "cached level";
            remaining.text = "cached time";
            _model.Moves = 5;
            _model.RemainingSeconds = 60.04f;
            yield return null;
            Assert.That(Root.Q<Label>(UiIds.Hud.Moves).text, Is.EqualTo("5"));
            Assert.That(level.text, Is.EqualTo("cached level"));
            Assert.That(remaining.text, Is.EqualTo("cached time"), "Time is cached at rounded tenths.");
            _model.RemainingSeconds = 59.94f;
            yield return null;
            Assert.That(remaining.text, Is.EqualTo($"{59.9f:F1} s"));
            Assert.That(level.text, Is.EqualTo("cached level"));
            _model.LevelNumber = 2;
            _model.LevelCount = 0;
            yield return null;
            Assert.That(level.text, Is.EqualTo("02 / ?"));
            Assert.That(remaining.text, Is.EqualTo("--"));
            Assert.That(Root.Q<Label>(UiIds.Hud.Moves).text, Is.EqualTo("5"));
        }

        [UnityTest]
        public IEnumerator EveryPhasePreservesInitialAndTransitionVisibilityAndCompactLayout()
        {
            var cases = new[]
            {
                (Phase: GameplayPhase.Loading, Class: UiIds.Phases.Loading, Notice: "Loading level…",
                    Hud: true, Selection: false, Completed: false, Results: false, CompactTop: 248f),
                (Phase: GameplayPhase.BindingViews, Class: UiIds.Phases.BindingViews, Notice: "Preparing board…",
                    Hud: true, Selection: false, Completed: false, Results: false, CompactTop: 248f),
                (Phase: GameplayPhase.Playing, Class: UiIds.Phases.Playing, Notice: "",
                    Hud: true, Selection: false, Completed: false, Results: false, CompactTop: 164f),
                (Phase: GameplayPhase.Completed, Class: UiIds.Phases.Completed, Notice: "",
                    Hud: true, Selection: false, Completed: true, Results: false, CompactTop: 248f),
                (Phase: GameplayPhase.ViewsFailed, Class: UiIds.Phases.ViewsFailed, Notice: "Views unavailable. Restart to retry.",
                    Hud: true, Selection: false, Completed: false, Results: false, CompactTop: 248f),
                (Phase: GameplayPhase.LevelFailed, Class: UiIds.Phases.LevelFailed, Notice: "Level unavailable. Restart to retry.",
                    Hud: true, Selection: false, Completed: false, Results: false, CompactTop: 248f),
                (Phase: GameplayPhase.Finished, Class: UiIds.Phases.Finished, Notice: "",
                    Hud: false, Selection: false, Completed: false, Results: true, CompactTop: 24f),
                (Phase: GameplayPhase.SelectingCharacter, Class: UiIds.Phases.SelectingCharacter, Notice: "",
                    Hud: false, Selection: true, Completed: false, Results: false, CompactTop: 164f),
                (Phase: GameplayPhase.BindingCharacter, Class: UiIds.Phases.BindingCharacter, Notice: "",
                    Hud: false, Selection: true, Completed: false, Results: false, CompactTop: 164f),
                (Phase: GameplayPhase.CharacterFailed, Class: UiIds.Phases.CharacterFailed, Notice: "",
                    Hud: false, Selection: true, Completed: false, Results: false, CompactTop: 164f)
            };
            foreach (bool rebind in new[] { false, true })
            {
                foreach (var expected in cases)
                {
                    _model.Phase = expected.Phase;
                    if (rebind)
                    {
                        _overlay.Construct(_model, _model);
                    }
                    yield return null;
                    yield return null;
                    Assert.That(Root.ClassListContains(expected.Class), Is.True, expected.Phase.ToString());
                    int phaseClasses = 0;
                    foreach (string name in Root.GetClasses())
                    {
                        if (name.StartsWith("phase-", System.StringComparison.Ordinal))
                        {
                            phaseClasses++;
                        }
                    }
                    Assert.That(phaseClasses, Is.EqualTo(1), "Exactly one phase class belongs to the shared overlay root.");
                    Assert.That(Root.Q<Label>(UiIds.Hud.Notice).text, Is.EqualTo(expected.Notice));
                    CheckDisplay(UiIds.Hud.Panel, expected.Hud);
                    CheckDisplay(UiIds.Controls.Scroll, expected.Selection == false);
                    CheckDisplay(UiIds.Selection.Panel, expected.Selection);
                    CheckDisplay(UiIds.Results.Completed, expected.Completed);
                    CheckDisplay(UiIds.Results.Panel, expected.Results);
                    CheckDisplay(UiIds.Hud.Notice, expected.Notice.Length > 0);
                    Root.Q(UiIds.Root.Name).AddToClassList(UiIds.Classes.Compact);
                    yield return null;
                    if (expected.Selection == false)
                    {
                        Assert.That(Root.Q(UiIds.Controls.Scroll).resolvedStyle.top, Is.EqualTo(expected.CompactTop).Within(1f));
                    }
                    Root.Q(UiIds.Root.Name).RemoveFromClassList(UiIds.Classes.Compact);
                }
            }

            void CheckDisplay(string name, bool visible)
            {
                Assert.That(Root.Q(name).resolvedStyle.display,
                    Is.EqualTo(visible ? DisplayStyle.Flex : DisplayStyle.None), $"{_model.Phase}: {name}");
            }
        }

        [Test]
        public void RealOverlayAndClonedTemplatesSatisfyTheBindingContract()
        {
            VisualElement tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Assets + "GameplayOverlay.uxml").CloneTree();
            Assert.That(BindingErrors(tree), Is.Empty, "Required UI elements must retain their names, types and scopes.");
        }

        [Test]
        public void BindingContractReportsMissingAndIncorrectlyTypedElementsTogether()
        {
            VisualElement tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Assets + "GameplayOverlay.uxml").CloneTree();
            tree.Q(UiIds.Hud.Level).RemoveFromHierarchy();
            VisualElement hint = tree.Q(UiIds.Controls.Hint);
            var replacement = new VisualElement { name = hint.name };
            foreach (VisualElement child in new List<VisualElement>(hint.Children()))
            {
                replacement.Add(child);
            }
            hint.parent.Add(replacement);
            hint.RemoveFromHierarchy();
            List<string> errors = BindingErrors(tree);
            Assert.That(errors.Count, Is.EqualTo(2));
            Assert.That(errors[0], Does.Contain(UiIds.Hud.Level).And.Contain(nameof(Label)));
            Assert.That(errors[1], Does.Contain(UiIds.Controls.Hint).And.Contain(nameof(Button)));
        }

        private static List<string> BindingErrors(VisualElement tree)
        {
            var errors = new List<string>();
            Check<VisualElement>(tree, UiIds.Root.Name);
            foreach (string name in new[] { UiIds.Hud.Level, UiIds.Hud.Remaining, UiIds.Hud.Moves, UiIds.Hud.Notice,
                         UiIds.Controls.PhaseDetail, UiIds.Controls.HintMessage, UiIds.Controls.HintTitle,
                         UiIds.Background.Label, UiIds.Background.Title, UiIds.Results.CompletedStats,
                         UiIds.Results.TotalMoves, UiIds.Results.TotalTime })
            {
                Check<Label>(tree, name);
            }
            foreach (string name in new[] { UiIds.Controls.Restart, UiIds.Controls.Undo, UiIds.Controls.Hint,
                         UiIds.Background.Next, UiIds.Results.PlayAgain })
            {
                Check<Button>(tree, name);
            }
            Check<VisualElement>(tree, UiIds.Results.Panel);
            Check<VisualElement>(tree, UiIds.Results.Completed);
            Check<VisualElement>(tree, UiIds.Results.Rows);
            Check<VisualElement>(tree, UiIds.Background.Controls);
            TemplateContainer result = Check<TemplateContainer>(tree, UiIds.Results.Template);
            CheckRow(result);
            if (result?.templateSource != null)
            {
                CheckRow(result.templateSource.CloneTree());
            }
            else
            {
                errors.Add("Result template source is missing.");
            }
            VisualElement selection = Check<VisualElement>(tree, UiIds.Selection.Panel);
            Check<VisualElement>(selection, UiIds.Selection.Choices);
            Check<VisualElement>(selection, UiIds.Selection.Portraits);
            Check<Label>(selection, UiIds.Selection.Feedback);
            Check<Button>(selection, UiIds.Selection.Confirm);
            TemplateContainer choice = Check<TemplateContainer>(selection, UiIds.Selection.Template);
            CheckCard(choice);
            if (choice?.templateSource != null)
            {
                CheckCard(choice.templateSource.CloneTree());
            }
            else
            {
                errors.Add("Choice template source is missing.");
            }
            return errors;

            T Check<T>(VisualElement scope, string name) where T : VisualElement
            {
                if (scope == null)
                {
                    errors.Add($"Missing scope for '{name}' ({typeof(T).Name}).");
                    return null;
                }
                try
                {
                    return UiElements.Require<T>(scope, name);
                }
                catch (System.InvalidOperationException error)
                {
                    errors.Add(error.Message);
                    return null;
                }
            }

            void CheckRow(VisualElement scope)
            {
                Check<Label>(scope, UiIds.ResultRow.Level);
                Check<Label>(scope, UiIds.ResultRow.Moves);
                Check<Label>(scope, UiIds.ResultRow.Time);
            }

            void CheckCard(VisualElement scope)
            {
                Button button = Check<Button>(scope, UiIds.Choice.Button);
                Check<Image>(button, UiIds.Choice.Portrait);
                Check<Label>(button, UiIds.Choice.Name);
            }
        }

        private IEnumerator Press(params Key[] keys)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keys));
            yield return null;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null;
        }

        private static void Submit(VisualElement target)
        {
            using var evt = NavigationSubmitEvent.GetPooled();
            evt.target = target;
            target.SendEvent(evt);
        }

        private sealed class Model : IGameplayStatus, IGameplayCommands
        {
            public bool Available = true;
            public int[] Requests { get; } = new int[5];
            public GameplayPhase Phase { get; set; } = GameplayPhase.Playing;
            public int Moves { get; set; }
            public int LevelCount { get; set; } = 3;
            public int LevelNumber { get; set; } = 1;
            public float RemainingSeconds { get; set; } = 60f;
            public BackgroundStatus BackgroundStatus { get; set; } = BackgroundStatus.Ready;
            public string BackgroundName => "Cumulus 04";
            public HintStatus HintStatus { get; set; }
            public MoveCommand HintMove => MoveCommand.Up;
            public List<LevelResult> Results { get; } = new();
            IReadOnlyList<LevelResult> IGameplayStatus.Results => Results;
            public bool CanRestart => Available;
            public List<SkinId> Characters { get; } = new() { SkinId.Default, SkinId.Alternate, SkinId.Punk };
            IReadOnlyList<SkinId> IGameplayStatus.Characters => Characters;
            public SkinId SelectedCharacter { get; set; }
            public int Confirmations;
            public bool CanChooseCharacter => Available && Phase is GameplayPhase.SelectingCharacter or GameplayPhase.CharacterFailed;
            public bool CanConfirmCharacter => CanChooseCharacter;
            public bool CanUndo => Available && Phase == GameplayPhase.Playing;
            public bool CanHint => Available && Phase == GameplayPhase.Playing && HintStatus != HintStatus.Calculating;
            public bool CanNextBackground => Available;
            public void RequestRestart() => Requests[0]++;
            public void RequestCharacter(SkinId skin)
            {
                Requests[1]++;
                SelectedCharacter = skin;
            }
            public void RequestConfirmCharacter() => Confirmations++;
            public void RequestUndo() => Requests[2]++;
            public void RequestHint() => Requests[3]++;
            public void RequestNextBackground() => Requests[4]++;
        }
    }
}
#endif
