using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace Community.PowerToys.Run.Plugin.CursorWorkspaces.CursorHelper;

public static class CursorInstances
{
    private static readonly string UserAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    private static readonly string LocalAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public static List<CursorInstance> Instances { get; } = new();

    private static BitmapImage BitmapToBitmapImage(Bitmap bitmap)
    {
        using var memory = new MemoryStream();
        bitmap.Save(memory, ImageFormat.Png);
        memory.Position = 0;

        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.StreamSource = memory;
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.EndInit();
        bitmapImage.Freeze();

        return bitmapImage;
    }

    private static Bitmap BitmapOverlayToCenter(Bitmap bitmap1, Bitmap overlayBitmap)
    {
        int bitmap1Width = bitmap1.Width;
        int bitmap1Height = bitmap1.Height;
        bitmap1.SetResolution(144, 144);
        using Bitmap overlayBitmapResized = new(overlayBitmap, new System.Drawing.Size(bitmap1Width / 2, bitmap1Height / 2));

        float marginLeft = (float)((bitmap1Width * 0.7) - (overlayBitmapResized.Width * 0.5));
        float marginTop = (float)((bitmap1Height * 0.7) - (overlayBitmapResized.Height * 0.5));

        Bitmap finalBitmap = new(bitmap1Width, bitmap1Height);
        using Graphics g = Graphics.FromImage(finalBitmap);
        g.DrawImage(bitmap1, System.Drawing.Point.Empty);
        g.DrawImage(overlayBitmapResized, marginLeft, marginTop);

        return finalBitmap;
    }

    /// <summary>定位 Cursor 安装目录与用户数据目录（与 VS Code 相同布局）。</summary>
    public static void LoadCursorInstances()
    {
        Instances.Clear();

        var exeCandidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string standard = Path.Combine(LocalAppDataPath, "Programs", "cursor", "Cursor.exe");
        if (File.Exists(standard))
        {
            exeCandidates.Add(Path.GetFullPath(standard));
        }

        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            foreach (var segment in pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var dir = segment.Trim();
                if (dir.Length == 0 || !Directory.Exists(dir))
                {
                    continue;
                }

                var cursorExe = Path.Combine(dir, "Cursor.exe");
                if (File.Exists(cursorExe))
                {
                    exeCandidates.Add(Path.GetFullPath(cursorExe));
                }
            }
        }

        foreach (var file in exeCandidates)
        {
            var instance = TryCreateInstance(file);
            if (instance != null)
            {
                Instances.Add(instance);
            }
        }
    }

    private static CursorInstance? TryCreateInstance(string file)
    {
        if (!File.Exists(file))
        {
            return null;
        }

        string iconDir = Path.GetDirectoryName(file.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? string.Empty;
        string portableData = Path.Join(iconDir, "data");
        string appData = Directory.Exists(portableData)
            ? Path.Join(portableData, "user-data")
            : Path.Combine(UserAppDataPath, "Cursor");

        using Icon? cursorIcon = Icon.ExtractAssociatedIcon(file);
        if (cursorIcon is null)
        {
            return null;
        }

        string pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
        string folderPng = Path.Join(pluginDir, "Images", "folder.png");
        string monitorPng = Path.Join(pluginDir, "Images", "monitor.png");

        using var cursorIconBitmap = cursorIcon.ToBitmap();
        BitmapImage workspaceBitmap;
        BitmapImage remoteBitmap;

        if (File.Exists(folderPng) && File.Exists(monitorPng))
        {
            using var folderIcon = (Bitmap)Image.FromFile(folderPng);
            using var bitmapFolderIcon = BitmapOverlayToCenter(folderIcon, cursorIconBitmap);
            using var monitorIcon = (Bitmap)Image.FromFile(monitorPng);
            using var bitmapMonitorIcon = BitmapOverlayToCenter(monitorIcon, cursorIconBitmap);
            workspaceBitmap = BitmapToBitmapImage(bitmapFolderIcon);
            remoteBitmap = BitmapToBitmapImage(bitmapMonitorIcon);
        }
        else
        {
            workspaceBitmap = BitmapToBitmapImage((Bitmap)cursorIconBitmap.Clone());
            remoteBitmap = BitmapToBitmapImage((Bitmap)cursorIconBitmap.Clone());
        }

        return new CursorInstance
        {
            ExecutablePath = file,
            AppData = appData,
            WorkspaceIconBitMap = workspaceBitmap,
            RemoteIconBitMap = remoteBitmap,
        };
    }
}
