using UnityEditor;
using UnityEngine;

namespace Echoes
{
    [InitializeOnLoad]
    internal sealed class CharacterProjectionWindow : EditorWindow
    {
        private EchoesPlayer source;
        [SerializeField] private bool mirrored;
        [SerializeField] private bool sideways = true;
        [SerializeField] private bool hideControls;
        [SerializeField] private float characterScale = 1;
        [SerializeField] private float lightReach = 1.45f;
        private const string ScaleKey = "Echoes.Projection.CharacterScale";
        private const string ExtentKey = "Echoes.Projection.LightReach";

        static CharacterProjectionWindow()
        {
            EchoesPlayer.CharacterWindowRequested += () => EditorApplication.delayCall += Open;
        }

        [MenuItem("Echoes of Light/Character Projection")]
        private static void Open()
        {
            var window = GetWindow<CharacterProjectionWindow>(true, "Echoes | Character", true);
            window.minSize = new Vector2(320, 200);
            if (window.position.width <= 350)
            {
                Rect bounds = window.position;
                window.position = new Rect(bounds.x, bounds.y, 640, 380);
            }
            window.ShowUtility();
        }

        [MenuItem("Echoes of Light/Preview Opening")]
        private static void PlayOpening() => EchoesPlayer.Active?.PlayOpening();

        [MenuItem("Echoes of Light/Preview Opening", true)]
        private static bool CanPreviewOpening() => EchoesPlayer.Active != null;

        private void OnEnable()
        {
            characterScale = EditorPrefs.GetFloat(ScaleKey, 1);
            lightReach = EditorPrefs.GetFloat(ExtentKey, 1.45f);
        }

        private void Update()
        {
            EchoesPlayer next = EditorApplication.isPlaying ? EchoesPlayer.Active : null;
            if (source != next)
            {
                if (source != null) source.SetEditorProjection(false);
                source = next;
            }
            // Unity can restore the window's player reference after a script reload.
            if (source != null) source.SetEditorProjection(true, position.size, sideways, lightReach);
            Repaint();
        }

        private void OnDisable()
        {
            if (source != null) source.SetEditorProjection(false);
        }

        private void OnGUI()
        {
            Rect viewport = new Rect(0, 0, position.width, position.height);
            if (source != null) source.DrawCharacterWindow(viewport, mirrored, sideways, characterScale, lightReach);
            else
            {
                EditorGUI.DrawRect(viewport, Color.black);
                EditorGUI.LabelField(new Rect(15, 48, viewport.width - 30, 25),
                    "Press Play in the main Unity window.", EditorStyles.whiteLabel);
            }

            Event key = Event.current;
            if (key.type == EventType.KeyDown && !key.alt && !key.control && !key.command)
            {
                if (key.keyCode == KeyCode.M) { mirrored = !mirrored; key.Use(); }
                else if (key.keyCode == KeyCode.R) { sideways = !sideways; key.Use(); }
                else if (key.keyCode == KeyCode.H) { hideControls = !hideControls; key.Use(); }
            }
            if (hideControls) return;
            GUILayout.BeginArea(new Rect(10, 9, Mathf.Min(390, viewport.width - 20), 82));
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button(mirrored ? "Unmirror (M)" : "Mirror (M)")) mirrored = !mirrored;
                if (GUILayout.Button(sideways ? "Upright (R)" : "Rotate (R)")) sideways = !sideways;
                if (GUILayout.Button("Hide (H)")) hideControls = true;
            }
            using (var changes = new EditorGUI.ChangeCheckScope())
            {
                characterScale = EditorGUILayout.Slider("人物大小", characterScale, .35f, 1.40f);
                lightReach = EditorGUILayout.Slider("光效延展", lightReach, .65f, 2.45f);
                if (changes.changed)
                {
                    EditorPrefs.SetFloat(ScaleKey, characterScale);
                    EditorPrefs.SetFloat(ExtentKey, lightReach);
                }
            }
            GUILayout.EndArea();
        }
    }
}
