using UnityEditor;
using UnityEngine;

namespace Echoes
{
    [InitializeOnLoad]
    internal sealed class ExhibitOperatorWindow : EditorWindow
    {
        private EchoesPlayer source;
        [SerializeField] private Vector2 scroll;

        static ExhibitOperatorWindow()
        {
            EchoesPlayer.OperatorWindowRequested += () => EditorApplication.delayCall += Open;
        }

        private void OnEnable() => wantsMouseEnterLeaveWindow = true;

        [MenuItem("Echoes of Light/Exhibit Controls")]
        private static void Open()
        {
            var window = GetWindow<ExhibitOperatorWindow>(true, "Echoes | Exhibit Controls", true);
            window.minSize = new Vector2(560, 335);
            if (window.position.width < 560 || window.position.height < 335)
            {
                Rect bounds = window.position;
                window.position = new Rect(bounds.x, bounds.y, 566, 670);
            }
            window.ShowUtility();
        }

        private void OnGUI()
        {
            EchoesPlayer next = EditorApplication.isPlaying ? EchoesPlayer.Active : null;
            if (source != next)
            {
                if (source != null) source.SetEditorOperatorVisible(false);
                source = next;
                if (source != null) source.SetEditorOperatorVisible(true);
            }

            if (source == null)
            {
                GUILayout.Space(12);
                EditorGUILayout.HelpBox(EchoesLocale.T("请先在 Unity 中启动 Play Mode。",
                    "Start Play Mode in the Unity editor."), MessageType.Info);
                return;
            }

            source.SetEditorOperatorFocused(focusedWindow == this);
            source.DrawEditorOperator(new Rect(0, 0, position.width, position.height), ref scroll);
        }

        private void OnInspectorUpdate() => Repaint();

        private void OnLostFocus()
        {
            if (source == null) return;
            source.SetEditorOperatorFocused(false);
            source.ReleaseEditorTouch();
        }

        private void OnDisable()
        {
            if (source != null) source.SetEditorOperatorVisible(false);
        }
    }
}
