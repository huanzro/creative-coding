#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
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
        private readonly ForestLampShow forestLamps = new ForestLampShow();
        private readonly ChapterLampShow chapterLamps = new ChapterLampShow();
        private readonly Dictionary<int, int> lampTestPackets = new Dictionary<int, int>();
        private readonly int[] touchPresses = new int[2], touchReleases = new int[2];
        private static readonly LampRgb TestAmber = new LampRgb(27, 17, 0);
        private static readonly LampRgb TestCyan = new LampRgb(4, 18, 25);
        private static readonly string[] LampTestSteps = { "左侧黄灯 · D6", "右侧黄灯 · D7", "双侧青灯", "双侧熄灭" };
        private bool inForest, outputWasLive;
        private bool overflow;
        private int sequence, lastMask, lastEnabled, lampTestAck;
        private float lastInput, nextWrite, lampTestAt = -1;
        private string portName;
        public bool Connected => port != null;
        public bool Ready { get; private set; }
        public bool FanSupported { get; private set; }
        public bool LampsEnabled { get; set; }
        public bool FanEnabled { get; set; }
        public string Status { get; private set; } = "串口未连接";
        public string LampTestStatus { get; private set; } = "";
        public bool LampTesting => lampTestAt >= 0;
        public event Action<int, bool> TouchChanged;
        public bool TouchEnabled(int option) => Ready && (lastEnabled & 1 << option) != 0;
        public bool TouchPressed(int option) => (lastMask & 1 << option) != 0;
        public (int presses, int releases) TouchCounts(int option) => (touchPresses[option], touchReleases[option]);

#if UNITY_EDITOR_WIN
        public static bool HasForegroundWindow
        {
            get
            {
                IntPtr window = GetForegroundWindow();
                return window != IntPtr.Zero && GetWindowThreadProcessId(window, out uint process) != 0 &&
                    process == GetCurrentProcessId();
            }
        }

        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentProcessId();
