using System;
using System.IO;
using UnityEngine;

namespace Echoes
{
    public sealed partial class EchoesPlayer
    {
        private bool deviceSettings;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        private readonly DesktopHandCamera desktopCamera = new DesktopHandCamera();
        private bool runInBackgroundBeforeCamera;
        private string[] serialPorts = Array.Empty<string>();
        private string comPort;
#endif

        private void UpdateHand(bool keyboard, bool enabled)
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            if (preview && desktopCamera.Active)
            {
                journey.SetHand(desktopCamera.ReadGesture(enabled && !journey.IsOpening), true);
                return;
            }
#endif
            Hand gesture = !enabled ? Hand.Missing : !preview ? liveHand :
                noHand || !keyboard ? Hand.Missing : Input.GetKey(KeyCode.Space) ? Hand.Fist : Hand.Open;
            journey.SetHand(gesture);
        }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
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

        private void RefreshSerialPorts()
        {
            serialPorts = WindowsExhibit.AvailablePorts();
            if (!exhibit.Connected && Array.IndexOf(serialPorts, comPort) < 0)
                comPort = serialPorts.Length == 0 ? null : serialPorts[0];
        }

        private void DrawArduinoSettings(GUIStyle label, GUIStyle button)
        {
            GUI.Label(new Rect(20, 286, 480, 26), "Arduino 串口", label);
            int selected = Array.IndexOf(serialPorts, comPort);
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && !exhibit.Connected && serialPorts.Length > 1;
            if (GUI.Button(new Rect(20, 319, 38, 32), "‹", button))
                comPort = serialPorts[(selected + serialPorts.Length - 1) % serialPorts.Length];
            if (GUI.Button(new Rect(165, 319, 38, 32), "›", button))
                comPort = serialPorts[(selected + 1) % serialPorts.Length];
            GUI.enabled = enabled;
            GUI.Label(new Rect(67, 320, 96, 30), comPort ?? "未检测到", label);
            if (GUI.Button(new Rect(217, 319, 111, 32), "刷新端口", button)) RefreshSerialPorts();
            GUI.enabled = enabled && (exhibit.Connected || comPort != null);
            if (GUI.Button(new Rect(361, 319, 143, 32), exhibit.Connected ? "断开 Arduino" : "连接 Arduino", button))
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
            GUI.Label(new Rect(20, 364, 484, 65), status, label);
            GUI.enabled = enabled && exhibit.Ready && !paused;
            exhibit.LampsEnabled = GUI.Toggle(new Rect(20, 442, 146, 28), exhibit.LampsEnabled, "启用双灯");
            GUI.enabled = GUI.enabled && exhibit.FanSupported;
            exhibit.FanEnabled = GUI.Toggle(new Rect(173, 442, 154, 28), exhibit.FanEnabled, "启用风扇");
            GUI.enabled = enabled && exhibit.Connected;
            if (GUI.Button(new Rect(361, 440, 143, 31), "关闭实体输出", button)) exhibit.StopOutput();
            GUI.enabled = enabled && exhibit.Ready && !exhibit.LampTesting;
            if (GUI.Button(new Rect(20, 486, 146, 32), "测试双灯", button))
                exhibit.StartLampTest(Time.unscaledTime);
            GUI.enabled = enabled;
            GUI.Label(new Rect(181, 481, 323, 65), exhibit.LampTestStatus, label);
        }
#endif

        private void DrawDeviceSettings(GUIStyle label, GUIStyle button)
        {
            GUI.Label(new Rect(20, 14, 315, 28), "摄像头、麦克风与 Arduino", label);
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            if (GUI.Button(new Rect(20, 55, 180, 32), desktopCamera.Active ? "关闭摄像头" : "开启摄像头", button))
            {
                if (desktopCamera.Active) CloseDesktopCamera();
                else OpenCameraPage();
            }
            if (desktopCamera.Active && GUI.Button(new Rect(258, 55, 245, 32), "重新打开识别页", button))
            {
                CloseDesktopCamera();
                OpenCameraPage();
            }
            GUI.Label(new Rect(20, 99, 485, 55), desktopCamera.Status, label);
            string hint = paused ? "游戏已暂停，点「继续」后可用手势。" : journey.IsOpening
                ? "序幕播放中，进入关卡后可控制精灵。"
                : "在识别页点「打开独立预览」。握拳再张手即可起飞。";
            GUI.Label(new Rect(20, 151, 485, 33), hint, label);
#endif
#if !UNITY_WEBGL || UNITY_EDITOR
            GUI.Label(new Rect(20, 191, 105, 27), "麦克风", label);
            if (GUI.Button(new Rect(20, 224, 144, 32), microphone.Active ? "关闭麦克风" : "开启麦克风", button))
                microphone.Toggle(Time.unscaledTime);
            if (GUI.Button(new Rect(178, 224, 112, 32), "重新校准", button)) microphone.Calibrate();
            GUI.Label(new Rect(303, 223, 202, 40), microphone.Status, label);
#endif
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            DrawArduinoSettings(label, button);
#endif
        }
    }
}
