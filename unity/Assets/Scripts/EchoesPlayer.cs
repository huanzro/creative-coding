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
        private static readonly string[] CastNames = { "三维精灵", "绿金长裙精灵", "兜帽旅人", "星纹术士", "红色小鸟", "黑天鹅" };
        private Hand liveHand;
        private StarTip liveIndexTip;
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
#endif
        private int savedRevision, savedStarInk;
        private float nextInkSave;
        private const string LateralKey = "EchoesUnity.Forest.LateralScale";
        private const string MazeKey = "EchoesUnity.Forest.MazeScale";
        private const string WebKey = "EchoesUnity.Forest.WebRange";
        private const string MawKey = "EchoesUnity.Forest.MawRange";
        private const string RuinLeftReachKey = "EchoesUnity.Ruins.LeftReach";
        private const string RuinRightReachKey = "EchoesUnity.Ruins.RightReach";
        private const string RuinUpperReachKey = "EchoesUnity.Ruins.UpperReach";
        private const string RuinLowerReachKey = "EchoesUnity.Ruins.LowerReach";
        private const string JumpHeightKey = "EchoesUnity.Flight.JumpHeight";
        private const string DoorJumpHeightKey = "EchoesUnity.Flight.DoorJumpHeight";
        private const string BrightnessKey = "EchoesUnity.Projection.Brightness";
        private const string BackgroundBrightnessKey = "EchoesUnity.Projection.BackgroundBrightness";
        private const string SpiderBrightnessKey = "EchoesUnity.Projection.ForestSpiderBrightness";
        private const string MawBrightnessKey = "EchoesUnity.Projection.ForestMawBrightness";
        private const string FalloffKey = "EchoesUnity.Projection.Falloff";
        private const string GradientKey = "EchoesUnity.Projection.Gradient";
        private const string StageScaleKey = "EchoesUnity.Projection.StageScale";
        private bool projectionSettings, flightSettings, ruinSettings, changedTuning;
        private readonly bool[] physicalTouchDown = new bool[2];
        private readonly bool[] browserTouch = new bool[2];
        private Frame remote = new Frame();
        private const string SaveKey = "EchoesUnity.Checkpoint.Ruins.11";
        private const string PreviousRouteSaveKey = "EchoesUnity.Checkpoint.Ruins.10";
        private const string PreviousRuinExhibitsSaveKey = "EchoesUnity.Checkpoint.Ruins.9";
        private const string PreviousRuinLootSaveKey = "EchoesUnity.Checkpoint.Ruins.8";
        private const string PreviousGallerySaveKey = "EchoesUnity.Checkpoint.Ruins.7";
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
            public int chapter, lesson, choice, flock, quota;
            public float time, light, charge, voice, progress;
            public bool closed, waiting, complete, paused, preview;
            public string title, message, left, right, phase;
            public SpiritFrame spirit = new SpiritFrame { releaseAge = -1, turnAge = -1, touchAge = -1, openingTime = -1 };
            public StarInkFrame starArtwork;
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
            journey.MeasureRuinPassage = MeasureRuinPassage;
            view.UseWorld = PlayerPrefs.GetInt("EchoesUnity.World3D", 1) != 0;
            view.ScreenBrightness = PlayerPrefs.GetFloat(BrightnessKey, ProjectionLighting.DefaultValue);
            view.BackgroundBrightness = PlayerPrefs.GetFloat(BackgroundBrightnessKey, ProjectionLighting.DefaultValue);
            view.ForestSpiderBrightness = PlayerPrefs.GetFloat(SpiderBrightnessKey, ProjectionLighting.DefaultValue);
            view.ForestMawBrightness = PlayerPrefs.GetFloat(MawBrightnessKey, ProjectionLighting.DefaultValue);
            view.ProjectionFalloff = PlayerPrefs.GetFloat(FalloffKey, ProjectionLighting.DefaultValue);
            view.ProjectionGradient = PlayerPrefs.GetInt(GradientKey, 1) != 0;
            view.StageScale = PlayerPrefs.GetFloat(StageScaleKey, 1);
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
            journey.ForestFireflyRange = PlayerPrefs.GetFloat(FireflyRangeKey, Journey.DefaultForestFireflyRange);
            journey.RuinLeftReach = PlayerPrefs.GetFloat(RuinLeftReachKey, 1);
            journey.RuinRightReach = PlayerPrefs.GetFloat(RuinRightReachKey, 1);
            journey.RuinUpperReach = PlayerPrefs.GetFloat(RuinUpperReachKey, 1);
            journey.RuinLowerReach = PlayerPrefs.GetFloat(RuinLowerReachKey, 1);
            journey.JumpHeightScale = PlayerPrefs.GetFloat(JumpHeightKey, Journey.DefaultJumpHeightScale);
            journey.ForestDoorJumpHeightScale = PlayerPrefs.GetFloat(DoorJumpHeightKey, Journey.DefaultForestDoorJumpHeightScale);
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
            UpdateHand(keyboard, !paused && (inputReady || journey.InStars));
            if (journey.InStars) UpdateIndexTip(keyboard, !paused && !settings, now);
            bool leftHeld = keyboard && Input.GetKey(KeyCode.A);
            bool rightHeld = keyboard && Input.GetKey(KeyCode.B);
            if (keyboard && Input.GetKeyDown(KeyCode.A) && (!journey.InForest || !rightHeld)) TrackTouch(0, true, false);
            if (keyboard && Input.GetKeyDown(KeyCode.B) && (!journey.InForest || !leftHeld)) TrackTouch(1, true, false);
            ResolveHeldSteering(leftHeld || view.ScreenTouchHeld(0), rightHeld || view.ScreenTouchHeld(1));
            if (!paused)
            {
                bool advance = inputReady || journey.InStars;
                bool hear = journey.InStars ? preview || audioReady && now - lastAudioAt < .5f : inputReady;
                journey.Step(journey.IsOpening ? Time.unscaledDeltaTime : dt, hear ? voice : 0, advance);
            }
            UpdateMazeMap(keyboard);
            view.Advance(journey);
            openingSound.Follow(journey.IsOpening ? journey.OpeningTime : -1, paused);
            travelSound.Follow(journey, paused);
            forestAudio.Follow(journey,paused);
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            exhibit.Tick(now, !paused && !journey.IsOpening, Application.isFocused,
                Mathf.Max(journey.Voice * .42f, journey.WindOutput), journey);