#endif

        public static string[] AvailablePorts()
        {
            // Unity's .NET Standard profile does not expose Microsoft.Win32.Registry.
            // A DOS-device query lists the COM aliases that Windows can open now.
            int count = 0;
            char[] names = null;
            for (int capacity = 4096; capacity <= 65536; capacity *= 2)
            {
                names = new char[capacity];
                count = QueryDosDevice(null, names, names.Length);
                if (count > 0 || Marshal.GetLastWin32Error() != 122) break;
            }
            if (count <= 0) return Array.Empty<string>();
            var numbers = new SortedSet<int>();
            foreach (string value in new string(names, 0, count).Split('\0'))
                if (IsPortName(value)) numbers.Add(int.Parse(value.Substring(3), CultureInfo.InvariantCulture));
            var ports = new string[numbers.Count];
            int index = 0;
            foreach (int number in numbers) ports[index++] = "COM" + number.ToString(CultureInfo.InvariantCulture);
            return ports;
        }

        private static bool IsPortName(string name) => Regex.IsMatch(name, @"^COM[1-9][0-9]{0,3}$", RegexOptions.IgnoreCase);

        public void Connect(string name, float now)
        {
            Dispose();
            name = (name ?? "").Trim().ToUpperInvariant();
            if (!IsPortName(name))
            {
                Status = "请先选择 Windows 检测到的 Arduino 串口";
                return;
            }
            try
            {
                port = OpenPort(@"\\.\" + name, 0xC0000000, 0, IntPtr.Zero, 3, 0, IntPtr.Zero);
                if (port.IsInvalid) throw Error();
                var state = new SerialState { Length = (uint)Marshal.SizeOf<SerialState>() };
                if (!GetCommState(port, ref state)) throw Error();
                state.BaudRate = 115200;
                state.Flags = 1 | (1u << 4); // Binary data and DTR_CONTROL_ENABLE for the Uno USB serial link.
                state.ByteSize = 8;
                state.Parity = state.StopBits = 0;
                if (!SetCommState(port, ref state)) throw Error();
                var timeouts = new SerialTimeouts { ReadInterval = uint.MaxValue, WriteConstant = 80 };
                if (!SetCommTimeouts(port, ref timeouts)) throw Error();
                lastInput = now;
                nextWrite = now;
                sequence = 0;
                portName = name;
                LampsEnabled = true;
                Status = name + " 已打开，等待 EOL3";
            }
            catch (Win32Exception error)
            {
                Dispose();
                Status = error.NativeErrorCode == 2 || error.NativeErrorCode == 3
                    ? name + " 已不在系统中。重新插好数据线后刷新端口。"
                    : error.NativeErrorCode == 5 || error.NativeErrorCode == 32
                        ? name + " 无法独占。关闭 Arduino 串口监视器或其他占用程序后重试。"
                        : name + " 连接失败（Windows 错误 " + error.NativeErrorCode + "） " + error.Message;
            }
        }

        public void Tick(float now, bool running, bool focused, float energy, Journey journey)
        {
            if (!Connected) return;
            try
            {
                if (inForest != journey.InForest)
                {
                    inForest = journey.InForest;
                    lampEnd[0] = lampEnd[1] = -1;
                }
                if (!ReadFile(port, received, received.Length, out int bytes, IntPtr.Zero)) throw Error();
                for (int i = 0; i < bytes; i++) ByteReceived((char)received[i], now);
                if (Ready && now - lastInput > 1.5f)
                {
                    SuspendOutput();
                    Ready = FanSupported = false;
                    Status = "EOL3 心跳中断，输出已停止";
                }
                if (!Ready && now - lastInput > 4.5f)
                    Status = portName + " 已打开，未收到 EOL3。请确认已烧录项目固件。";
                if (!focused || !running && !LampTesting)
                {
                    SuspendOutput();
                    forestLamps.Reset(journey.CueId, journey.ForestSwallowing);
                    chapterLamps.Reset(journey.CueId);
                    return;
                }
                LampRgb forest = default;
                if (Ready && running && LampsEnabled && inForest && !LampTesting)
                    forest = forestLamps.Sample(journey, now);
                else forestLamps.Reset(journey.CueId, journey.ForestSwallowing);
                LampRgb chapterLeft = default, chapterRight = default;
                if (Ready && running && LampsEnabled && !LampTesting && (journey.InRuins || journey.InStars))
                    chapterLamps.Sample(journey, now, out chapterLeft, out chapterRight);
                else chapterLamps.Reset(journey.CueId);
                if (!Ready || now < nextWrite) return;
                nextWrite = now + .05f;
                if (LampTesting)
                {
                    TickLampTest(now);
                    return;
                }
                int fan = FanEnabled ? (int)Math.Round(Math.Max(0, Math.Min(1, energy)) * 132) : 0;
                LampRgb left = inForest ? forest : ExhibitProtocol.LeftTouch.Dim(Lamp(0, now));
                LampRgb right = inForest ? forest : ExhibitProtocol.RightTouch.Dim(Lamp(1, now));
                if (journey.InRuins || journey.InStars)
                {
                    left = chapterLeft;
                    right = chapterRight;
                }
                WriteOutput(left, right, fan);
            }
            catch (Win32Exception error)
            {
                string name = portName;
                Dispose();
                Status = name + " 串口读写中断（Windows 错误 " + error.NativeErrorCode + "）。请重新连接。";
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
            if (LampTesting && ExhibitProtocol.ReadAck(frame, out int acknowledged))
            {
                if (lampTestPackets.TryGetValue(acknowledged, out int step))
                {
                    lampTestAck |= 1 << step;
                    lampTestPackets.Remove(acknowledged);
                }
                return;
            }
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
                bool pressed = (mask & bit) != 0;
                if (pressed) touchPresses[option]++;
                else touchReleases[option]++;
                TouchChanged?.Invoke(option, pressed);
                if (!pressed && !inForest)
                    lampEnd[option] = Math.Min(lampEnd[option], Math.Max(lampBegin[option] + .5f, now));
            }
        }

        public void Flash(int option, float now, bool physical)
        {
            if (!Ready || !LampsEnabled) return;
            lampBegin[option] = now;
            lampEnd[option] = now + (physical ? 1.8f : .5f);
        }

        public void StartLampTest(float now)
        {
            if (!Ready || LampTesting) return;
            lampTestAt = nextWrite = now;
            lampTestAck = 0;
            LampTestStatus = LampTestSteps[0];
        }

        private void TickLampTest(float now)
        {
            float age = now - lampTestAt;
            if (age >= 2.35f)
            {
                if (outputWasLive) WriteOutput(default, default, 0);
                int acknowledged = 0;
                for (int i = 0; i < LampTestSteps.Length; i++)
                    if ((lampTestAck & 1 << i) != 0) acknowledged++;
                string result = acknowledged == LampTestSteps.Length
                    ? "Arduino 已确认 4/4。请核对两侧实际亮灯。"
                    : "Arduino 已确认 " + acknowledged + "/4。请检查固件和串口。";
                FinishLampTest(result);
                return;
            }
            int step = age < .64f ? 0 : age < 1.28f ? 1 : age < 1.92f ? 2 : 3;
            LampTestStatus = LampTestSteps[step];
            LampRgb left = step == 0 ? TestAmber : step == 2 ? TestCyan : default;
            LampRgb right = step == 1 ? TestAmber : step == 2 ? TestCyan : default;
            WriteOutput(left, right, 0, step);
        }

        private void FinishLampTest(string status)
        {
            LampTestStatus = status;
            lampTestAt = -1;
            lampTestPackets.Clear();
        }

        public void ClearLamp(int option) => lampEnd[option] = -1;

        private float Lamp(int option, float now)
        {
            if (!LampsEnabled) return 0;
            if (lampEnd[option] < 0) return 0;
            return Math.Max(0, Math.Min(1, 1 - (now - lampEnd[option]) / .42f));
        }

        public void StopOutput()
        {
            LampsEnabled = false;
            SuspendOutput();
        }

        public void SuspendOutput()
        {
            FanEnabled = false;
            lampEnd[0] = lampEnd[1] = -1;
            forestLamps.Reset();
            if (LampTesting) FinishLampTest("双灯测试已停止。");
            if (outputWasLive && Ready && port != null && !port.IsInvalid)
            {
                try { WriteOutput(default, default, 0); }
                catch (Win32Exception) { /* Firmware timeout stops output when the cable is gone. */ }
            }
            outputWasLive = false;
        }

        private void WriteOutput(LampRgb left, LampRgb right, int fan, int testStep = -1)
        {
            int packet = sequence++ & 65535;
            Write(ExhibitProtocol.Output(packet, left, right, fan, FanSupported));
            outputWasLive = left.Lit || right.Lit || fan > 0;
            if (testStep >= 0 && (lampTestAck & 1 << testStep) == 0) lampTestPackets[packet] = testStep;
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
            portName = null;
            Ready = FanSupported = overflow = false;
            Array.Clear(touchPresses, 0, touchPresses.Length);
            Array.Clear(touchReleases, 0, touchReleases.Length);
            line.Clear();
            Status = "串口未连接";
            LampTestStatus = "";
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
        [DllImport("kernel32.dll", EntryPoint = "QueryDosDeviceW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int QueryDosDevice(string device, [Out, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.U2)] char[] result, int capacity);
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
