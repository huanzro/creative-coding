#if UNITY_EDITOR_WIN || UNITY_EDITOR_OSX || (!UNITY_EDITOR && (UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX))
#define ECHOES_DESKTOP_CAMERA
#endif
#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
#define ECHOES_WINDOWS_HARDWARE
#endif
using System;
using System.IO;
using UnityEngine;

namespace Echoes
{
    public sealed partial class EchoesPlayer
    {
        private bool deviceSettings;
        private Hand recentCameraHand;
        private float lastCameraHandAt = -10;
        private bool keyboardFist;
#if ECHOES_DESKTOP_CAMERA
        private readonly DesktopHandCamera desktopCamera = new DesktopHandCamera();
        private bool runInBackgroundBeforeCamera;
#endif
#if ECHOES_WINDOWS_HARDWARE
        private string[] serialPorts = Array.Empty<string>();
        private string comPort;
#endif
        private static string DeviceSettingsLabel =>
#if ECHOES_WINDOWS_HARDWARE
            "摄像头 / Arduino";
#else
            EchoesLocale.T("摄像头 / 麦克风", "Camera / Mic");
#endif

        private void UpdateHand(bool keyboard, bool enabled, bool operatorTouch)
        {
#if ECHOES_DESKTOP_CAMERA
            if (preview && desktopCamera.Active)
            {
                bool overrideHand = enabled && keyboard && Input.GetKey(KeyCode.Space);
                Hand observed = desktopCamera.ReadGesture(enabled && !journey.IsOpening);
                float now = Time.unscaledTime;
                if (observed != Hand.Missing)
                {
                    recentCameraHand = observed;
                    lastCameraHandAt = now;
                }
                Hand stabilized = now - lastCameraHandAt <= .16f ? recentCameraHand : Hand.Missing;
                Hand next = !enabled ? Hand.Missing : overrideHand ? Hand.Fist : keyboardFist ? Hand.Open : stabilized;
                keyboardFist = overrideHand;
                if (!enabled) lastCameraHandAt = -10;
                journey.SetHand(next, true);
                return;
            }
#endif
            keyboardFist = false;
            Hand gesture = !enabled ? Hand.Missing : !preview ? liveHand :
                noHand || !keyboard && !operatorTouch ? Hand.Missing : keyboard && Input.GetKey(KeyCode.Space) ? Hand.Fist : Hand.Open;
            journey.SetHand(gesture);
        }

        private static double IndexClock =>
#if ECHOES_DESKTOP_CAMERA
            DesktopHandCamera.Clock;
#else
            Time.realtimeSinceStartupAsDouble;
#endif

        private void UpdateIndexTip(bool keyboard, bool enabled)
        {
            if (brushPaper != journey.StarPaper)
            {
                brushPaper = journey.StarPaper;
                starBrush = default;
            }
            double now = IndexClock, capture = now;
            StarTip tip = default;
            if (enabled)
            {
#if ECHOES_DESKTOP_CAMERA
                if (preview && desktopCamera.Active)
                {
                    tip = desktopCamera.ReadIndexTip(out double sampledAt);
                    if (tip.Valid) capture = sampledAt;
                }
#endif
                if (!preview && cameraReady && now - liveIndexTipAt < .30)
                {
                    tip = liveIndexTip;
                    capture = liveIndexTipAt;
                }
                if (!tip.Valid && keyboard && Input.GetMouseButton(0))
                {
                    Rect area = view.StageViewport;
                    Vector3 mouse = Input.mousePosition;
                    float x = (mouse.x - area.x) / area.width;
                    float y = (Screen.height - mouse.y - area.y) / area.height;
                    tip = new StarTip((x - .5f) / .82f + .5f, (y - .5f) / .71f + .5f);
                }
            }
            journey.SetStarPointer(starBrush.Follow(tip, capture, now, Mathf.Min(.12f, Time.unscaledDeltaTime)));
        }

#if ECHOES_DESKTOP_CAMERA
        private void OpenCameraPage()
        {
            string url = StartDesktopCamera();
            if (url != null) Application.OpenURL(url);
        }

        private string StartDesktopCamera()
        {
            string page = Path.Combine(Application.streamingAssetsPath, "EchoesCamera");
            string media = Application.isEditor ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "dist")) : page;
            bool wasActive = desktopCamera.Active;
            string url = desktopCamera.Start(page, media);
            if (url != null && !wasActive)
            {
                runInBackgroundBeforeCamera = Application.runInBackground;
                Application.runInBackground = true;
                noHand = false;
                journey.SetHand(Hand.Missing);
            }
            return url;
        }

        private void CloseDesktopCamera()
        {
            if (!desktopCamera.Active) return;
            desktopCamera.Dispose();
            Application.runInBackground = runInBackgroundBeforeCamera;
            journey.SetHand(Hand.Missing);
        }
#endif

