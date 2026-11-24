using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Echoes
{
    public sealed class EchoesPlayer : MonoBehaviour
    {
        private readonly Journey journey = new Journey();
        private EchoesView view;
        private bool preview, projection, mirror, audioReady, paused;
        private Hand liveHand;
        private float liveVoice, lastHandAt, lastAudioAt, publishAt;
        private int savedRevision;
        private string published;
        private Frame remote = new Frame();
        private const string SaveKey = "EchoesUnity.Checkpoint.1.";
        private static readonly string[] Titles = { "沉睡森林", "回声之门", "星海回响" };

        [Serializable]
        private sealed class Frame
        {
            public int chapter, round, lesson, choice, flock, quota, score;
            public float time, light, charge, voice, progress;
            public bool closed, waiting, complete, paused, preview;
            public string title, message, left, right, phase;
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
            preview = Application.platform != RuntimePlatform.WebGLPlayer;
            view = new EchoesView();
            savedRevision = journey.Revision;
            Application.targetFrameRate = 60;
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLInput.captureAllKeyboardInput = false;
#endif
        }

        private void Update()
        {
            if (projection) return;
            if (preview && Input.GetKeyDown(KeyCode.A)) Choose("0");
            if (preview && Input.GetKeyDown(KeyCode.B)) Choose("1");
            if (preview && Input.GetKeyDown(KeyCode.R)) Retry("");
            if (!paused)
            {
                bool active = preview || audioReady && Time.unscaledTime - lastAudioAt < .5f &&
                    Time.unscaledTime - lastHandAt < .5f;
                var gesture = active ? (preview ? Input.GetKey(KeyCode.Space) ? Hand.Fist : Hand.Open : liveHand) : Hand.Missing;
                journey.SetHand(gesture);
                float voice = preview ? (Input.GetKey(KeyCode.X) ? .7f : Input.GetKey(KeyCode.Z) ? .3f : 0) : liveVoice;
                journey.Step(Mathf.Min(.04f, Time.unscaledDeltaTime), voice);
            }
            else journey.SetHand(Hand.Missing);
            view.Advance(journey);

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
                waiting = journey.Gesture == Hand.Missing, paused = paused, preview = preview,
                title = journey.Lesson >= 0 ? "回声庭院" : Titles[journey.Chapter],
                message = journey.Phase == Stage.Replay
                    ? journey.Message + (journey.RequiredHand == Hand.Open ? " 当前要张手。" : " 当前要握拳。")
                    : journey.Message,
                left = left, right = right, phase = journey.Phase.ToString()
            };
        }

        private void OnGUI()
        {
            if (projection)
            {
                view.DrawProjection(remote.chapter, remote.time, remote.light, remote.charge, remote.closed, mirror);
                return;
            }
            view.DrawStage(journey);
            if (Application.platform == RuntimePlatform.WebGLPlayer) return;
            var state = Snapshot();
            view.DrawControls(state.title, state.message, state.left, state.right, state.voice,
                option => Choose(option.ToString()), Select, () => Retry(""), () => Restore(""),
                () => Pause(paused ? "0" : "1"), paused);
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

        public void SetPreview(string value)
        {
            preview = value == "1";
            journey.SetHand(Hand.Missing);
        }

        public void Choose(string value)
        {
            if (!paused && int.TryParse(value, out int option)) journey.Touch(option);
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

        public void ProjectFrame(string json)
        {
            if (!projection) return;
            var next = JsonUtility.FromJson<Frame>(json);
            if (next != null) remote = next;
        }
    }
}
