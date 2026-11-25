using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Echoes
{
    public sealed partial class EchoesPlayer : MonoBehaviour
    {
        private readonly Journey journey = new Journey();
        private EchoesView view;
        private OpeningSound openingSound;
        private TravelSound travelSound;
        private ForestAudioDetails forestAudio;
        private bool preview, projection, mirror, audioReady, cameraReady, paused, inputReady;
        private bool settings, characterOnly, splitProjection, noHand;
        private int cast;
        private static readonly string[] CastNames = { "三维精灵", "绿金长裙精灵", "兜帽旅人", "星纹术士" };
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
        private int exhibitCue;
#endif
        private int savedRevision;
        private const string LateralKey = "EchoesUnity.Forest.LateralScale";
        private const string MazeKey = "EchoesUnity.Forest.MazeScale";
        private const string WebKey = "EchoesUnity.Forest.WebRange";
        private const string MawKey = "EchoesUnity.Forest.MawRange";
        private const string BrightnessKey = "EchoesUnity.Projection.Brightness";
        private const string FalloffKey = "EchoesUnity.Projection.Falloff";
        private const string GradientKey = "EchoesUnity.Projection.Gradient";
        private bool projectionSettings, changedTuning;
        private readonly bool[] physicalTouchDown = new bool[2];
        private Frame remote = new Frame();
        private const string SaveKey = "EchoesUnity.Checkpoint.Ruins.7";
        private const string PreviousCircuitSaveKey = "EchoesUnity.Checkpoint.Ruins.6";
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
            view.ScreenBrightness = PlayerPrefs.GetFloat(BrightnessKey, ProjectionLighting.DefaultValue);
            view.ProjectionFalloff = PlayerPrefs.GetFloat(FalloffKey, ProjectionLighting.DefaultValue);
            view.ProjectionGradient = PlayerPrefs.GetInt(GradientKey, 1) != 0;
            cast = Mathf.Clamp(PlayerPrefs.GetInt("EchoesUnity.Cast", 0), 0, CastNames.Length - 1);
            if (FindObjectOfType<AudioListener>() == null)
                gameObject.AddComponent<AudioListener>();
            openingSound = new OpeningSound(gameObject);
            travelSound = new TravelSound(gameObject);
            forestAudio = new ForestAudioDetails(gameObject);
            journey.ForestLateralScale = PlayerPrefs.GetFloat(LateralKey, Journey.DefaultForestLateralScale);
            journey.MazeWalkScale = PlayerPrefs.GetFloat(MazeKey, Journey.DefaultMazeWalkScale);
            journey.ForestWebRange = PlayerPrefs.GetFloat(WebKey, Journey.DefaultForestCaptureRange);
            journey.ForestMawRange = PlayerPrefs.GetFloat(MawKey, Journey.DefaultForestCaptureRange);
            savedRevision = journey.Revision;
            Application.targetFrameRate = 60;
#if UNITY_EDITOR
            Active = this;
#endif
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            exhibit.TouchChanged += PhysicalTouch;
            comPort = PlayerPrefs.GetString("EchoesUnity.Port", "");
            RefreshSerialPorts();
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
            bool keyboard = preview && Application.isFocused;
            inputReady = preview || audioReady && cameraReady && now - lastAudioAt < .5f && now - lastHandAt < .5f;
            if (preview && Input.GetKeyDown(KeyCode.F1)) ToggleSettings();
            if (keyboard && Input.GetKeyDown(KeyCode.R)) Retry("");
            if (keyboard && Input.GetKeyDown(KeyCode.Escape)) journey.FinishOpening();
            float voice = preview ? (keyboard && Input.GetKey(KeyCode.X) ? .7f :
                keyboard && Input.GetKey(KeyCode.Z) ? .3f : 0) : liveVoice;
#if !UNITY_WEBGL || UNITY_EDITOR
            microphone.Update(now, dt);
            if (microphone.Ready) voice = microphone.Level;
#endif
            var gesture = paused || !inputReady ? Hand.Missing : preview
                ? PreviewHand(keyboard)
                : liveHand;
            journey.SetHand(gesture);
            bool leftHeld = keyboard && Input.GetKey(KeyCode.A);
            bool rightHeld = keyboard && Input.GetKey(KeyCode.B);
            if (keyboard && Input.GetKeyDown(KeyCode.A) && (!journey.InForest || !rightHeld)) TrackTouch(0, true, false);
            if (keyboard && Input.GetKeyDown(KeyCode.B) && (!journey.InForest || !leftHeld)) TrackTouch(1, true, false);
            ResolveHeldSteering(leftHeld || view.ForestScreenTouchHeld(0), rightHeld || view.ForestScreenTouchHeld(1));
            if (!paused) journey.Step(journey.IsOpening ? Time.unscaledDeltaTime : dt, inputReady ? voice : 0, inputReady);
            UpdateMazeMap(keyboard);
            view.Advance(journey);
            openingSound.Follow(journey.IsOpening ? journey.OpeningTime : -1, paused);
            travelSound.Follow(journey, paused);
            forestAudio.Follow(journey,paused);
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
            string message = journey.Message;
            if (journey.ForestMazeWalkArea && journey.Phase == Stage.ForestExplore && string.IsNullOrEmpty(message))
                message = Journey.MazeWalkHint;
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
                    ? "请把手放回画面，张开手继续。 " + message : message,
                left = journey.LeftLabel, right = journey.RightLabel, phase = journey.Phase.ToString(), spirit = LocalSpirit
            };
        }

        private SpiritFrame LocalSpirit => SpiritFrame.Read(journey, cast);
        private bool ShowProjectionCalibration => settings && projectionSettings;

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
            if (!projection && (!characterOnly || ShowProjectionCalibration))
                view.UpdateWorld(journey, frame, StageSplit, ShowProjectionCalibration);
        }

        private void OnGUI()
        {
            if (projection)
            {
                view.DrawProjection(remote.spirit, mirror, true);
                return;
            }
            SpiritFrame spirit = LocalSpirit;
            if (characterOnly && !ShowProjectionCalibration) view.DrawProjection(spirit, mirror, true);
            else
            {
                view.DrawStage(journey, spirit, StageSplit, ShowProjectionCalibration);
            }
            if (!settings && !characterOnly && journey.ForestMazeActive)
                DrawMazeMapControls();
            if (Application.platform == RuntimePlatform.WebGLPlayer) return;
            if (journey.IsOpening && !settings)
            {
                if (!characterOnly) view.DrawOpeningControls(journey.OpeningTime, journey.FinishOpening);
                return;
            }
            var state = Snapshot();
            view.DrawControls(state.title, state.message, state.left, state.right, state.voice,
                option => Choose(option.ToString()), Select, () => Retry(""), () => Restore(""),
                () => Pause(paused ? "0" : "1"), paused, characterOnly, journey.ForestMazeWalkArea, journey.InForest,
                ToggleSettings, settings ? DrawSettings : (Action<GUIStyle, GUIStyle>)null);
        }

        private void DrawSettings(GUIStyle label, GUIStyle button)
        {
            if (deviceSettings)
            {
                if (GUI.Button(new Rect(354, 12, 148, 31), "返回游戏设置", button)) deviceSettings = false;
                else DrawDeviceSettings(label, button);
                return;
            }
            if (GUI.Button(new Rect(354, 12, 148, 31), projectionSettings ? "返回游戏设置" : "投影校准", button))
                projectionSettings = !projectionSettings;
            if (projectionSettings)
            {
                DrawProjectionSettings(label, button);
                return;
            }
            GUI.Label(new Rect(20, 14, 160, 28), "游戏设置", label);
            if (GUI.Button(new Rect(184, 12, 156, 31), "摄像头 / Arduino", button))
            {
                deviceSettings = true;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
                RefreshSerialPorts();
#endif
            }
            GUI.Label(new Rect(20, 56, 85, 27), "背景时段", label);
            view.TimeOfDay = GUI.SelectionGrid(new Rect(110, 54, 394, 31), view.TimeOfDay,
                new[] { "自动", "薄暮", "蓝调", "月夜", "破晓" }, 5, button);
            GUI.Label(new Rect(20, 98, 160, 27), "背景速度 " + view.ScrollSpeed.ToString("0.00") + "×", label);
            view.ScrollSpeed = GUI.HorizontalSlider(new Rect(182, 109, 317, 21), view.ScrollSpeed, .25f, 2);
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && !desktopCamera.Active;
#endif
            noHand = GUI.Toggle(new Rect(20, 139, 210, 27), noHand, "模拟手离开画面");
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            GUI.enabled = enabled;
#endif
            characterOnly = GUI.Toggle(new Rect(226, 139, 153, 27), characterOnly, "纯黑人物");
            mirror = GUI.Toggle(new Rect(400, 139, 115, 27), mirror, "镜像");
#if UNITY_EDITOR
            if (GUI.Button(new Rect(20, 186, 183, 30), "打开独立人物窗口", button)) CharacterWindowRequested?.Invoke();
            if (editorProjection) GUI.Label(new Rect(367, 186, 143, 29), "已分屏", label);
#endif
            if (GUI.Button(new Rect(218, 186, 138, 30), "预览片头", button)) PlayOpening();
            GUI.Label(new Rect(20, 245, 100, 28), "出场角色", label);
            int picked = GUI.SelectionGrid(new Rect(116, 231, 388, 62), cast, CastNames, 2, button);
            if (picked != cast)
            {
                cast = picked;
                PlayerPrefs.SetInt("EchoesUnity.Cast", cast);
                PlayerPrefs.Save();
            }
            bool world = GUI.Toggle(new Rect(20, 305, 300, 27), view.UseWorld, "星海使用三维背景");
            if (world != view.UseWorld)
            {
                view.UseWorld = world;
                PlayerPrefs.SetInt("EchoesUnity.World3D", world ? 1 : 0);
                PlayerPrefs.Save();
            }
            GUI.Label(new Rect(20, 352, 285, 26), "森林 · 速度与捕捉范围", label);
            journey.ForestLateralScale = TuningSlider(381, "左右移速", LateralKey, journey.ForestLateralScale,
                Journey.MinForestLateralScale, Journey.MaxForestLateralScale, label);
            journey.MazeWalkScale = TuningSlider(414, "迷宫行走速度", MazeKey, journey.MazeWalkScale,
                Journey.MinimumMazeWalkScale, Journey.MaximumMazeWalkScale, label);
            journey.ForestWebRange = TuningSlider(447, "蛛网捕捉范围", WebKey, journey.ForestWebRange,
                Journey.MinForestCaptureRange, Journey.MaxForestCaptureRange, label);
            journey.ForestMawRange = TuningSlider(480, "食人花捕捉范围", MawKey, journey.ForestMawRange,
                Journey.MinForestCaptureRange, Journey.MaxForestCaptureRange, label);
        }

        private void DrawProjectionSettings(GUIStyle label, GUIStyle button)
        {
            GUI.Label(new Rect(20, 14, 315, 28), "拟全息投影校准", label);
            bool gradient = GUI.Toggle(new Rect(20, 55, 257, 28), view.ProjectionGradient, "启用人物周围渐亮");
            if (gradient != view.ProjectionGradient)
            {
                view.ProjectionGradient = gradient;
                PlayerPrefs.SetInt(GradientKey, gradient ? 1 : 0);
                changedTuning = true;
            }
#if UNITY_EDITOR
            if (!editorProjection && GUI.Button(new Rect(309, 53, 193, 31), "打开独立人物窗口", button))
                CharacterWindowRequested?.Invoke();
#endif
            view.ScreenBrightness = TuningSlider(98, "全屏亮度", BrightnessKey, view.ScreenBrightness,
                ProjectionLighting.MinimumBrightness, ProjectionLighting.MaximumBrightness, label, "");
            DrawLightingCurve(153, "输出亮度", "原画亮度", false, label);
            view.ProjectionFalloff = TuningSlider(283, "渐亮速率", FalloffKey, view.ProjectionFalloff,
                ProjectionLighting.MinimumFalloff, ProjectionLighting.MaximumFalloff, label);
            DrawLightingCurve(338, "背景亮度倍率", "到人物的距离", true, label);
            GUI.Label(new Rect(20, 476, 482, 42), !view.ProjectionGradient ? "渐亮已关闭，可在上方重新启用。" :
                $"三维主画面实时预览。人物背后保留 {ProjectionLighting.CenterGain(view.ProjectionFalloff):0%} 背景亮度。", label);
            if (!GUI.Button(new Rect(20, 521, 141, 30), "恢复默认", button)) return;
            view.ScreenBrightness = view.ProjectionFalloff = ProjectionLighting.DefaultValue;
            view.ProjectionGradient = true;
            PlayerPrefs.DeleteKey(BrightnessKey);
            PlayerPrefs.DeleteKey(FalloffKey);
            PlayerPrefs.DeleteKey(GradientKey);
            changedTuning = true;
        }

        private void DrawLightingCurve(float y, string vertical, string horizontal, bool radial, GUIStyle label)
        {
            var plot = new Rect(22, y, 478, 98);
            GUI.Label(new Rect(plot.x, plot.y-26, 240, 25), vertical, label);
            GUI.Label(new Rect(plot.xMax-116, plot.yMax+1, 118, 25), horizontal + " →", label);
            if (Event.current.type != EventType.Repaint) return;
            var previousColor = GUI.color;
            var previousMatrix = GUI.matrix;
            Texture2D pixel = Texture2D.whiteTexture;
            GUI.color = new Color(.055f,.095f,.14f);
            GUI.DrawTexture(plot,pixel);
            GUI.color = new Color(.17f,.24f,.32f);
            for (int i=0; i<=4; i++)
            {
                GUI.DrawTexture(new Rect(plot.x+i*plot.width/4,plot.y,1,plot.height),pixel);
                GUI.DrawTexture(new Rect(plot.x,plot.y+i*plot.height/4,plot.width,1),pixel);
            }
            GUI.color = radial ? new Color(.62f,.86f,1) : new Color(1,.83f,.51f);
            Vector2 last = default;
            const int segments = 48;
            for (int i=0; i<=segments; i++)
            {
                float t = i/(float)segments;
                float level = radial ? ProjectionLighting.RadialGain(t*1.24f,view.ProjectionCoreRadius,view.ProjectionFalloff) :
                    ProjectionLighting.Tone(t,view.ScreenBrightness);
                Vector2 next = new Vector2(plot.x+t*plot.width,plot.yMax-level*plot.height);
                if (i > 0)
                {
                    Vector2 direction = next-last;
                    GUI.matrix = previousMatrix * Matrix4x4.TRS(last,
                        Quaternion.Euler(0,0,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg),Vector3.one);
                    GUI.DrawTexture(new Rect(0,-1,direction.magnitude+1,2),pixel);
                }
                last = next;
            }
            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
        }

        private float TuningSlider(float y,string title,string key,float value,float min,float max,GUIStyle label,string unit = "×")
        {
            GUI.Label(new Rect(20,y,223,24), title+"  "+value.ToString("0.00")+unit,label);
            float next=GUI.HorizontalSlider(new Rect(249,y+7,247,19),value,min,max);
            if (Mathf.Approximately(next,value)) return value;
            next=Mathf.Clamp(Mathf.Round(next*100)/100,min,max);
            PlayerPrefs.SetFloat(key,next);
            changedTuning=true;
            return next;
        }

        private void ToggleSettings()
        {
            settings = !settings;
            if (!settings) SaveTuning();
        }

        private void SaveTuning()
        {
            if (!changedTuning) return;
            PlayerPrefs.Save();
            changedTuning=false;
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
            bool active = !paused && inputReady && Application.isFocused;
            if (physical)
            {
                bool held = active && down;
                if (physicalTouchDown[option] == held) return;
                physicalTouchDown[option] = held;
                if (journey.InForest && physicalTouchDown[1-option]) return;
            }
            if (active && down) Touch(option, physical);
        }

        private void ResolveHeldSteering(bool left, bool right)
        {
            bool active = Application.isFocused && !paused && inputReady;
            bool physicalReady = false;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            physicalReady = exhibit.Ready;
#endif
            if (!active || !physicalReady) Array.Clear(physicalTouchDown, 0, physicalTouchDown.Length);
            left = active && (left || physicalTouchDown[0]);
            right = active && (right || physicalTouchDown[1]);
            journey.SetMazeSteering(left, right);
            journey.SetForestLateralInput(left, right);
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
                PlayerPrefs.GetString(PreviousCircuitSaveKey,
                    PlayerPrefs.GetString(PreviousRuinsSaveKey, PlayerPrefs.GetString(PreviousSaveKey, ""))));
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
            SaveTuning();
#if UNITY_EDITOR
            if (Active == this) Active = null;
#endif
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            exhibit.TouchChanged -= PhysicalTouch;
            exhibit.Dispose();
            CloseDesktopCamera();
#endif
#if !UNITY_WEBGL || UNITY_EDITOR
            microphone.Stop();
#endif
            openingSound?.Dispose();
            openingSound = null;
            travelSound?.Dispose();
            travelSound = null;
            forestAudio?.Dispose();
            forestAudio = null;
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
