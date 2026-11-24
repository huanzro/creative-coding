using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Echoes
{
    public sealed class EchoesPlayer : MonoBehaviour
    {
        private readonly Journey journey = new Journey();
        private EchoesView view;
        private bool preview, projection, mirror, audioReady, cameraReady, paused, inputReady;
        private bool settings, characterOnly, splitProjection, noHand;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        private bool typingPort;
#else
        private const bool typingPort = false;
#endif
        private Hand liveHand;
        private float liveVoice, lastHandAt, lastAudioAt;
#if UNITY_WEBGL && !UNITY_EDITOR
        private float publishAt;
        private string published;
#endif
#if !UNITY_WEBGL || UNITY_EDITOR
        private readonly LocalMicrophone microphone = new LocalMicrophone();
#endif
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        private readonly WindowsExhibit exhibit = new WindowsExhibit();
        private string comPort = "COM3";
#endif
        private int savedRevision;
        private Frame remote = new Frame();
        private const string SaveKey = "EchoesUnity.Checkpoint.1.";
        private static readonly string[] Titles = { "沉睡森林", "回声之门", "星海回响" };
#if UNITY_EDITOR
        public static EchoesPlayer Active { get; private set; }
        public static event Action CharacterWindowRequested;
        private bool editorProjection;
#endif

        [Serializable]
        private sealed class Frame
        {
            public int chapter, round, lesson, choice, flock, quota, score;
            public float time, light, charge, voice, progress;
            public bool closed, waiting, complete, paused, preview;
            public string title, message, left, right, phase;
            public SpiritFrame spirit = new SpiritFrame { releaseAge = -1, turnAge = -1, touchAge = -1 };
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void EchoesPublish(string json);
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreatePlayer()
        {
            if (FindObjectOfType<EchoesPlayer>() == null)
                new GameObject("EchoesPlayer").AddComponent<EchoesPlayer>();
        }

        private void Awake()
        {
            var cameraObject = new GameObject("Stage Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 0;
        }

        private void OnEnable()
        {
            // Awake is not rerun when Unity reloads scripts during a Play Mode iteration.
            preview = Application.platform != RuntimePlatform.WebGLPlayer;
            view = new EchoesView();
            savedRevision = journey.Revision;
            Application.targetFrameRate = 60;
#if UNITY_EDITOR
            Active = this;
#endif
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            exhibit.Pressed += PhysicalTouch;
            comPort = PlayerPrefs.GetString("EchoesUnity.Port", comPort);
#endif
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLInput.captureAllKeyboardInput = false;
#endif
        }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        private void PhysicalTouch(int option) { Touch(option, true); }
#endif

        private void Update()
        {
            if (projection) return;
            float now = Time.unscaledTime;
            float dt = Mathf.Min(.04f, Time.unscaledDeltaTime);
            bool keyboard = preview && Application.isFocused && (!settings || !typingPort);
            if (preview && Input.GetKeyDown(KeyCode.F1)) settings = !settings;
            if (keyboard && Input.GetKeyDown(KeyCode.A)) Choose("0");
            if (keyboard && Input.GetKeyDown(KeyCode.B)) Choose("1");
            if (keyboard && Input.GetKeyDown(KeyCode.R)) Retry("");
            inputReady = preview || audioReady && cameraReady && now - lastAudioAt < .5f && now - lastHandAt < .5f;
            float voice = preview ? (keyboard && Input.GetKey(KeyCode.X) ? .7f :
                keyboard && Input.GetKey(KeyCode.Z) ? .3f : 0) : liveVoice;
#if !UNITY_WEBGL || UNITY_EDITOR
            microphone.Update(now, dt);
            if (microphone.Ready) voice = microphone.Level;
#endif
            if (!paused)
            {
                var gesture = !inputReady ? Hand.Missing : preview
                    ? noHand || !keyboard ? Hand.Missing : Input.GetKey(KeyCode.Space) ? Hand.Fist : Hand.Open
                    : liveHand;
                journey.SetHand(gesture);
                journey.Step(dt, inputReady ? voice : 0, inputReady);
            }
            else journey.SetHand(Hand.Missing);
            view.Advance(journey);
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            exhibit.Tick(now, !paused && Application.isFocused, journey.Voice);
#endif

            if (savedRevision != journey.Revision)
            {
                savedRevision = journey.Revision;
                PlayerPrefs.SetInt(SaveKey + "chapter", journey.Chapter);
                PlayerPrefs.SetInt(SaveKey + "round", journey.Round);
                PlayerPrefs.SetInt(SaveKey + "lesson", journey.Lesson);
                PlayerPrefs.SetInt(SaveKey + "score", journey.Score);
                PlayerPrefs.Save();
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            if (Time.unscaledTime - publishAt > .08f)
            {
                publishAt = Time.unscaledTime;
                string json = JsonUtility.ToJson(Snapshot());
                if (json != published) { published = json; EchoesPublish(json); }
            }
#endif
        }

        private Frame Snapshot()
        {
            string left = "A", right = "B";
            if (journey.Phase == Stage.Fork || journey.Phase == Stage.Tutorial && journey.Lesson == 2)
            { left = "A · 林荫绕路"; right = "B · 风带捷径"; }
            else if (journey.Phase == Stage.Complete && journey.Chapter < 2) left = right = "下一章";
            else if (journey.Chapter == 2 && journey.Phase != Stage.Complete)
            { left = "A · 同相"; right = "B · 反相"; }
            else if (journey.Phase == Stage.RecordReady || journey.Phase == Stage.Record) left = right = "记录动作";
            else if (journey.Phase == Stage.ReplayReady || journey.Phase == Stage.ReplayFailed) left = right = "与回声合作";
            return new Frame {
                chapter = journey.Chapter, round = journey.Round, lesson = journey.Lesson, choice = journey.Choice,
                flock = journey.Flock, quota = journey.Quota, score = journey.Score, time = journey.Time,
                light = journey.Light, charge = journey.Charge, voice = journey.Voice, progress = journey.Progress,
                closed = journey.Gesture == Hand.Fist, complete = journey.Phase == Stage.Complete,
                waiting = !inputReady || !journey.Started, paused = paused, preview = preview,
                title = journey.Lesson >= 0 ? "回声庭院" : Titles[journey.Chapter],
                message = journey.Phase == Stage.Replay
                    ? journey.Message + (journey.RequiredHand == Hand.Open ? " 当前要张手。" : " 当前要握拳。")
                    : journey.Message,
                left = left, right = right, phase = journey.Phase.ToString(), spirit = SpiritFrame.Read(journey)
            };
        }

        private void LateUpdate()
        {
            view?.UpdateSpirit(projection ? remote.spirit : SpiritFrame.Read(journey));
        }

        private void OnGUI()
        {
            if (projection)
            {
                view.DrawProjection(remote.spirit, mirror, true);
                return;
            }
            SpiritFrame spirit = SpiritFrame.Read(journey);
            if (characterOnly) view.DrawProjection(spirit, mirror, true);
            else
            {
                bool split = splitProjection;
#if UNITY_EDITOR
                split |= editorProjection;
#endif
                view.DrawStage(journey, spirit, split);
            }
            if (Application.platform == RuntimePlatform.WebGLPlayer) return;
            var state = Snapshot();
            view.DrawControls(state.title, state.message, state.left, state.right, state.voice,
                option => Choose(option.ToString()), Select, () => Retry(""), () => Restore(""),
                () => Pause(paused ? "0" : "1"), paused, characterOnly,
                () => settings = !settings, settings ? DrawSettings : (Action<GUIStyle, GUIStyle>)null);
        }

        private void DrawSettings(GUIStyle label, GUIStyle button)
        {
            GUI.Label(new Rect(20, 14, 530, 28), "游戏设置 · Unity Play Mode", label);
            GUI.Label(new Rect(20, 56, 85, 27), "背景时段", label);
            view.TimeOfDay = GUI.SelectionGrid(new Rect(110, 54, 394, 31), view.TimeOfDay,
                new[] { "自动", "薄暮", "蓝调", "月夜", "破晓" }, 5, button);
            GUI.Label(new Rect(20, 98, 160, 27), "背景速度 " + view.ScrollSpeed.ToString("0.00") + "×", label);
            view.ScrollSpeed = GUI.HorizontalSlider(new Rect(182, 109, 317, 21), view.ScrollSpeed, .25f, 2);
            noHand = GUI.Toggle(new Rect(20, 139, 210, 27), noHand, "模拟手离开画面");
            characterOnly = GUI.Toggle(new Rect(226, 139, 153, 27), characterOnly, "纯黑人物");
            mirror = GUI.Toggle(new Rect(400, 139, 115, 27), mirror, "镜像");
#if !UNITY_WEBGL || UNITY_EDITOR
            if (GUI.Button(new Rect(20, 181, 130, 29), microphone.Active ? "关闭麦克风" : "开启麦克风", button))
                microphone.Toggle(Time.unscaledTime);
            if (GUI.Button(new Rect(160, 181, 94, 29), "重新校准", button)) microphone.Calibrate();
            GUI.Label(new Rect(264, 181, 240, 30), microphone.Status, label);
#endif
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            GUI.SetNextControlName("EchoesComPort");
            comPort = GUI.TextField(new Rect(20, 228, 88, 29), comPort, 8);
            typingPort = GUI.GetNameOfFocusedControl() == "EchoesComPort";
            if (GUI.Button(new Rect(117, 228, 130, 29), exhibit.Connected ? "断开 Arduino" : "连接 Arduino", button))
            {
                if (exhibit.Connected) exhibit.Dispose();
                else
                {
                    exhibit.Connect(comPort, Time.unscaledTime);
                    if (exhibit.Connected) PlayerPrefs.SetString("EchoesUnity.Port", comPort);
                }
            }
            GUI.Label(new Rect(259, 228, 244, 48), exhibit.Status, label);
            bool guiEnabled = GUI.enabled;
            GUI.enabled = exhibit.Ready && !paused;
            exhibit.LampsEnabled = GUI.Toggle(new Rect(20, 272, 164, 28), exhibit.LampsEnabled, "启用双灯");
            GUI.enabled = GUI.enabled && exhibit.FanSupported;
            exhibit.FanEnabled = GUI.Toggle(new Rect(179, 272, 173, 28), exhibit.FanEnabled, "启用风扇");
            GUI.enabled = guiEnabled;
            if (GUI.Button(new Rect(371, 270, 132, 30), "关闭实体输出", button)) exhibit.StopOutput();
#endif
#if UNITY_EDITOR
            if (GUI.Button(new Rect(20, 309, 183, 30), "打开独立人物窗口", button)) CharacterWindowRequested?.Invoke();
            if (editorProjection) GUI.Label(new Rect(219, 309, 270, 29), "已分屏 · 背景保留淡光效", label);
#endif
        }

#if UNITY_EDITOR
        public void SetEditorProjection(bool active) { editorProjection = active; }

        public void DrawCharacterWindow(Rect viewport, bool mirrored, bool sideways)
        {
            view?.DrawProjection(SpiritFrame.Read(journey), mirrored, sideways, viewport);
        }
#endif

        public void BrowserHand(string value)
        {
            liveHand = value == "open" ? Hand.Open : value == "closed" ? Hand.Fist : Hand.Missing;
            lastHandAt = Time.unscaledTime;
            if (liveHand == Hand.Missing && !preview) journey.SetHand(Hand.Missing);
        }

        public void BrowserVoice(string value)
        {
            if (!float.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out liveVoice) ||
                float.IsNaN(liveVoice) || float.IsInfinity(liveVoice)) liveVoice = 0;
            lastAudioAt = Time.unscaledTime;
        }

        public void BrowserAudioReady(string value)
        {
            audioReady = value == "1";
            if (!audioReady && !preview) journey.SetHand(Hand.Missing);
        }

        public void BrowserCameraReady(string value) { cameraReady = value == "1"; }

        public void SetPreview(string value)
        {
            preview = value == "1";
            journey.SetHand(Hand.Missing);
        }

        public void Choose(string value)
        {
            if (int.TryParse(value, out int option)) Touch(option, false);
        }

        private void Touch(int option, bool physical)
        {
            if (paused || !inputReady || !journey.Touch(option)) return;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            exhibit.Flash(option, Time.unscaledTime, physical);
#endif
        }

        public void Select(string value)
        {
            if (!int.TryParse(value, out int chapter) || chapter < 0 || chapter > 2) return;
            // Selecting a chapter is an explicit entry point, not a progress checkpoint.
            journey.SelectChapter(chapter);
            savedRevision = journey.Revision;
        }

        public void Retry(string unused)
        {
            journey.Retry();
        }

        public void Restore(string unused)
        {
            if (!PlayerPrefs.HasKey(SaveKey + "chapter")) return;
            journey.Restore(PlayerPrefs.GetInt(SaveKey + "chapter"), PlayerPrefs.GetInt(SaveKey + "round"),
                PlayerPrefs.GetInt(SaveKey + "lesson"), PlayerPrefs.GetInt(SaveKey + "score"));
            savedRevision = journey.Revision;
        }

        public void Pause(string value) { paused = value == "1"; }
        public void SetProjection(string value) { projection = value == "1"; }
        public void SetMirror(string value) { mirror = value == "1"; }
        public void SetSplit(string value) { splitProjection = value == "1"; }

        public void SetTimeOfDay(string value)
        {
            if (int.TryParse(value, out int index)) view.TimeOfDay = Mathf.Clamp(index, 0, 4);
        }

        public void SetScrollSpeed(string value)
        {
            if (float.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float speed) &&
                !float.IsNaN(speed) && !float.IsInfinity(speed)) view.ScrollSpeed = Mathf.Clamp(speed, .25f, 2);
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            if (Active == this) Active = null;
#endif
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            exhibit.Pressed -= PhysicalTouch;
            exhibit.Dispose();
#endif
#if !UNITY_WEBGL || UNITY_EDITOR
            microphone.Stop();
#endif
            view?.Dispose();
            view = null;
        }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        private void OnApplicationFocus(bool focused)
        {
            if (!focused) exhibit.StopOutput();
        }
#endif

        public void ProjectFrame(string json)
        {
            if (!projection) return;
            var next = JsonUtility.FromJson<Frame>(json);
            if (next != null) remote = next;
        }
    }
}
