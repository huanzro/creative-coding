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
        private string[] serialPorts = Array.Empty<string>();
        private string comPort;
#endif

        private Hand PreviewHand(bool keyboard)
        {
            if (noHand) return Hand.Missing;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            if (desktopCamera.Active) return desktopCamera.Gesture;
#endif
            return !keyboard ? Hand.Missing : Input.GetKey(KeyCode.Space) ? Hand.Fist : Hand.Open;
        }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        private void OpenCameraPage()
        {
            noHand = false;
            string page = Path.Combine(Application.streamingAssetsPath, "EchoesCamera");
            string media = Application.isEditor ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "dist")) : page;
            string url = desktopCamera.Start(page, media);
            if (url != null) Application.OpenURL(url);
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
            GUI.Label(new Rect(20, 364, 484, 65), status, label);
            GUI.enabled = enabled && exhibit.Ready && !paused;
            exhibit.LampsEnabled = GUI.Toggle(new Rect(20, 442, 146, 28), exhibit.LampsEnabled, "启用双灯");
            GUI.enabled = GUI.enabled && exhibit.FanSupported;
            exhibit.FanEnabled = GUI.Toggle(new Rect(173, 442, 154, 28), exhibit.FanEnabled, "启用风扇");
            GUI.enabled = enabled && exhibit.Connected;
            if (GUI.Button(new Rect(361, 440, 143, 31), "关闭实体输出", button)) exhibit.StopOutput();
            GUI.enabled = enabled;
        }
#endif

        private void DrawDeviceSettings(GUIStyle label, GUIStyle button)
        {
            GUI.Label(new Rect(20, 14, 315, 28), "摄像头、麦克风与 Arduino", label);
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            if (GUI.Button(new Rect(20, 55, 180, 32), desktopCamera.Active ? "关闭摄像头" : "开启摄像头", button))
            {
                if (desktopCamera.Active) desktopCamera.Dispose();
                else OpenCameraPage();
            }
            if (desktopCamera.Active && GUI.Button(new Rect(258, 55, 245, 32), "重新打开识别页", button))
            {
                desktopCamera.Dispose();
                OpenCameraPage();
            }
            GUI.Label(new Rect(20, 99, 485, 55), desktopCamera.Status, label);
            GUI.Label(new Rect(20, 151, 485, 33), "在浏览器允许摄像头后回到 Unity，保持识别页打开。", label);
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
