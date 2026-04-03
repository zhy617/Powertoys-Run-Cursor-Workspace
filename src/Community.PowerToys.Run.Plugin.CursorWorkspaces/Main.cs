using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using Community.PowerToys.Run.Plugin.CursorWorkspaces.CursorHelper;
using Community.PowerToys.Run.Plugin.CursorWorkspaces.WorkspacesHelper;
using Wox.Infrastructure;
using Wox.Plugin;
using Wox.Plugin.Logger;

using System.IO;
using System.Text;

namespace Community.PowerToys.Run.Plugin.CursorWorkspaces;

public class Main : IPlugin, IPluginI18n, IContextMenu
{
    private PluginInitContext? _context;

    public string Name => PluginStrings.PluginTitle;

    public string Description => PluginStrings.PluginDescription;

    public static string PluginID => "A7B9C8D1E2F345678901234567890AB";

    private readonly CursorWorkspacesApi _workspacesApi = new();

    private static readonly object _loadLock = new();
    private static bool _instancesLoaded;

    /// <summary>
    /// PowerToys 可能在非 STA 线程上构造插件；构造函数内创建 WPF <see cref="System.Windows.Media.Imaging.BitmapImage"/> 会抛错并导致「插件初始化错误」。
    /// 改为在首次 <see cref="Query"/>（通常在 STA）时再加载 Cursor 实例与图标。
    /// </summary>
    private void EnsureCursorInstancesLoaded()
    {
        lock (_loadLock)
        {
            if (_instancesLoaded)
            {
                return;
            }

            try
            {
                CursorInstances.LoadCursorInstances();
            }
            catch (Exception ex)
            {
                Log.Exception("Cursor 工作区：加载 Cursor 实例失败。", ex, typeof(Main));
            }
            finally
            {
                _instancesLoaded = true;
            }
        }
    }

    public List<Result> Query(Query query)
    {
        EnsureCursorInstancesLoaded();
        
        // 调试用的，静默失败即可
        // try
        // {
        //     var logPath = Path.Combine(Path.GetTempPath(), "CursorWorkspacesPlugin-debug.txt");
        //     var sb = new StringBuilder();
        //     sb.AppendLine("=== " + DateTime.Now.ToString("o") + " ===");
        //     sb.AppendLine("InstanceCount=" + CursorInstances.Instances.Count);
        //     foreach (var inst in CursorInstances.Instances)
        //     {
        //         sb.AppendLine("AppData=" + inst.AppData);
        //         sb.AppendLine("Exe=" + inst.ExecutablePath);
        //     }

        //     File.AppendAllText(logPath, sb.ToString() + Environment.NewLine);
        // }
        // catch
        // {
        //     // 调试用的，静默失败即可
        // }

        var results = new List<Result>();

        if (query is null)
        {
            return results;
        }

        var search = query.Search?.Trim() ?? string.Empty;

        // folderUri 在远程场景下可能与 remoteAuthority 拆开存储；仅按 Path 去重会合并不同 SSH 主机上的同一路径。
        var seenWorkspaceKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in _workspacesApi.Workspaces)
        {
            var dedupKey = $"{a.Path}\u0001{a.RemoteAuthority ?? string.Empty}";
            if (!seenWorkspaceKeys.Add(dedupKey))
            {
                continue;
            }

            var typeWorkspace = a.WorkspaceEnvironmentToString();
            var title = BuildWorkspaceResultTitle(a, typeWorkspace);

            string realPath = SystemPath.RealPath(a.RelativePath);
            string kind = a.WorkspaceType == WorkspaceType.WorkspaceFile ? PluginStrings.Workspace : PluginStrings.ProjectFolder;
            string subtitle = a.WorkspaceEnvironment != WorkspaceEnvironment.Local
                ? $"{kind} {PluginStrings.In} {typeWorkspace}: {realPath}"
                : $"{kind}: {realPath}";
            if (a.IsFromSshConfigOnly)
            {
                subtitle = $"{subtitle} · {PluginStrings.SshConfigHostOnly}";
            }

            var tooltip = new ToolTipData(title, subtitle);

            results.Add(new Result
            {
                Title = title,
                SubTitle = subtitle,
                IcoPath = a.WorkspaceEnvironment != WorkspaceEnvironment.Local
                    ? a.CursorInstance.RemoteIcoPath
                    : a.CursorInstance.WorkspaceIcoPath,
                ToolTipData = tooltip,
                Action = _ =>
                {
                    try
                    {
                        var process = new ProcessStartInfo
                        {
                            FileName = a.CursorInstance.ExecutablePath,
                            UseShellExecute = true,
                            Arguments = a.WorkspaceType == WorkspaceType.ProjectFolder
                                ? $"--folder-uri {a.Path}"
                                : $"--file-uri {a.Path}",
                            WindowStyle = ProcessWindowStyle.Hidden,
                        };
                        Process.Start(process);
                        return true;
                    }
                    catch (Win32Exception ex)
                    {
                        HandleError("无法打开该工作区。", ex, showMsg: true);
                        return false;
                    }
                },
                ContextData = a,
            });
        }

