using System;
using System.IO;
using System.Text;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#else
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
#endif
using UnityEngine;

namespace Echoes
{
    internal sealed class StarKeepsakeShare : IDisposable
    {
        private readonly byte[] png;
        public string LocalPath { get; }
        public Texture2D Qr { get; }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void EchoesDownloadKeepsake(byte[] image, int size);
#else
        private readonly TcpListener listener;
        private readonly string route;
        private volatile bool serving;
#endif

        public StarKeepsakeShare(byte[] png)
        {
            this.png = png;
#if !UNITY_WEBGL || UNITY_EDITOR
            try
            {
                string folder = Path.Combine(Application.persistentDataPath, "Star Sea keepsakes");
                Directory.CreateDirectory(folder);
                LocalPath = Path.Combine(folder, "Echoes-of-Light-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".png");
                File.WriteAllBytes(LocalPath, png);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                LocalPath = null;
                Debug.LogWarning("Could not write the Star Sea keepsake: " + e.Message);
            }
            try
            {
                IPAddress address = PrivateAddress();
                if (address == null) return;
                listener = new TcpListener(address, 0);
                listener.Start(4);
                route = "/e/" + Guid.NewGuid().ToString("N").Substring(0, 16);
                Qr = StarKeepsakeQr.Texture("http://" + listener.LocalEndpoint + route);
                if (Qr == null) { listener.Stop(); return; }
                serving = true;
                var server = new Thread(Serve) { IsBackground = true, Name = "Star Sea keepsake" };
                server.Start();
            }
            catch (Exception e) when (e is SocketException || e is NetworkInformationException)
            {
                listener?.Stop();
                Debug.LogWarning("Star Sea keepsake QR is unavailable: " + e.Message);
            }
#endif
        }

        public void Open()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            EchoesDownloadKeepsake(png, png.Length);
#else
            if (LocalPath != null) Application.OpenURL(new Uri(Path.GetDirectoryName(LocalPath)).AbsoluteUri);
#endif
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        private static IPAddress PrivateAddress()
        {
            IPAddress selected = null;
            int best = 0;
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                bool wifi = adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211;
                string label = (adapter.Name + " " + adapter.Description).ToLowerInvariant();
                bool hotspot = label.Contains("wi-fi direct") || label.Contains("hosted network");
                if (adapter.OperationalStatus != OperationalStatus.Up ||
                    (!wifi && !hotspot && adapter.NetworkInterfaceType != NetworkInterfaceType.Ethernet &&
                        adapter.NetworkInterfaceType != NetworkInterfaceType.GigabitEthernet)) continue;
                if ((!hotspot && label.Contains("virtual")) || label.Contains("vpn") || label.Contains("hyper-v")) continue;
                IPInterfaceProperties network = adapter.GetIPProperties();
                int priority = 1 + (wifi ? 2 : 0) + (network.GatewayAddresses.Count > 0 ? 4 : 0) + (hotspot ? 10 : 0);
                foreach (UnicastIPAddressInformation entry in network.UnicastAddresses)
                {
                    if (priority <= best || entry.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    byte[] ip = entry.Address.GetAddressBytes();
                    if (ip[0] != 10 && !(ip[0] == 192 && ip[1] == 168) && !(ip[0] == 172 && ip[1] >= 16 && ip[1] <= 31))
                        continue;
                    selected = entry.Address;
                    best = priority;
                }
            }
            return selected;
        }

        private void Serve()
        {
            while (serving)
            {
                try
                {
                    using (TcpClient client = listener.AcceptTcpClient())
                    {
                        client.ReceiveTimeout = 1500;
                        client.SendTimeout = 1800;
                        using (NetworkStream connection = client.GetStream()) Respond(connection);
                    }
                }
                catch (Exception e) when (e is IOException || e is SocketException ||
                    e is ObjectDisposedException || e is InvalidOperationException) { }
            }
        }

        private void Respond(NetworkStream connection)
        {
            // Only this one image is served. No file path from the phone reaches the filesystem.
            var request = new byte[4096];
            int count = 0;
            string headers = "";
            while (count < request.Length)
            {
                int received = connection.Read(request, count, request.Length - count);
                if (received <= 0) return;
                count += received;
                headers = Encoding.ASCII.GetString(request, 0, count);
                if (headers.Contains("\r\n\r\n")) break;
            }
            int lineEnd = headers.IndexOf("\r\n", StringComparison.Ordinal);
            if (lineEnd < 0) return;
            string[] line = headers.Substring(0, lineEnd).Split(' ');
            bool available = line.Length == 3 && (line[0] == "GET" || line[0] == "HEAD") && line[1] == route;
            string response = available
                ? "HTTP/1.1 200 OK\r\nContent-Type: image/png\r\nContent-Disposition: attachment; filename=\"Echoes-of-Light.png\"\r\nContent-Length: " + png.Length
                : "HTTP/1.1 404 Not Found\r\nContent-Length: 0";
            byte[] header = Encoding.ASCII.GetBytes(response + "\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nConnection: close\r\n\r\n");
            connection.Write(header, 0, header.Length);
            if (available && line[0] == "GET") connection.Write(png, 0, png.Length);
        }
#endif

        public void Dispose()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            serving = false;
            listener?.Stop();
#endif
            if (Qr != null) UnityEngine.Object.Destroy(Qr);
        }
    }
}
