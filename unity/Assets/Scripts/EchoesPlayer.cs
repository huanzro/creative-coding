using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Echoes
{
    public sealed class EchoesPlayer : MonoBehaviour
    {
        private readonly Journey journey = new Journey();
        private EchoesView view;
        private OpeningSound openingSound;
        private TravelSound travelSound;
        private bool preview, projection, mirror, audioReady, cameraReady, paused, inputReady;
        private bool settings, characterOnly, splitProjection, noHand;
        private int cast;
        private static readonly string[] CastNames = { "三维精灵", "绿金长裙精灵", "兜帽旅人", "星纹术士" };
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
        private int exhibitCue;
#endif
        private int savedRevision;
        private const float TurnHoldSeconds = .72f;
        private readonly float[] touchBegan = new float[4];
        private readonly bool[] pendingTouch = new bool[4];
        private Frame remote = new Frame();
        private const string SaveKey = "EchoesUnity.Checkpoint.Ruins.6";
        private const string PreviousRuinsSaveKey = "EchoesUnity.Checkpoint.Ruins.5";
        private const string PreviousSaveKey = "EchoesUnity.Checkpoint.3D.2";
        private static readonly string[] Titles = { "沉睡森林", "回声遗迹", "星海回响" };
#if UNITY_EDITOR
        public static EchoesPlayer Active { get; private set; }
        public static event Action CharacterWindowRequested;
        private bool editorProjection;
#endif

        [Serializable]
        private sealed class Frame
        {
            public int chapter, round, lesson, choice, flock, quota;
            public float time, light, charge, voice, progress;
            public bool closed, waiting, complete, paused, preview;
            public string title, message, left, right, phase;
            public SpiritFrame spirit = new SpiritFrame { releaseAge = -1, turnAge = -1, touchAge = -1, openingTime = -1 };
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
            view.UseWorld = PlayerPrefs.GetInt("EchoesUnity.World3D", 1) != 0;
            cast = Mathf.Clamp(PlayerPrefs.GetInt("EchoesUnity.Cast", 0), 0, CastNames.Length - 1);
            if (FindObjectOfType<AudioListener>() == null)
                gameObject.AddComponent<AudioListener>();
            openingSound = new OpeningSound(gameObject);
            travelSound = new TravelSound(gameObject);
            savedRevision = journey.Revision;
            Application.targetFrameRate = 60;
#if UNITY_EDITOR
            Active = this;
#endif
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            exhibit.TouchChanged += PhysicalTouch;
            comPort = PlayerPrefs.GetString("EchoesUnity.Port", comPort);
#endif
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLInput.captureAllKeyboardInput = false;
#endif
        }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        private void PhysicalTouch(int option, bool down) => TrackTouch(option, down, true);
#endif

        private void Update()
        {
            if (projection) return;
            float now = Time.unscaledTime;
            float dt = Mathf.Min(.04f, Time.unscaledDeltaTime);
            bool keyboard = preview && Application.isFocused && (!settings || !typingPort);
            inputReady = preview || audioReady && cameraReady && now - lastAudioAt < .5f && now - lastHandAt < .5f;
            if (preview && Input.GetKeyDown(KeyCode.F1)) settings = !settings;
            if (keyboard && Input.GetKeyDown(KeyCode.A)) TrackTouch(0, true, false);
            if (keyboard && Input.GetKeyDown(KeyCode.B)) TrackTouch(1, true, false);
            if (keyboard && Input.GetKeyUp(KeyCode.A)) TrackTouch(0, false, false);
            if (keyboard && Input.GetKeyUp(KeyCode.B)) TrackTouch(1, false, false);
            if (keyboard && Input.GetKeyDown(KeyCode.R)) Retry("");
            if (keyboard && Input.GetKeyDown(KeyCode.Escape)) journey.FinishOpening();
            ResolveHeldTouches(now, keyboard);
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
                journey.Step(journey.IsOpening ? Time.unscaledDeltaTime : dt, inputReady ? voice : 0, inputReady);
            }
            else journey.SetHand(Hand.Missing);
            view.Advance(journey);
            openingSound.Follow(journey.IsOpening ? journey.OpeningTime : -1, paused);
            travelSound.Follow(journey, paused);
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            exhibit.SetForestPalette(journey.InForest);
            if (exhibitCue != journey.CueId)
            {
                exhibitCue = journey.CueId;
                if (!paused && Application.isFocused && journey.InForest)
                {
                    if (journey.Cue == JourneyCue.ForestAmber) exhibit.FlashForest(false, now);
                    if (journey.Cue == JourneyCue.ForestScarlet) exhibit.FlashForest(true, now);
                }
            }
            exhibit.HoldingAuthorization = journey.Phase == Stage.StarsDelegated;
            exhibit.Tick(now, !paused && !journey.IsOpening && Application.isFocused,
                Mathf.Max(journey.Voice * .42f, journey.WindOutput));
#endif

            if (!journey.IsOpening && savedRevision != journey.Revision)
            {
                savedRevision = journey.Revision;
                PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(journey.Checkpoint));
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
            return new Frame {
                chapter = journey.Chapter, round = journey.StarCount, lesson = journey.Lesson, choice = journey.ForestRoute,
                flock = journey.InRuins ? journey.RuinInventory : journey.InForest ? journey.ForestLights : journey.Plants,
                quota = journey.InRuins ? Journey.RuinCapacity : journey.InForest ? journey.ForestPlantsRequired : 2,
                time = journey.Time,
                light = journey.Light, charge = journey.Charge, voice = journey.Voice, progress = journey.Progress,
                closed = journey.Gesture == Hand.Fist, complete = journey.Phase == Stage.Complete,
                waiting = !inputReady || !journey.Started, paused = paused, preview = preview,
                title = journey.IsOpening ? "序幕" : journey.Lesson >= 0 ? "回声庭院 · 试玩" : Titles[journey.Chapter],
                message = !journey.IsOpening && !paused && inputReady && journey.Gesture == Hand.Missing
                    ? "请把手放回画面，张开手继续。 " + journey.Message : journey.Message,
                left = journey.LeftLabel, right = journey.RightLabel, phase = journey.Phase.ToString(), spirit = LocalSpirit
            };
        }

        private SpiritFrame LocalSpirit => SpiritFrame.Read(journey, cast);

        private bool StageSplit
        {
            get
            {
#if UNITY_EDITOR
                return splitProjection || editorProjection;
#else
                return splitProjection;
#endif
            }
        }

        private void LateUpdate()
        {
            if (view == null) return;
            SpiritFrame frame = projection ? remote.spirit : LocalSpirit;
            view.UpdateSpirit(frame);
            if (!projection && !characterOnly) view.UpdateWorld(journey, frame, StageSplit);
        }

        private void OnGUI()
        {
            if (projection)
            {
                view.DrawProjection(remote.spirit, mirror, true);
                return;
            }
            SpiritFrame spirit = LocalSpirit;
            if (characterOnly) view.DrawProjection(spirit, mirror, true);
            else
            {
                view.DrawStage(journey, spirit, StageSplit);
            }
            if (Application.platform == RuntimePlatform.WebGLPlayer) return;
            if (journey.IsOpening)
            {
                if (!characterOnly) view.DrawOpeningControls(journey.OpeningTime, journey.FinishOpening);
                return;
            }
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
            if (editorProjection) GUI.Label(new Rect(367, 309, 143, 29), "已分屏", label);
#endif
            if (GUI.Button(new Rect(218, 309, 138, 30), "预览片头", button)) PlayOpening();
            GUI.Label(new Rect(20, 358, 100, 28), "出场角色", label);
            int picked = GUI.SelectionGrid(new Rect(116, 350, 388, 62), cast, CastNames, 2, button);
            if (picked != cast)
            {
                cast = picked;
                PlayerPrefs.SetInt("EchoesUnity.Cast", cast);
                PlayerPrefs.Save();
            }
            bool world = GUI.Toggle(new Rect(20, 420, 300, 27), view.UseWorld, "星海使用三维背景");
            if (world != view.UseWorld)
            {
                view.UseWorld = world;
                PlayerPrefs.SetInt("EchoesUnity.World3D", world ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

#if UNITY_EDITOR
        public void SetEditorProjection(bool active) { editorProjection = active; }

        public void DrawCharacterWindow(Rect viewport, bool mirrored, bool sideways, float characterScale, float lightReach)
        {
            view?.DrawProjection(LocalSpirit, mirrored, sideways, viewport, characterScale, lightReach);
        }
#endif

        public void PlayOpening()
        {
            settings = paused = characterOnly = false;
            journey.PlayOpening();
        }

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

        private void TrackTouch(int option, bool down, bool physical)
        {
            int index = option + (physical ? 2 : 0);
            if (down)
            {
                if (paused || !inputReady || !Application.isFocused) return;
                if (!journey.ForestCanTurnAround) { Touch(option, physical); return; }
                pendingTouch[index] = true;
                touchBegan[index] = Time.unscaledTime;
                return;
            }
            if (!pendingTouch[index]) return;
            pendingTouch[index] = false;
            if (!journey.ForestCanTurnAround || paused || !inputReady || !Application.isFocused) return;
            if (Time.unscaledTime - touchBegan[index] >= TurnHoldSeconds) journey.TurnAroundForest();
            else Touch(option, physical);
        }

        private void ResolveHeldTouches(float now, bool keyboard)
        {
            bool allowTurn = journey.ForestCanTurnAround && Application.isFocused && !paused && inputReady;
            bool physicalReady = false;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            physicalReady = exhibit.Ready;
#endif
            for (int i = 0; i < pendingTouch.Length; i++)
            {
                if (!allowTurn || !(i < 2 ? keyboard : physicalReady)) pendingTouch[i] = false;
                if (!pendingTouch[i] || now - touchBegan[i] < TurnHoldSeconds) continue;
                Array.Clear(pendingTouch, 0, pendingTouch.Length);
                journey.TurnAroundForest();
                break;
            }
        }

        private void Touch(int option, bool physical)
        {
            int cueBefore = journey.CueId;
            bool steering = journey.InForest || journey.InRuins;
            if (paused || !inputReady || !journey.Touch(option)) return;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            if (steering) return;
            bool changed = journey.CueId != cueBefore;
            if (changed && journey.Cue == JourneyCue.Withdraw) exhibit.ClearLamp(1);
            else exhibit.Flash(option, Time.unscaledTime, physical && changed && journey.Cue == JourneyCue.Assist);
#endif
        }

        public void Select(string value)
        {
            if (!int.TryParse(value, out int chapter) || chapter < -1 || chapter > 2) return;
            // Selecting a chapter is an explicit entry point, not a progress checkpoint.
            if (chapter == -1) journey.SelectPractice();
            else journey.SelectChapter(chapter);
            savedRevision = journey.Revision;
        }

        public void Retry(string unused)
        {
            journey.Retry();
        }

        public void Restore(string unused)
        {
            string save = PlayerPrefs.GetString(SaveKey,
                PlayerPrefs.GetString(PreviousRuinsSaveKey, PlayerPrefs.GetString(PreviousSaveKey, "")));
            if (string.IsNullOrEmpty(save)) return;
            try
            {
                if (!journey.Restore(JsonUtility.FromJson<JourneyCheckpoint>(save))) return;
            }
            catch (ArgumentException) { return; }
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
            exhibit.TouchChanged -= PhysicalTouch;
            exhibit.Dispose();
#endif
#if !UNITY_WEBGL || UNITY_EDITOR
            microphone.Stop();
#endif
            openingSound?.Dispose();
            openingSound = null;
            travelSound?.Dispose();
            travelSound = null;
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
