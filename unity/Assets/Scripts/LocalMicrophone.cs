#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Echoes
{
    internal sealed class LocalMicrophone
    {
        private AudioClip clip;
        private string device, profile;
        private float[] buffer;
        private readonly List<float> quiet = new List<float>();
        private readonly List<float> comfortable = new List<float>();
        private float quietDb, fullDb, elapsed, lastSample, started;
        private int position = -1;
        public bool Active => clip != null;
        public bool Ready { get; private set; }
        public float Level { get; private set; }
        public string Status { get; private set; } = "未启用";

        public void Toggle(float now)
        {
            if (Active) { Stop(); return; }
            var devices = Microphone.devices;
            if (devices.Length == 0) { Status = "未找到麦克风"; return; }
            device = devices[0];
            profile = "EchoesUnity.Mic.1." + device;
            clip = Microphone.Start(device, true, 1, 44100);
            if (clip == null) { Status = "麦克风无法开启"; return; }
            buffer = new float[1024 * clip.channels];
            started = lastSample = now;
            position = -1;
            quietDb = PlayerPrefs.GetFloat(profile + ".quiet", 0);
            fullDb = PlayerPrefs.GetFloat(profile + ".full", 0);
            Ready = quietDb < 0 && fullDb > quietDb + 6 && fullDb <= 0;
            if (!Ready) Calibrate();
        }

        public void Calibrate()
        {
            if (!Active) return;
            Ready = false;
            Level = elapsed = 0;
            quiet.Clear();
            comfortable.Clear();
            Status = "安静 2 秒";
        }

        public void Update(float now, float dt)
        {
            if (!Active) return;
            int cursor = Microphone.GetPosition(device);
            if (cursor == position || cursor < 0 || (now - started < .25f && cursor < 1024))
            {
                if (now - lastSample > 1.25f) { Stop(); Status = "麦克风信号中断"; }
                return;
            }
            position = cursor;
            lastSample = now;
            if (!clip.GetData(buffer, (cursor + clip.samples - 1024) % clip.samples)) return;
            double mean = 0, squared = 0;
            foreach (float sample in buffer) { mean += sample; squared += sample * sample; }
            mean /= buffer.Length;
            float db = (float)(10 * Math.Log10(Math.Max(1e-10, squared / buffer.Length - mean * mean)));
            if (!Ready)
            {
                elapsed += dt;
                if (elapsed < 2) { quiet.Add(db); Status = "安静 " + Mathf.CeilToInt(2 - elapsed) + " 秒"; }
                else if (elapsed < 5)
                {
                    if (elapsed > 2.3f) comfortable.Add(db);
                    Status = "舒适哼声 " + Mathf.CeilToInt(5 - elapsed) + " 秒";
                }
                else FinishCalibration();
                return;
            }
            float normalized = Mathf.Clamp01((db - quietDb) / (fullDb - quietDb));
            float target = normalized * normalized * (3 - 2 * normalized);
            Level += (target - Level) * (1 - Mathf.Exp(-dt / (target > Level ? .14f : .28f)));
            Status = "实时音量 " + Mathf.RoundToInt(Level * 100) + "%";
        }

        private void FinishCalibration()
        {
            if (quiet.Count > 0 && comfortable.Count > 0)
            {
                quiet.Sort();
                comfortable.Sort();
                quietDb = Mathf.Clamp(quiet[(int)((quiet.Count - 1) * .9f)] + 6, -75, -15);
                float hummed = comfortable[(int)((comfortable.Count - 1) * .7f)];
                fullDb = Mathf.Min(0, quietDb + (hummed - quietDb) / .72f);
                if (hummed >= quietDb + 8 && hummed <= quietDb + (fullDb - quietDb) * .8f)
                {
                    PlayerPrefs.SetFloat(profile + ".quiet", quietDb);
                    PlayerPrefs.SetFloat(profile + ".full", fullDb);
                    PlayerPrefs.Save();
                    Ready = true;
                    return;
                }
            }
            Stop();
            Status = "未测出明显音量变化，请重试";
        }

        public void Stop()
        {
            if (Active)
            {
                Microphone.End(device);
                UnityEngine.Object.Destroy(clip);
            }
            clip = null;
            Ready = false;
            Level = 0;
            Status = "未启用";
        }
    }
}
#endif
