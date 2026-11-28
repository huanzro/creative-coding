using UnityEditor;
using UnityEngine;
#if UNITY_EDITOR_WIN
using System;
using System.Runtime.InteropServices;
#endif

namespace Echoes
{
    [InitializeOnLoad]
    internal sealed class CharacterProjectionWindow : EditorWindow
    {
        private EchoesPlayer source;
        private const string WindowTitle = "Echoes | Character";
#if UNITY_EDITOR_WIN
        private IntPtr fullscreenWindow;
        private NativeRect windowedBounds;
        private int windowedStyle;
        private bool windowedControls;
#endif
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
            var window = GetWindow<CharacterProjectionWindow>(true, WindowTitle, true);
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
#if UNITY_EDITOR_WIN
            ExitFullscreen();
#endif
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
#if UNITY_EDITOR_WIN
                if (key.keyCode == KeyCode.F11) { ToggleFullscreen(); key.Use(); }
                else if (key.keyCode == KeyCode.Escape && fullscreenWindow != IntPtr.Zero)
                { ExitFullscreen(); key.Use(); }
                else
#endif
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
#if UNITY_EDITOR_WIN
                if (GUILayout.Button(EchoesLocale.T("全屏 (F11)", "Fullscreen (F11)"))) ToggleFullscreen();
#endif
            }
            using (var changes = new EditorGUI.ChangeCheckScope())
            {
                characterScale = EditorGUILayout.Slider(EchoesLocale.T("人物大小", "Character size"), characterScale, .35f, 1.40f);
                lightReach = EditorGUILayout.Slider(EchoesLocale.T("光效延展", "Light extent"), lightReach, .65f, 2.45f);
                if (changes.changed)
                {
                    EditorPrefs.SetFloat(ScaleKey, characterScale);
                    EditorPrefs.SetFloat(ExtentKey, lightReach);
                }
            }
            GUILayout.EndArea();
        }

#if UNITY_EDITOR_WIN
        private const int WindowStyle = -16;
        private const int FrameStyle = 0x00C40000; // Caption and sizing border.
        private const uint RefreshFrame = 0x0034; // Keep Z order and activation, recalculate the frame.

        private void ToggleFullscreen()
        {
            if (fullscreenWindow != IntPtr.Zero) { ExitFullscreen(); return; }
            IntPtr window = FindWindow(null, WindowTitle);
            if (window == IntPtr.Zero || GetWindowThreadProcessId(window, out uint owner) == 0 ||
                owner != GetCurrentProcessId() || !GetWindowRect(window, out windowedBounds)) return;

            var monitor = new MonitorInfo { size = Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(MonitorFromWindow(window, 2), ref monitor)) return;
            windowedStyle = GetWindowLong(window, WindowStyle);
            windowedControls = hideControls;
            fullscreenWindow = window;
            SetWindowLong(window, WindowStyle, windowedStyle & ~FrameStyle);
            if (SetBounds(window, monitor.bounds)) hideControls = true;
            else ExitFullscreen();
        }

        private void ExitFullscreen()
        {
            if (fullscreenWindow == IntPtr.Zero) return;
            SetWindowLong(fullscreenWindow, WindowStyle, windowedStyle);
            SetBounds(fullscreenWindow, windowedBounds);
            fullscreenWindow = IntPtr.Zero;
            hideControls = windowedControls;
        }

        private static bool SetBounds(IntPtr window, NativeRect bounds) => SetWindowPos(window, IntPtr.Zero,
            bounds.left, bounds.top, bounds.right - bounds.left, bounds.bottom - bounds.top, RefreshFrame);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect { public int left, top, right, bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo { public int size; public NativeRect bounds, workArea; public uint flags; }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string className, string title);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentProcessId();
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out NativeRect bounds);
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(IntPtr window, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong(IntPtr window, int index, int value);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
#endif
    }
}