        results = results.Where(r => MatchesWorkspaceSearch(r.Title, r.SubTitle, search)).ToList();

        foreach (var x in results)
        {
            if (x.Score == 0)
            {
                x.Score = 100;
            }

            var haystack = (x.Title + " " + x.SubTitle).ToLowerInvariant();
            var intersection = Convert.ToInt32(haystack.Intersect(search.ToLowerInvariant()).Count() * search.Length);
            var differenceWithQuery = Convert.ToInt32((haystack.Length - intersection) * search.Length * 0.7);
            x.Score = x.Score - differenceWithQuery + intersection;
        }

        if (string.IsNullOrEmpty(search))
        {
            results = results.OrderBy(x => x.Title).ToList();
        }
        else
        {
            // 路径较长的工作区（如 .../deepseek-vl2-bridge）在字符交集打分下会输给短标题；优先「标题含完整搜索词」。
            results = results
                .OrderByDescending(r => r.Title.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(x => x.Score)
                .ToList();
        }

        return results;
    }

    private static string BuildWorkspaceResultTitle(CursorWorkspace a, string typeWorkspace)
    {
        if (a.WorkspaceType == WorkspaceType.WorkspaceFile)
        {
            string baseName = a.FolderName.Replace(".code-workspace", $" ({PluginStrings.Workspace})", StringComparison.OrdinalIgnoreCase);
            if (a.WorkspaceEnvironment == WorkspaceEnvironment.Local)
            {
                return baseName;
            }

            if (a.WorkspaceEnvironment == WorkspaceEnvironment.RemoteSSH)
            {
                return BuildSshRemoteTitle(a, baseName);
            }

            return $"{baseName}{(a.ExtraInfo != null ? $" - {a.ExtraInfo}" : string.Empty)} ({typeWorkspace})";
        }

        if (a.WorkspaceEnvironment == WorkspaceEnvironment.Local)
        {
            return a.FolderName;
        }

        if (a.WorkspaceEnvironment == WorkspaceEnvironment.RemoteSSH)
        {
            return BuildSshRemoteTitle(a, a.FolderName);
        }

        return $"{a.FolderName}{(a.ExtraInfo != null ? $" - {a.ExtraInfo}" : string.Empty)} ({typeWorkspace})";
    }

    /// <summary>与 Cursor 最近打开一致：优先其 <c>label</c>；否则为「远程目录 [SSH: 主机]」。</summary>
    private static string BuildSshRemoteTitle(CursorWorkspace a, string folderNameFallbackWhenNoPath)
    {
        if (!string.IsNullOrWhiteSpace(a.CursorLabel))
        {
            return a.CursorLabel.Trim();
        }

        string pathPart = FormatSshRemotePathForDisplay(a.RelativePath);
        if (string.IsNullOrEmpty(pathPart))
        {
            pathPart = folderNameFallbackWhenNoPath;
        }

        string host = string.IsNullOrEmpty(a.ExtraInfo) ? "SSH" : a.ExtraInfo;
        return $"{pathPart} [SSH: {host}]";
    }