#endif

        }

        private void SaveProgress(float now, bool flush = false)
        {
            if (forestDebugPoint.HasValue || journey.IsOpening || view == null) return;
            bool artworkChanged = journey.InStars && savedStarInk != view.StarInkRevision;
            if (savedRevision == journey.Revision && (!artworkChanged || !flush && now < nextInkSave)) return;
            var checkpoint = journey.Checkpoint;
            if (journey.InStars)
            {
                checkpoint.starArtwork = JsonUtility.ToJson(view.StarArtwork);
                savedStarInk = view.StarInkRevision;
                nextInkSave = now + .85f;
            }
            savedRevision = journey.Revision;
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(checkpoint));
            PlayerPrefs.Save();
        }

        private void Publish()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (Time.unscaledTime - publishAt > .08f)
            {
                publishAt = Time.unscaledTime;
                string json = JsonUtility.ToJson(Snapshot(splitProjection));
                if (json != published) { published = json; EchoesPublish(json); }
            }
#endif
        }

        private Frame Snapshot(bool includeArtwork = false)
        {
            string message = journey.InStars ? journey.StarInstruction : journey.Message;
            if (journey.ForestMazeWalkArea && journey.Phase == Stage.ForestExplore && string.IsNullOrEmpty(message))
                message = Journey.MazeWalkHint;
            return new Frame {
                chapter = journey.Chapter, lesson = journey.Lesson, choice = journey.ForestRoute,
                flock = journey.InRuins ? journey.RuinInventory : journey.InForest ? journey.ForestLights : journey.Plants,
                quota = journey.InRuins ? Journey.RuinCapacity : journey.InForest ? journey.ForestPlantsRequired : 2,
                time = journey.Time,
                light = journey.Light, charge = journey.Charge, voice = journey.Voice, progress = journey.Progress,
                closed = journey.Gesture == Hand.Fist, complete = journey.Phase == Stage.Complete,
                waiting = !journey.InStars && (!inputReady || !journey.Started), paused = paused, preview = preview,
                title = journey.IsOpening ? "序幕" : journey.Lesson >= 0 ? "回声庭院 · 试玩" : Titles[journey.Chapter],
                message = !journey.IsOpening && !journey.InStars && !paused && inputReady && journey.Gesture == Hand.Missing
                    ? "请把手放回画面，张开手继续。 " + message : message,
                left = journey.LeftLabel, right = journey.RightLabel, phase = journey.Phase.ToString(), spirit = LocalSpirit,
                starArtwork = includeArtwork && journey.InStars ? view.StarArtwork : null
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
            if (projection) return;
            if (!characterOnly || ShowProjectionCalibration || journey.InStars)
                view.UpdateWorld(journey, frame, StageSplit, ShowProjectionCalibration);
            SaveProgress(Time.unscaledTime);
            Publish();
        }

        private void OnGUI()
        {
            if (projection)
            {
                view.DrawRemoteProjection(remote.spirit, mirror, remote.starArtwork);
                return;
            }
            if (Event.current.type == EventType.Repaint)
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.blackTexture, ScaleMode.StretchToFill, false);
            SpiritFrame spirit = LocalSpirit;
            if (characterOnly && !ShowProjectionCalibration) view.DrawProjection(spirit, mirror, true, view.StageViewport);
            else view.DrawStage(journey, spirit, StageSplit, ShowProjectionCalibration);
            if (!settings && !characterOnly && journey.ForestMazeActive)
                DrawMazeMapControls();
            if (Application.platform == RuntimePlatform.WebGLPlayer) return;
            if (journey.IsOpening && !settings)
            {
                if (!characterOnly) view.DrawOpeningControls(journey.OpeningTime, journey.FinishOpening);
                return;
            }
            var state = Snapshot();
            string heading = forestDebugPoint.HasValue ? "调试 · " + ForestDebugLabels[(int)forestDebugPoint.Value] : state.title;
            view.DrawControls(heading, state.message, state.left, state.right, state.voice,
                option => Choose(option.ToString()), Select, () => Retry(""), () => Restore(""),
                () => Pause(paused ? "0" : "1"), paused, characterOnly, journey.ForestMazeWalkArea,
                journey.InForest || journey.InRuins && journey.Phase == Stage.RuinExplore && !journey.RuinLootOpen,
                journey.InStars,
                ToggleSettings, settings ? DrawSettings : (Action<GUIStyle, GUIStyle>)null);
        }

        private void DrawSettings(GUIStyle label, GUIStyle button)
        {
            if (forestDebugSettings)
            {
                DrawForestDebugSettings(label, button);
                return;
            }
            if (ruinSettings)
            {
                if (GUI.Button(new Rect(354, 12, 148, 31), "返回游戏设置", button)) ruinSettings = false;
                else DrawRuinSettings(label, button);
                return;
            }
            if (flightSettings)
            {
                if (GUI.Button(new Rect(354, 12, 148, 31), "返回游戏设置", button)) flightSettings = false;
                else DrawFlightSettings(label, button);
                return;
            }
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
            int picked = GUI.SelectionGrid(new Rect(116, 231, 388, 62), cast, CastNames, 3, button);
            if (picked != cast)
            {
                cast = picked;
                PlayerPrefs.SetInt("EchoesUnity.Cast", cast);
                PlayerPrefs.Save();
            }
            bool world = GUI.Toggle(new Rect(20, 305, 300, 27), view.UseWorld, "试玩使用三维背景");
            if (GUI.Button(new Rect(336, 304, 166, 30), "森林调试断点", button)) forestDebugSettings = true;
            if (world != view.UseWorld)
            {
                view.UseWorld = world;
                PlayerPrefs.SetInt("EchoesUnity.World3D", world ? 1 : 0);
                PlayerPrefs.Save();
            }
            GUI.Label(new Rect(20, 352, 285, 26), "森林 · 速度与捕捉范围", label);
            if (GUI.Button(new Rect(293, 349, 116, 29), "遗迹入口范围", button)) ruinSettings = true;
            if (GUI.Button(new Rect(416, 349, 86, 29), "跳跃高度", button)) flightSettings = true;
            journey.ForestLateralScale = TuningSlider(381, "左右移速", LateralKey, journey.ForestLateralScale,
                Journey.MinForestLateralScale, Journey.MaxForestLateralScale, label);
            journey.MazeWalkScale = TuningSlider(414, "迷宫行走速度", MazeKey, journey.MazeWalkScale,
                Journey.MinimumMazeWalkScale, Journey.MaximumMazeWalkScale, label);
            journey.ForestWebRange = TuningSlider(447, "蛛网捕捉范围", WebKey, journey.ForestWebRange,
                Journey.MinForestCaptureRange, Journey.MaxForestCaptureRange, label);
            journey.ForestMawRange = TuningSlider(480, "食人花捕捉范围", MawKey, journey.ForestMawRange,
                Journey.MinForestCaptureRange, Journey.MaxForestCaptureRange, label);
            journey.ForestFireflyRange = TuningSlider(513, "萤火接触范围", FireflyRangeKey, journey.ForestFireflyRange,
                Journey.MinForestFireflyRange, Journey.MaxForestFireflyRange, label);
        }

        private void DrawRuinSettings(GUIStyle label, GUIStyle button)
        {
            const float minimum = Journey.RuinMinimumReach, maximum = Journey.RuinMaximumReach;
            GUI.Label(new Rect(20, 14, 315, 28), "遗迹入口范围", label);
            GUI.Label(new Rect(20, 54, 480, 52), "倍率越大，越早能从附近转入对应的岔道。四个方向可以分别调节。", label);
            journey.RuinLeftReach = TuningSlider(112, "左侧 · 按 A", RuinLeftReachKey, journey.RuinLeftReach, minimum, maximum, label);
            journey.RuinRightReach = TuningSlider(153, "右侧 · 按 B", RuinRightReachKey, journey.RuinRightReach, minimum, maximum, label);
            journey.RuinUpperReach = TuningSlider(194, "上方 · 张手", RuinUpperReachKey, journey.RuinUpperReach, minimum, maximum, label);
            journey.RuinLowerReach = TuningSlider(235, "下方 · 下降", RuinLowerReachKey, journey.RuinLowerReach, minimum, maximum, label);
            if (!GUI.Button(new Rect(20, 288, 141, 30), "恢复默认", button)) return;
            journey.RuinLeftReach = journey.RuinRightReach = journey.RuinUpperReach = journey.RuinLowerReach = 1;
            PlayerPrefs.DeleteKey(RuinLeftReachKey);
            PlayerPrefs.DeleteKey(RuinRightReachKey);
            PlayerPrefs.DeleteKey(RuinUpperReachKey);
            PlayerPrefs.DeleteKey(RuinLowerReachKey);
            changedTuning = true;
        }

        private void DrawFlightSettings(GUIStyle label, GUIStyle button)
        {
            GUI.Label(new Rect(20, 14, 315, 28), "跳跃高度", label);
            journey.JumpHeightScale = TuningSlider(73, "常规单跳高度", JumpHeightKey, journey.JumpHeightScale,
                Journey.MinJumpHeightScale, Journey.MaxJumpHeightScale, label);
            GUI.Label(new Rect(20, 103, 484, 33), "每次握拳再张手触发一次起跳。", label);

            journey.ForestDoorJumpHeightScale = TuningSlider(153, "右路门前单跳", DoorJumpHeightKey, journey.ForestDoorJumpHeightScale,
                Journey.MinForestDoorJumpHeightScale, Journey.MaxForestDoorJumpHeightScale, label);
            GUI.Label(new Rect(20, 183, 484, 53), "进入森林右路的机器门廊后，门前的所有入口共用这个高度。", label);
            if (!GUI.Button(new Rect(20, 251, 141, 30), "恢复默认", button)) return;
            journey.JumpHeightScale = Journey.DefaultJumpHeightScale;
            journey.ForestDoorJumpHeightScale = Journey.DefaultForestDoorJumpHeightScale;
            PlayerPrefs.DeleteKey(JumpHeightKey);
            PlayerPrefs.DeleteKey(DoorJumpHeightKey);
            changedTuning = true;
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
            view.ScreenBrightness = TuningSlider(90, "全屏亮度", BrightnessKey, view.ScreenBrightness,
                ProjectionLighting.MinimumBrightness, ProjectionLighting.MaximumBrightness, label, "");
            DrawLightingCurve(139, "输出亮度", "原画亮度", false, label);
            view.ProjectionFalloff = TuningSlider(232, "渐亮速率", FalloffKey, view.ProjectionFalloff,
                ProjectionLighting.MinimumFalloff, ProjectionLighting.MaximumFalloff, label);
            DrawLightingCurve(281, "背景亮度倍率", "到人物的距离", true, label);
            view.BackgroundBrightness = TuningSlider(379, "背景亮度", BackgroundBrightnessKey, view.BackgroundBrightness,
                ProjectionLighting.MinimumLayerBrightness, ProjectionLighting.MaximumLayerBrightness, label);
            view.ForestSpiderBrightness = TuningSlider(410, "蜘蛛动画亮度", SpiderBrightnessKey, view.ForestSpiderBrightness,
                ProjectionLighting.MinimumLayerBrightness, ProjectionLighting.MaximumLayerBrightness, label);
            view.ForestMawBrightness = TuningSlider(441, "食人花动画亮度", MawBrightnessKey, view.ForestMawBrightness,
                ProjectionLighting.MinimumLayerBrightness, ProjectionLighting.MaximumLayerBrightness, label);
            view.StageScale = TuningSlider(472, "主画面大小", StageScaleKey, view.StageScale,
                EchoesView.MinimumStageScale, 1, label, "%");
            GUI.Label(new Rect(20, 500, 482, 27), view.BackgroundBrightness <= 0 ? "背景已设为纯黑。" :
                !view.ProjectionGradient ? "渐亮已关闭，可在上方重新启用。" :
                $"人物背后保留当前背景亮度的 {ProjectionLighting.CenterGain(view.ProjectionFalloff):0%}。", label);
            if (!GUI.Button(new Rect(20, 533, 141, 30), "恢复默认", button)) return;
            view.ScreenBrightness = view.ProjectionFalloff = ProjectionLighting.DefaultValue;
            view.BackgroundBrightness = view.ForestSpiderBrightness = view.ForestMawBrightness = ProjectionLighting.DefaultValue;
            view.ProjectionGradient = true;
            view.StageScale = 1;
            PlayerPrefs.DeleteKey(BrightnessKey);
            PlayerPrefs.DeleteKey(BackgroundBrightnessKey);
            PlayerPrefs.DeleteKey(SpiderBrightnessKey);
            PlayerPrefs.DeleteKey(MawBrightnessKey);
            PlayerPrefs.DeleteKey(FalloffKey);
            PlayerPrefs.DeleteKey(GradientKey);
            PlayerPrefs.DeleteKey(StageScaleKey);
            changedTuning = true;
        }

        private void DrawLightingCurve(float y, string vertical, string horizontal, bool radial, GUIStyle label)
        {
            var plot = new Rect(22, y, 478, 64);
            GUI.Label(new Rect(plot.x, plot.y-26, 240, 25), vertical, label);
            GUI.Label(new Rect(plot.xMax-116, plot.yMax+1, 118, 25), horizontal + " →", label);
            if (Event.current.type != EventType.Repaint) return;
            var previousColor = GUI.color;
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
            const int segments = 192;
            float column = plot.width / segments;
            float lastY = plot.yMax;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float level = radial ? ProjectionLighting.RadialGain(t * 1.24f, view.ProjectionCoreRadius, view.ProjectionFalloff) :
                    ProjectionLighting.Tone(t, view.ScreenBrightness);
                float nextY = plot.yMax - level * plot.height;
                if (i > 0)
                {
                    float top = Mathf.Max(plot.y, Mathf.Min(lastY, nextY) - .8f);
                    float bottom = Mathf.Min(plot.yMax, Mathf.Max(lastY, nextY) + .8f);
                    GUI.DrawTexture(new Rect(plot.x + (i - 1) * column, top, column, bottom - top), pixel);
                }
                lastY = nextY;
            }
            GUI.color = previousColor;
        }

        private float TuningSlider(float y,string title,string key,float value,float min,float max,GUIStyle label,string unit = "×")
        {
            string display = unit == "%" ? value.ToString("0%") : value.ToString("0.00") + unit;
            GUI.Label(new Rect(20,y,223,24), title + "  " + display,label);
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
            forestDebugPoint = null;
            settings = paused = characterOnly = false;
            journey.PlayOpening();
        }

        public void BrowserHand(string value)
        {
            if (!StarTip.Decode(value, out string state, out liveIndexTip)) state = "missing";
            liveHand = state == "open" ? Hand.Open : state == "closed" ? Hand.Fist : Hand.Missing;
            lastHandAt = Time.unscaledTime;
            if (liveHand == Hand.Missing && !preview)
            {
                Array.Clear(browserTouch, 0, browserTouch.Length);
                journey.SetHand(Hand.Missing);
            }
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

        public void BrowserTouch(string value)
        {
            if (!int.TryParse(value, out int input) || input < 0 || input > 3) return;
            int option = input / 2;
            bool down = (input & 1) != 0;
            if (!down)
            {
                browserTouch[option] = false;
                return;
            }
            if (preview || paused || !inputReady && !journey.InStars || browserTouch[option]) return;
            browserTouch[option] = true;
            Touch(option, false);
        }

        private static RuinTransferMetrics MeasureRuinPassage(Journey game)
        {
            RuinPortal portal = game.RuinEntrance;
            RuinTrackConnection path = WorldSet.RuinConnection(game, portal);
            RuinRouteFrame start = WorldSet.RuinPassageStart(game);
            float distance = path.TravelMetres(start);
            float source = RuinTrack.RouteMetresPerDepth(game.RuinRoomId, game.RuinRoomLength);
            float destination = RuinTrack.RouteMetresPerDepth(portal.TargetRoom, game.RuinRoomLengthFor(portal.TargetRoom));
            return new RuinTransferMetrics(distance, source, destination, progress => path.SafeTransferSpeed(start, progress));
        }

        private void TrackTouch(int option, bool down, bool physical)
        {
            bool active = !paused && (inputReady || journey.InStars) && Application.isFocused;
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
            bool playing = Application.isFocused && !paused;
            bool active = playing && inputReady;
            bool touchesActive = playing && (inputReady || journey.InStars);
            bool physicalReady = false;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            physicalReady = exhibit.Ready;
#endif
            if (!touchesActive || !physicalReady) Array.Clear(physicalTouchDown, 0, physicalTouchDown.Length);
            left = active && (left || physicalTouchDown[0]);
            right = active && (right || physicalTouchDown[1]);
            journey.SetMazeSteering(left, right);
            journey.SetForestLateralInput(left, right);
            if (!touchesActive || preview) Array.Clear(browserTouch, 0, browserTouch.Length);
            journey.SetRuinSteering(left || browserTouch[0], right || browserTouch[1]);
        }

        private void Touch(int option, bool physical)
        {
            int cueBefore = journey.CueId;
            bool steering = journey.InForest || journey.InRuins;
            if (paused || !inputReady && !journey.InStars || !journey.Touch(option)) return;
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
            if (journey.InStars) SaveProgress(Time.unscaledTime, true);
            forestDebugPoint = null;
            // Selecting a chapter is an explicit entry point, not a progress checkpoint.
            if (chapter == -1) journey.SelectPractice();
            else journey.SelectChapter(chapter);
            savedRevision = journey.Revision;
        }

        public void Retry(string unused)
        {
            if (forestDebugPoint.HasValue) StartForestDebugPoint(forestDebugPoint.Value);
            else journey.Retry();
        }

        public void Restore(string unused)
        {
            string save = PlayerPrefs.GetString(SaveKey,
                PlayerPrefs.GetString(PreviousRouteSaveKey, PlayerPrefs.GetString(PreviousRuinExhibitsSaveKey,
                    PlayerPrefs.GetString(PreviousRuinLootSaveKey,
                        PlayerPrefs.GetString(PreviousGallerySaveKey,
                            PlayerPrefs.GetString(PreviousCircuitSaveKey,
                                PlayerPrefs.GetString(PreviousRuinsSaveKey, PlayerPrefs.GetString(PreviousSaveKey, ""))))))));
            if (string.IsNullOrEmpty(save)) return;
            try
            {
                var checkpoint = JsonUtility.FromJson<JourneyCheckpoint>(save);
                if (!journey.Restore(checkpoint)) return;
                if (journey.InStars) view.RestoreStarArtwork(journey, checkpoint);
            }
            catch (ArgumentException) { return; }
            forestDebugPoint = null;
            savedRevision = journey.Revision;
            savedStarInk = view.StarInkRevision;
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
            if (!projection && journey.InStars) SaveProgress(Time.unscaledTime, true);
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
            if (!focused) exhibit.SuspendOutput();
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