#if ECHOES_WINDOWS_HARDWARE
        private void RefreshSerialPorts()
        {
            serialPorts = WindowsExhibit.AvailablePorts();
            if (!exhibit.Connected && Array.IndexOf(serialPorts, comPort) < 0)
                comPort = serialPorts.Length == 0 ? null : serialPorts[0];
        }

        private void DrawArduinoSettings(GUIStyle label, GUIStyle button)
        {
            LocaleGui.Label(new Rect(20, 286, 480, 26), "Arduino 串口", label);
            int selected = Array.IndexOf(serialPorts, comPort);
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && !exhibit.Connected && serialPorts.Length > 1;
            if (LocaleGui.Button(new Rect(20, 319, 38, 32), "‹", button))
                comPort = serialPorts[(selected + serialPorts.Length - 1) % serialPorts.Length];
            if (LocaleGui.Button(new Rect(165, 319, 38, 32), "›", button))
                comPort = serialPorts[(selected + 1) % serialPorts.Length];
            GUI.enabled = enabled;
            LocaleGui.Label(new Rect(67, 320, 96, 30), comPort ?? "未检测到", label);
            if (LocaleGui.Button(new Rect(217, 319, 111, 32), "刷新端口", button)) RefreshSerialPorts();
            GUI.enabled = enabled && (exhibit.Connected || comPort != null);
            if (LocaleGui.Button(new Rect(361, 319, 143, 32), exhibit.Connected ? "断开 Arduino" : "连接 Arduino", button))
            {
                if (exhibit.Connected) exhibit.Dispose();
                else
                {
                    exhibit.Connect(comPort, Time.unscaledTime);
                    if (exhibit.Connected)
                    {
                        PlayerPrefs.SetString("EchoesUnity.Port", comPort);
                        PlayerPrefs.Save();
                    }
                }
            }
            GUI.enabled = enabled;
            string status = serialPorts.Length == 0 && !exhibit.Connected
                ? "Windows 未发现串口。用 USB 数据线连接，再点「刷新端口」。" : exhibit.Status;
            if (exhibit.Ready && !exhibit.FanSupported) status += " · 低亮度模式";
            LocaleGui.Label(new Rect(20, 364, 484, 65), status, label);
            GUI.enabled = enabled && exhibit.Ready && !paused;
            exhibit.LampsEnabled = LocaleGui.Toggle(new Rect(20, 442, 146, 28), exhibit.LampsEnabled, "启用双灯");
            GUI.enabled = GUI.enabled && exhibit.FanSupported;
            exhibit.FanEnabled = LocaleGui.Toggle(new Rect(173, 442, 154, 28), exhibit.FanEnabled, "启用风扇");
            GUI.enabled = enabled && exhibit.Connected;
            if (LocaleGui.Button(new Rect(361, 440, 143, 31), "关闭实体输出", button)) exhibit.StopOutput();
            GUI.enabled = enabled && exhibit.Ready && !exhibit.LampTesting;
            if (LocaleGui.Button(new Rect(20, 486, 146, 32), "测试双灯", button))
                exhibit.StartLampTest(Time.unscaledTime);
            GUI.enabled = enabled;
            LocaleGui.Label(new Rect(181, 481, 323, 65), exhibit.LampTestStatus, label);
        }
#endif

        private void DrawDeviceSettings(GUIStyle label, GUIStyle button)
        {
#if ECHOES_WINDOWS_HARDWARE
            LocaleGui.Label(new Rect(20, 14, 315, 28), "摄像头、麦克风与 Arduino", label);
#else
            LocaleGui.Label(new Rect(20, 14, 315, 28), EchoesLocale.T("摄像头与麦克风", "Camera and microphone"), label);
#endif
#if ECHOES_DESKTOP_CAMERA
            if (LocaleGui.Button(new Rect(20, 55, 180, 32), desktopCamera.Active ? "关闭摄像头" : "开启摄像头", button))
            {
                if (desktopCamera.Active) CloseDesktopCamera();
                else OpenCameraPage();
            }
            if (desktopCamera.Active && LocaleGui.Button(new Rect(258, 55, 245, 32), "重新打开识别页", button))
            {
                CloseDesktopCamera();
                OpenCameraPage();
            }
            LocaleGui.Label(new Rect(20, 99, 485, 55), desktopCamera.Status, label);
            string hint = paused ? "游戏已暂停，点「继续」后可用手势。" : journey.IsOpening
                ? "序幕播放中，进入关卡后可控制精灵。"
                : journey.InStars ? "仅伸出食指指向镜头。识别页亮点会跟随指尖。"
                : "在识别页点「打开独立预览」。握拳再张手即可起飞。";
            LocaleGui.Label(new Rect(20, 151, 485, 33), hint, label);
#endif
#if !UNITY_WEBGL || UNITY_EDITOR
            LocaleGui.Label(new Rect(20, 191, 105, 27), "麦克风", label);
            if (LocaleGui.Button(new Rect(20, 224, 144, 32), microphone.Active ? "关闭麦克风" : "开启麦克风", button))
                microphone.Toggle(Time.unscaledTime);
            if (LocaleGui.Button(new Rect(178, 224, 112, 32), "重新校准", button)) microphone.Calibrate();
            LocaleGui.Label(new Rect(303, 223, 202, 40), microphone.Status, label);
#endif
#if ECHOES_WINDOWS_HARDWARE
            DrawArduinoSettings(label, button);
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            LocaleGui.Label(new Rect(20, 289, 477, 65),
                EchoesLocale.T("如无法启用麦克风，请在系统设置 → 隐私与安全性 → 麦克风中检查游戏权限。",
                    "If the microphone is unavailable, check the game's permission in System Settings → Privacy & Security → Microphone."), label);
#endif
        }
    }
}
