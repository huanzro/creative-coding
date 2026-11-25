#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Echoes
{
    // MediaPipe stays on the permission page. The loopback carries pose and normalized index coordinates.
    internal sealed class DesktopHandCamera : IDisposable
    {
        private sealed class Sample
        {
            public readonly string State;
            public readonly StarTip Tip;
            public readonly long At = Stopwatch.GetTimestamp();
            public Sample(string state, StarTip tip = default) { State = state; Tip = tip; }
        }

        private sealed class Session
        {
            private const int PoseQueueLimit = 4;
            private const double PoseTimeoutSeconds = 1.25;
            public readonly TcpListener Listener = new TcpListener(IPAddress.Loopback, 0);
            public readonly string Token = Guid.NewGuid().ToString("N");
            private readonly string pageRoot, mediaRoot;
            private readonly object sampleLock = new object();
            private readonly Queue<Sample> pendingPoses = new Queue<Sample>(PoseQueueLimit);
            private volatile bool running = true;
            private volatile Sample latest = new Sample("waiting");
            private int requests;

            public Session(string pageRoot, string mediaRoot)
            {
                this.pageRoot = pageRoot;
                this.mediaRoot = mediaRoot;
            }

            public string Url => $"http://127.0.0.1:{((IPEndPoint)Listener.LocalEndpoint).Port}/{Token}/";
            public bool Live => running;
            public string State
            {
                get
                {
                    Sample sample = latest;
                    if (Expired(sample) && (IsPose(sample.State) || sample.State == "missing"))
                        return "stale";
                    return sample.State;
                }
            }

            private static bool Expired(Sample sample) =>
                Stopwatch.GetTimestamp() - sample.At > PoseTimeoutSeconds * Stopwatch.Frequency;
            private static bool IsPose(string state) => state == "open" || state == "closed";

            public StarTip ReadIndexTip()
            {
                Sample sample = latest;
                return Stopwatch.GetTimestamp() - sample.At <= .30 * Stopwatch.Frequency ? sample.Tip : default;
            }

            private void Receive(string state, StarTip tip = default)
            {
                lock (sampleLock)
                {
                    string previous = State;
                    latest = new Sample(state, tip);
                    if (!IsPose(state))
                    {
                        pendingPoses.Clear();
                        pendingPoses.Enqueue(latest);
                    }
                    else if (state != previous)
                    {
                        // Preserve short pose changes until the next rendered frames can consume them.
                        if (previous == "stale" || pendingPoses.Count == PoseQueueLimit)
                        {
                            pendingPoses.Clear();
                            pendingPoses.Enqueue(new Sample("missing"));
                        }
                        pendingPoses.Enqueue(latest);
                    }
                }
            }

            public Hand ReadGesture(bool enabled)
            {
                lock (sampleLock)
                {
                    if (!enabled || State == "stale" || pendingPoses.Count != 0 && Expired(pendingPoses.Peek()))
                    {
                        pendingPoses.Clear();
                        return Hand.Missing;
                    }
                    string state = pendingPoses.Count == 0 ? latest.State : pendingPoses.Dequeue().State;
                    return state == "open" ? Hand.Open : state == "closed" ? Hand.Fist : Hand.Missing;
                }
            }

            public async Task Run()
            {
                try
                {
                    while (running)
                    {
                        TcpClient client = await Listener.AcceptTcpClientAsync().ConfigureAwait(false);
                        if (Interlocked.Increment(ref requests) > 8)
                        {
                            Interlocked.Decrement(ref requests);
                            client.Dispose();
                            continue;
                        }
                        _ = Task.Run(() => Serve(client));
                    }
                }
                catch (Exception error) when (error is SocketException || error is ObjectDisposedException)
                {
                    if (running) Receive("bridge-error");
                }
                finally { running = false; }
            }

            public void Stop()
            {
                running = false;
                Listener.Stop();
            }

            private void Serve(TcpClient client)
            {
                try
                {
                    using (client)
                    {
                        client.ReceiveTimeout = 2500;
                        client.SendTimeout = 5000;
                        using (NetworkStream stream = client.GetStream())
                        {
                            string request = ReadRequest(stream);
                            if (request == null) return;
                            string[] line = request.Split(' ');
                            string prefix = "/" + Token + "/";
                            if (line.Length != 3 || !line[1].StartsWith(prefix, StringComparison.Ordinal))
                            {
                                Reply(stream, "404 Not Found", null, 0);
                                return;
                            }
                            string path = line[1].Substring(prefix.Length);
                            if (line[0] == "POST" && path.StartsWith("state/", StringComparison.Ordinal))
                            {
                                if (StarTip.Decode(path.Substring(6), out string state, out StarTip tip) &&
                                    ValidState(state) && (!tip.Valid || IsPose(state) || state == "missing"))
                                {
                                    Receive(state, tip);
                                    Reply(stream, "204 No Content", null, 0);
                                    return;
                                }
                            }
                            if (line[0] == "GET" && TryAsset(path, out string file, out string kind) && File.Exists(file))
                            {
                                using (var asset = File.OpenRead(file))
                                {
                                    Reply(stream, "200 OK", kind, asset.Length);
                                    asset.CopyTo(stream);
                                }
                            }
                            else Reply(stream, "404 Not Found", null, 0);
                        }
                    }
                }
                catch (Exception error) when (error is IOException || error is SocketException || error is ObjectDisposedException || error is UnauthorizedAccessException)
                {
                    // The browser can cancel requests when the page or the camera closes.
                }
                finally { Interlocked.Decrement(ref requests); }
            }

            private bool TryAsset(string path, out string file, out string kind)
            {
                string root = mediaRoot;
                switch (path)
                {
                    case "": path = "index.html"; goto case "index.html";
                    case "index.html": kind = "text/html; charset=utf-8"; root = pageRoot; break;
                    case "camera.mjs": kind = "text/javascript; charset=utf-8"; root = pageRoot; break;
                    case "index-tip.mjs": kind = "text/javascript; charset=utf-8"; root = pageRoot; break;
                    case "core.js":
                    case "vendor/vision_bundle.mjs":
                    case "vendor/wasm/vision_wasm_internal.js":
                    case "vendor/wasm/vision_wasm_nosimd_internal.js": kind = "text/javascript; charset=utf-8"; break;
                    case "vendor/wasm/vision_wasm_internal.wasm":
                    case "vendor/wasm/vision_wasm_nosimd_internal.wasm": kind = "application/wasm"; break;
                    case "vendor/hand_landmarker.task": kind = "application/octet-stream"; break;
                    default: file = kind = null; return false;
                }
                file = Path.Combine(root, path);
                return true;
            }

            private static bool ValidState(string state) => state == "open" || state == "closed" || state == "missing" ||
                state == "loading" || state == "off" || state == "not-found" || state == "denied" || state == "busy" || state == "model-error";

            private static string ReadRequest(NetworkStream stream)
            {
                var buffer = new byte[8192];
                int count = 0, firstLine = -1;
                while (count < buffer.Length)
                {
                    int previous = count;
                    int read = stream.Read(buffer, count, buffer.Length - count);
                    if (read == 0) return null;
                    count += read;
                    for (int i = Math.Max(0, previous - 3); i + 1 < count; i++)
                    {
                        if (buffer[i] != 13 || buffer[i + 1] != 10) continue;
                        if (firstLine < 0) firstLine = i;
                        if (i + 3 < count && buffer[i + 2] == 13 && buffer[i + 3] == 10)
                            return firstLine <= 512 ? Encoding.ASCII.GetString(buffer, 0, firstLine) : null;
                    }
                }
                return null;
            }

            private static void Reply(NetworkStream stream, string status, string kind, long length)
            {
                string mime = kind == null ? "" : "Content-Type: " + kind + "\r\n";
                var header = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + "\r\nConnection: close\r\n" + mime +
                    "Cache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nContent-Length: " +
                    length.ToString(CultureInfo.InvariantCulture) + "\r\n\r\n");
                stream.Write(header, 0, header.Length);
            }
        }

        private Session session;
        private string error = "摄像头已关闭";
        public bool Active => session != null;
        public Hand ReadGesture(bool enabled) => session?.ReadGesture(enabled) ?? Hand.Missing;
        public StarTip ReadIndexTip() => session?.ReadIndexTip() ?? default;
        public string Status
        {
            get
            {
                if (!Active) return error;
                if (ReadIndexTip().Valid) return "摄像头已启用，识别到食指坐标";
                switch (session.State)
                {
                    case "open": return "摄像头已启用，识别到张手";
                    case "closed": return "摄像头已启用，识别到握拳";
                    case "missing": return "摄像头已启用，请把一只手放入画面";
                    case "loading": return "等待摄像头权限或正在载入识别模型";
                    case "denied": return "浏览器未获摄像头权限，请在识别页允许后重试";
                    case "busy": return "摄像头被占用或不可读，关闭其他占用程序后重试";
                    case "not-found": return "未检测到摄像头，请检查插线和系统权限";
                    case "off": return "识别页已关闭摄像头，点「重新打开识别页」恢复";
                    case "model-error": return "识别模型未启动，请查看识别页提示";
                    case "stale": return "摄像头信号中断，请检查识别页是否仍在运行";
                    case "bridge-error": return "本机连接已结束，请关闭后重新开启摄像头";
                    default: return "识别页已启动，请在弹出的浏览器中允许摄像头";
                }
            }
        }

        public string Start(string pageRoot, string mediaRoot)
        {
            if (session != null && session.Live) return session.Url;
            session?.Stop();
            session = null;
            if (!File.Exists(Path.Combine(pageRoot, "camera.mjs")) ||
                !File.Exists(Path.Combine(mediaRoot, "vendor", "hand_landmarker.task")))
            {
                error = "摄像头资源不完整，请从仓库运行或重新构建 Windows 版";
                return null;
            }
            var next = new Session(pageRoot, mediaRoot);
            try { next.Listener.Start(8); }
            catch (SocketException)
            {
                next.Stop();
                error = "无法启动本机摄像头连接，请重试";
                return null;
            }
            session = next;
            _ = next.Run();
            return next.Url;
        }

        public void Dispose()
        {
            session?.Stop();
            session = null;
            error = "摄像头已关闭";
        }
    }
}
#endif
