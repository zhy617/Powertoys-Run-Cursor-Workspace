using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media.Imaging;

namespace Community.PowerToys.Run.Plugin.CursorWorkspaces.CursorHelper;

public static class CursorInstances
{
    private static readonly string UserAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    private static readonly string LocalAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static readonly string UserProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static List<CursorInstance> Instances { get; } = new();

    private static BitmapImage BitmapImageFromFile(string absolutePath)
    {
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.UriSource = new Uri(absolutePath, UriKind.Absolute);
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
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

        foreach (var scoopExe in EnumerateScoopCursorExeCandidates())
        {
            exeCandidates.Add(scoopExe);
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

                // Windows 上文件名大小写不敏感；Scoop shim 常为 cursor.exe
                foreach (var name in new[] { "Cursor.exe", "cursor.exe" })
                {
                    var cursorExe = Path.Combine(dir, name);
                    if (File.Exists(cursorExe))
                    {
                        exeCandidates.Add(Path.GetFullPath(cursorExe));
                    }
                }
            }
        }

        var resolvedExe = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in exeCandidates)
        {
            var r = ResolveExecutablePath(file);
            if (File.Exists(r))
            {
                resolvedExe.Add(Path.GetFullPath(r));
            }
        }

        foreach (var file in resolvedExe)
        {
            var instance = TryCreateInstance(file);
            if (instance != null)
            {
                Instances.Add(instance);
            }
        }
    }

    /// <summary>Scoop 常见安装位置（用户目录 / SCOOP 环境变量 / 全局 ProgramData）。</summary>
    private static IEnumerable<string> EnumerateScoopCursorExeCandidates()
    {
        var roots = new List<string>();
        if (!string.IsNullOrEmpty(UserProfilePath))
        {
            roots.Add(Path.Combine(UserProfilePath, "scoop"));
        }

        string? scoopEnv = Environment.GetEnvironmentVariable("SCOOP");
        if (!string.IsNullOrEmpty(scoopEnv))
        {
            roots.Add(scoopEnv);
        }

        string globalScoop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "scoop");
        if (Directory.Exists(globalScoop))
        {
            roots.Add(globalScoop);
        }

        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string current = Path.Combine(root, "apps", "cursor", "current", "Cursor.exe");
            if (File.Exists(current))
            {
                yield return Path.GetFullPath(current);
            }
        }
    }

    /// <summary>PATH 里指向的是 shims 下的启动器时，改用 apps\cursor\current 里的真实可执行文件。</summary>
    private static string ResolveExecutablePath(string file)
    {
        if (!file.Contains("shims", StringComparison.OrdinalIgnoreCase))
        {
            return file;
        }

        foreach (var scoopExe in EnumerateScoopCursorExeCandidates())
        {
            if (File.Exists(scoopExe))
            {
                return scoopExe;
            }
        }

        return file;
    }

    private static bool HasCursorUserData(string root)
    {
        if (!Directory.Exists(root))
        {
            return false;
        }

        if (File.Exists(Path.Combine(root, "storage.json")))
        {
            return true;
        }

        return File.Exists(Path.Combine(root, "User", "globalStorage", "state.vscdb"));
    }

    private static string ResolveCursorAppDataRoot(string installDir)
    {
        string portableData = Path.Join(installDir, "data");
        string portableUserData = Path.Join(portableData, "user-data");
        if (Directory.Exists(portableData) && Directory.Exists(portableUserData) && HasCursorUserData(portableUserData))
        {
            return portableUserData;
        }

        string roamingDefault = Path.Combine(UserAppDataPath, "Cursor");
        if (HasCursorUserData(roamingDefault))
        {
            return roamingDefault;
        }

        // Scoop persist：常把 %APPDATA%\Cursor 等价内容放在 persist\cursor
        foreach (var scoopRoot in new[] { Path.Combine(UserProfilePath, "scoop"), Environment.GetEnvironmentVariable("SCOOP") }.Where(s => !string.IsNullOrEmpty(s)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string persist = Path.Combine(scoopRoot!, "persist", "cursor");
            if (HasCursorUserData(persist))
            {
                return persist;
            }

            string persistUserData = Path.Combine(persist, "data", "user-data");
            if (HasCursorUserData(persistUserData))
            {
                return persistUserData;
            }
        }

        string globalPersist = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "scoop", "persist", "cursor");
        if (HasCursorUserData(globalPersist))
        {
            return globalPersist;
        }

        if (Directory.Exists(portableUserData))
        {
            return portableUserData;
        }

        if (Directory.Exists(roamingDefault))
        {
            return roamingDefault;
        }

        return roamingDefault;
    }

    /// <summary>
    /// PowerToys Run 的 Result.Icon 委托在异步加载后于非 UI 线程赋值，易导致图标无法绘制（显示为纯色块）。
    /// 使用绝对路径 Result.IcoPath 走 ImageLoader.LoadAsync 可正常显示。
    /// </summary>
    private static string GetStableShortId(string exePath)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(exePath));
        return Convert.ToHexString(bytes.AsSpan(0, 8));
    }

    private static CursorInstance? TryCreateInstance(string file)
    {
        if (!File.Exists(file))
        {
            return null;
        }

        string iconDir = Path.GetDirectoryName(file.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? string.Empty;
        string appData = ResolveCursorAppDataRoot(iconDir);
        string pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;

        using Icon? cursorIcon = Icon.ExtractAssociatedIcon(file);
        if (cursorIcon is null)
        {
            return null;
        }

        string cacheDir = Path.Combine(pluginDir, "IconCache");
        Directory.CreateDirectory(cacheDir);
        string id = GetStableShortId(file);
        string wsIcoPath = Path.Combine(cacheDir, $"{id}.ws.png");
        string rmIcoPath = Path.Combine(cacheDir, $"{id}.remote.png");

        BitmapImage workspaceBitmap;
        BitmapImage remoteBitmap;
        string workspaceIcoPathForResult;
        string remoteIcoPathForResult;

        try
        {
            string folderPng = Path.Join(pluginDir, "Images", "folder.png");
            string monitorPng = Path.Join(pluginDir, "Images", "monitor.png");

            using var cursorIconBitmap = cursorIcon.ToBitmap();
            if (File.Exists(folderPng) && File.Exists(monitorPng))
            {
                using var folderIcon = (Bitmap)Image.FromFile(folderPng);
                using var bitmapFolderIcon = BitmapOverlayToCenter(folderIcon, cursorIconBitmap);
                using var monitorIcon = (Bitmap)Image.FromFile(monitorPng);
                using var bitmapMonitorIcon = BitmapOverlayToCenter(monitorIcon, cursorIconBitmap);
                bitmapFolderIcon.Save(wsIcoPath, ImageFormat.Png);
                bitmapMonitorIcon.Save(rmIcoPath, ImageFormat.Png);
                workspaceBitmap = BitmapImageFromFile(wsIcoPath);
                remoteBitmap = BitmapImageFromFile(rmIcoPath);
            }
            else
            {
                using (var wsBmp = (Bitmap)cursorIconBitmap.Clone())
                using (var rmBmp = (Bitmap)cursorIconBitmap.Clone())
                {
                    wsBmp.Save(wsIcoPath, ImageFormat.Png);
                    rmBmp.Save(rmIcoPath, ImageFormat.Png);
                }

                workspaceBitmap = BitmapImageFromFile(wsIcoPath);
                remoteBitmap = BitmapImageFromFile(rmIcoPath);
            }

            workspaceIcoPathForResult = wsIcoPath;
            remoteIcoPathForResult = rmIcoPath;
        }
        catch
        {
            string dark = Path.Combine(pluginDir, "Images", "cursor.dark.png");
            string light = Path.Combine(pluginDir, "Images", "cursor.light.png");
            if (!File.Exists(dark))
            {
                return null;
            }

            workspaceBitmap = BitmapImageFromFile(dark);
            remoteBitmap = File.Exists(light) ? BitmapImageFromFile(light) : workspaceBitmap;
            workspaceIcoPathForResult = dark;
            remoteIcoPathForResult = File.Exists(light) ? light : dark;
        }

        return new CursorInstance
        {
            ExecutablePath = file,
            AppData = appData,
            WorkspaceIcoPath = workspaceIcoPathForResult,
            RemoteIcoPath = remoteIcoPathForResult,
            WorkspaceIconBitMap = workspaceBitmap,
            RemoteIconBitMap = remoteBitmap,
        };
    }
}