    /// <summary>将 URI 中的 Unix 路径展示为带 <c>~</c> 的家目录简写（例如 <c>/root/foo</c> → <c>~/foo</c>）。</summary>
    private static string FormatSshRemotePathForDisplay(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath) || relativePath == "/")
        {
            return "~";
        }

        string p = relativePath.TrimEnd('/');
        if (p.StartsWith("/root/", StringComparison.Ordinal) && p.Length > 6)
        {
            return "~/" + p[6..].TrimStart('/');
        }

        return p;
    }

    /// <summary>
    /// SSH 标题形如 <c>~/DeepSeek-VL2 [SSH: DeepSeekVL2-80G]</c>；整段 <c>Contains</c> 可匹配 48G/80G 等后缀。
    /// 否则要求搜索中的字母数字片段均在标题或副标题中出现。
    /// </summary>
    private static bool MatchesWorkspaceSearch(string title, string? subtitle, string search)
    {
        if (string.IsNullOrEmpty(search))
        {
            return true;
        }

        var hay = $"{title} {subtitle ?? string.Empty}";
        if (hay.Contains(search, StringComparison.InvariantCultureIgnoreCase))
        {
            return true;
        }

        var tokens = Regex.Matches(search, @"[A-Za-z0-9][A-Za-z0-9\-]*")
            .Select(m => m.Value)
            .Where(t => t.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (tokens.Count == 0)
        {
            return hay.Contains(search, StringComparison.InvariantCultureIgnoreCase);
        }

        return tokens.All(t => hay.Contains(t, StringComparison.InvariantCultureIgnoreCase));
    }

    public void Init(PluginInitContext context)
    {
        _context = context;
    }

    public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
    {
        if (selectedResult?.ContextData is not CursorWorkspace workspace)
        {
            return new List<ContextMenuResult>();
        }

        string realPath = SystemPath.RealPath(workspace.RelativePath);

        return new List<ContextMenuResult>
        {
            new()
            {
                PluginName = Name,
                Title = $"{PluginStrings.CopyPath} (Ctrl+C)",
                Glyph = "\xE8C8",
                FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                AcceleratorKey = Key.C,
                AcceleratorModifiers = ModifierKeys.Control,
                Action = _ => CopyToClipboard(realPath),
            },
            new()
            {
                PluginName = Name,
                Title = $"{PluginStrings.OpenInExplorer} (Ctrl+Shift+F)",
                Glyph = "\xEC50",
                FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                AcceleratorKey = Key.F,
                AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                Action = _ => OpenInExplorer(realPath),
            },
            new()
            {
                PluginName = Name,
                Title = $"{PluginStrings.OpenInConsole} (Ctrl+Shift+C)",
                Glyph = "\xE756",
                FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                AcceleratorKey = Key.C,
                AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                Action = _ => OpenInConsole(realPath),
            },
        };
    }

    private bool CopyToClipboard(string path)
    {
        try
        {
            Clipboard.SetText(path);
            return true;
        }
        catch (Exception ex)
        {
            HandleError("无法复制到剪贴板。", ex, showMsg: true);
            return false;
        }
    }

    private bool OpenInConsole(string path)
    {
        try
        {
            Helper.OpenInConsole(path);
            return true;
        }
        catch (Exception ex)
        {
            HandleError($"无法在终端中打开路径: {path}", ex, showMsg: true);
            return false;
        }
    }

    private bool OpenInExplorer(string path)
    {
        if (!Helper.OpenInShell("explorer.exe", $"\"{path}\""))
        {
            HandleError($"无法在资源管理器中打开: {path}", showMsg: true);
            return false;
        }

        return true;
    }

    private void HandleError(string msg, Exception? exception = null, bool showMsg = false)
    {
        if (exception != null)
        {
            Log.Exception(msg, exception, exception.GetType());
        }
        else
        {
            Log.Error(msg, typeof(Main));
        }

        if (showMsg && _context is not null)
        {
            _context.API.ShowMsg($"插件: {_context.CurrentPluginMetadata.Name}", msg);
        }
    }

    public string GetTranslatedPluginTitle() => PluginStrings.PluginTitle;

    public string GetTranslatedPluginDescription() => PluginStrings.PluginDescription;
}
