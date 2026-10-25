using Zenject;
using UnityEngine;
using Sokoban.Core;

namespace Sokoban.Presentation
{
    public sealed class GameplayDebugOverlay : MonoBehaviour
    {
        [SerializeField, Range(16, 40)] private int fontSize = 24;

        private IGameplayStatus _status;
        private IGameplayCommands _commands;
        private Vector2 _scroll;
        private GUIStyle _labelStyle;
        private GUIStyle _buttonStyle;

        private GUIStyle LabelStyle => _labelStyle ??= new GUIStyle(GUI.skin.label)
        {
            wordWrap = true,
            margin = new RectOffset(4, 4, 3, 3)
        };

        [Inject]
        public void Construct(IGameplayStatus status, IGameplayCommands commands)
        {
            _status = status;
            _commands = commands;
        }

        private void OnGUI()
        {
            LabelStyle.fontSize = fontSize;
            
            GUILayout.BeginArea(new Rect(12, 12, 480, Screen.height - 24));
            GUILayout.BeginVertical("box");
            
            Label($"Phase: {_status.Phase}");
            string count = _status.LevelCount > 0 ? _status.LevelCount.ToString() : "?";
            
            Label($"Level {_status.LevelNumber} / {count}");
            string remaining = _status.LevelCount > 0 ? _status.RemainingSeconds.ToString("F1") : "--";
            
            Label($"Remaining: {remaining} s");
            Label($"Moves: {_status.Moves}");
            
            if (_status.Phase == GameplayPhase.Finished)
            {
                var totalMoves = 0;
                var totalSeconds = 0f;
                _scroll = GUILayout.BeginScrollView(_scroll);
                
                for (int index = 0; index < _status.Results.Count; index++)
                {
                    LevelResult result = _status.Results[index];
                    Label($"{index + 1}. {result.Moves} moves, {result.ElapsedSeconds:F1} s");
                    totalMoves += result.Moves;
                    totalSeconds += result.ElapsedSeconds;
                }
                
                GUILayout.EndScrollView();
                Label($"Total: {totalMoves} moves, {totalSeconds:F1} s");
                Label("Press R to play again");
            }
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
            
            DrawControls();
        }

        private void DrawControls()
        {
            _buttonStyle ??= new GUIStyle(GUI.skin.button);
            _buttonStyle.fontSize = fontSize;
            
            float width = Mathf.Min(250f, Screen.width - 24f);
            
            GUILayout.BeginArea(new Rect(Screen.width - width - 12f, 12f, width, 150f));
            GUILayout.BeginVertical("box");
            
            bool isGuiEnabled = GUI.enabled;
            
            try
            {
                GUI.enabled = isGuiEnabled && _commands.CanRestart;
                string restart = _status.Phase == GameplayPhase.Finished ? "Play again (R)" : "Restart (R)";
                if (GUILayout.Button(restart, _buttonStyle, GUILayout.Height(52f)))
                {
                    _commands.RequestRestart();
                }

                GUI.enabled = isGuiEnabled && _commands.CanSwitchSkin;
                if (GUILayout.Button("Switch skin (K)", _buttonStyle, GUILayout.Height(52f)))
                {
                    _commands.RequestNextSkin();
                }
            }
            finally
            {
                GUI.enabled = isGuiEnabled;
                GUILayout.EndVertical();
                GUILayout.EndArea();
            }
        }

        private void Label(string text) => 
            GUILayout.Label(text, _labelStyle);
    }
}
