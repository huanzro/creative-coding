#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace Echoes
{
    // Windows owns the one exclusive COM handle for both inputs and outputs.
    internal sealed class WindowsExhibit : IDisposable
    {
        private SafeFileHandle port;
        private readonly byte[] received = new byte[128];
        private readonly StringBuilder line = new StringBuilder(80);
        private readonly float[] lampBegin = new float[2];
        private readonly float[] lampEnd = { -1, -1 };
        private readonly float[] lampStrength = new float[2];
        private bool forestPalette;
        private bool overflow;
        private int sequence, lastMask, lastEnabled;
        private float lastInput, nextWrite;
        public bool Connected => port != null;
        public bool Ready { get; private set; }
        public bool FanSupported { get; private set; }
        public bool LampsEnabled { get; set; }
        public bool FanEnabled { get; set; }
        public bool HoldingAuthorization { get; set; }
        public string Status { get; private set; } = "串口未连接";
        public event Action<int> Pressed;

        public void Connect(string name, float now)
        {
            Dispose();
            name = name.Trim().ToUpperInvariant();
            if (!Regex.IsMatch(name, @"^COM[1-9][0-9]{0,3}$"))
            {
                Status = "输入设备管理器中的端口，例如 COM3";
                return;
            }
            try
            {
                port = OpenPort(@"\\.\" + name, 0xC0000000, 0, IntPtr.Zero, 3, 0, IntPtr.Zero);
                if (port.IsInvalid) throw Error();
                var state = new SerialState { Length = (uint)Marshal.SizeOf<SerialState>() };
                if (!GetCommState(port, ref state)) throw Error();
                state.BaudRate = 115200;
                state.Flags = 1;
                state.ByteSize = 8;
                state.Parity = state.StopBits = 0;
                if (!SetCommState(port, ref state)) throw Error();
                var timeouts = new SerialTimeouts { ReadInterval = uint.MaxValue, WriteConstant = 80 };
                if (!SetCommTimeouts(port, ref timeouts)) throw Error();
                lastInput = now;
                nextWrite = now;
                sequence = 0;
                Status = name + " 已打开，等待 EOL3";
            }
            catch (Win32Exception error)
            {
                Dispose();
                Status = "串口无法打开。 " + error.Message;
            }
        }

        public void SetForestPalette(bool enabled)
        {
            if (forestPalette == enabled) return;
            forestPalette = enabled;
            lampEnd[0] = lampEnd[1] = -1;
        }

        public void Tick(float now, bool running, float energy)
        {
            if (!Connected) return;
            try
            {
                if (!ReadFile(port, received, received.Length, out int bytes, IntPtr.Zero)) throw Error();
                for (int i = 0; i < bytes; i++) ByteReceived((char)received[i], now);
                if (Ready && now - lastInput > 1.5f)
                {
                    StopOutput();
                    Ready = FanSupported = false;
                    Status = "EOL3 心跳中断，输出已停止";
                }
                if (!running && (LampsEnabled || FanEnabled)) StopOutput();
                if (!Ready || now < nextWrite) return;
                nextWrite = now + .1f;
                int fan = FanEnabled ? (int)Math.Round(Math.Max(0, Math.Min(1, energy)) * 132) : 0;
                Write(ExhibitProtocol.Output(sequence++, Lamp(0, now), Lamp(1, now), fan, FanSupported, forestPalette));
            }
            catch (Win32Exception error)
            {
                Dispose();
                Status = "串口连接中断。 " + error.Message;
            }
        }

        private void ByteReceived(char value, float now)
        {
            if (value == '\n')
            {
                if (!overflow) HandleFrame(line.ToString().TrimEnd('\r'), now);
                overflow = false;
                line.Clear();
            }
            else if (line.Length < 80) line.Append(value);
            else overflow = true;
        }

        private void HandleFrame(string frame, float now)
        {
            if (frame == "EOL3 CAP FAN") { FanSupported = true; return; }
            if (!ExhibitProtocol.ReadInputs(frame, out int mask, out int enabled)) return;
            int changed = Ready ? (mask ^ lastMask) & enabled & lastEnabled : 0;
            lastInput = now;
            Ready = true;
            lastMask = mask;
            lastEnabled = enabled;
            Status = (enabled & 2) == 0 ? "EOL3 已连接 · B 未启用" : "EOL3 已连接";
            for (int option = 0; option < 2; option++)
            {
                int bit = 1 << option;
                if ((changed & bit) == 0) continue;
                if ((mask & bit) != 0) Pressed?.Invoke(option);
                else if (!forestPalette) lampEnd[option] = Math.Min(lampEnd[option], Math.Max(lampBegin[option] + .5f, now));
            }
        }

        public void Flash(int option, float now, bool physical)
        {
            if (!Ready || !LampsEnabled) return;
            lampBegin[option] = now;
            lampEnd[option] = now + (physical ? 1.8f : .5f);
            lampStrength[option] = 1;
        }

        public void FlashForest(bool red, float now)
        {
            if (!Ready || !LampsEnabled || !forestPalette) return;
            int index = red ? 1 : 0;
            lampBegin[index] = now;
            lampEnd[index] = now + .21f;
            lampStrength[index] = red ? .52f : .63f;
        }

        public void ClearLamp(int option) => lampEnd[option] = -1;

        private float Lamp(int option, float now)
        {
            if (!LampsEnabled) return 0;
            if (option == 1 && HoldingAuthorization) return 1;
            if (lampEnd[option] < 0) return 0;
            float fadeOut = Math.Max(0, Math.Min(1, 1 - (now - lampEnd[option]) / .42f));
            float fadeIn = forestPalette ? Math.Max(0, Math.Min(1, (now - lampBegin[option]) / .08f)) : 1;
            return lampStrength[option] * fadeIn * fadeOut;
        }

        public void StopOutput()
        {
            LampsEnabled = FanEnabled = false;
            lampEnd[0] = lampEnd[1] = -1;
            if (Ready && port != null && !port.IsInvalid)
            {
                try { Write(ExhibitProtocol.Output(sequence++, 0, 0, 0, FanSupported)); }
                catch (Win32Exception) { /* Firmware timeout stops output when the cable is gone. */ }
            }
        }

        private void Write(string message)
        {
            var bytes = Encoding.ASCII.GetBytes(message);
            if (!WriteFile(port, bytes, bytes.Length, out int sent, IntPtr.Zero) || sent != bytes.Length)
                throw Error();
        }

        public void Dispose()
        {
            StopOutput();
            port?.Dispose();
            port = null;
            Ready = FanSupported = overflow = false;
            line.Clear();
            Status = "串口未连接";
        }

        private static Win32Exception Error() => new Win32Exception(Marshal.GetLastWin32Error());

        [StructLayout(LayoutKind.Sequential)]
        private struct SerialState
        {
            public uint Length, BaudRate, Flags;
            public ushort Reserved, XonLimit, XoffLimit;
            public byte ByteSize, Parity, StopBits, Xon, Xoff, Error, Eof, Event;
            public ushort Reserved1;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SerialTimeouts
        {
            public uint ReadInterval, ReadMultiplier, ReadConstant, WriteMultiplier, WriteConstant;
        }

        [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle OpenPort(string name, uint access, uint share,
            IntPtr security, uint creation, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetCommState(SafeFileHandle file, ref SerialState state);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetCommState(SafeFileHandle file, ref SerialState state);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetCommTimeouts(SafeFileHandle file, ref SerialTimeouts timeouts);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadFile(SafeFileHandle file, [Out] byte[] buffer, int toRead,
            out int read, IntPtr overlapped);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteFile(SafeFileHandle file, byte[] buffer, int toWrite,
            out int written, IntPtr overlapped);
    }
}
#endif
