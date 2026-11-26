using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows.Forms;

internal static class EchoesInstaller
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            string folder;
            if (args.Length == 2 && args[0] == "--extract")
                folder = args[1];
            else
            {
                folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Creative Coding", "Echoes of Light");
                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "Choose where to install Echoes of Light / 选择《光的回响》的安装目录";
                    dialog.SelectedPath = Path.GetDirectoryName(folder);
                    if (dialog.ShowDialog() != DialogResult.OK) return 0;
                    folder = Path.Combine(dialog.SelectedPath, "Echoes of Light");
                }
            }
            string root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(root);
            using (var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("Echoes.Windows.zip"))
            using (var archive = new ZipArchive(payload, ZipArchiveMode.Read))
                foreach (var item in archive.Entries)
                {
                    string target = Path.GetFullPath(Path.Combine(root, item.FullName));
                    if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        throw new IOException("The installer archive contains an invalid path.");
                    if (string.IsNullOrEmpty(item.Name)) Directory.CreateDirectory(target);
                    else
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(target));
                        item.ExtractToFile(target, true);
                    }
                }
            string game = Path.Combine(root, "Echoes of Light.exe");
            if (!File.Exists(game)) throw new FileNotFoundException("The game executable was not extracted.");
            if (args.Length == 0 && MessageBox.Show("安装完成。现在启动游戏？\nInstalled. Start Echoes of Light now?",
                "Echoes of Light", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                Process.Start(new ProcessStartInfo(game) { WorkingDirectory = root });
            return 0;
        }
        catch (Exception error)
        {
            if (args.Length == 0)
                MessageBox.Show(error.Message, "安装失败 / Installation failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}
