using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using Community.PowerToys.Run.Plugin.CursorWorkspaces.CursorHelper;
using Community.PowerToys.Run.Plugin.CursorWorkspaces.WorkspacesHelper;
using Wox.Infrastructure;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.CursorWorkspaces;

public class Main : IPlugin, IPluginI18n, IContextMenu
{
    private PluginInitContext? _context;

    public string Name => PluginStrings.PluginTitle;

    public string Description => PluginStrings.PluginDescription;

    public static string PluginID => "A7B9C8D1E2F345678901234567890AB";

    private readonly CursorWorkspacesApi _workspacesApi = new();

    public Main()
    {
        CursorInstances.LoadCursorInstances();
    }

    public List<Result> Query(Query query)
    {
        var results = new List<Result>();

        if (query is null)
        {
            return results;
        }

        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in _workspacesApi.Workspaces)
        {
            if (!seenPaths.Add(a.Path))
            {
                continue;
            }

            var title = a.WorkspaceType == WorkspaceType.ProjectFolder
                ? a.FolderName
                : a.FolderName.Replace(".code-workspace", $" ({PluginStrings.Workspace})", StringComparison.OrdinalIgnoreCase);

            var typeWorkspace = a.WorkspaceEnvironmentToString();
            if (a.WorkspaceEnvironment != WorkspaceEnvironment.Local)
            {
                title = $"{title}{(a.ExtraInfo != null ? $" - {a.ExtraInfo}" : string.Empty)} ({typeWorkspace})";
            }

            string realPath = SystemPath.RealPath(a.RelativePath);
            string kind = a.WorkspaceType == WorkspaceType.WorkspaceFile ? PluginStrings.Workspace : PluginStrings.ProjectFolder;
            string subtitle = a.WorkspaceEnvironment != WorkspaceEnvironment.Local
                ? $"{kind} {PluginStrings.In} {typeWorkspace}: {realPath}"
                : $"{kind}: {realPath}";

            var tooltip = new ToolTipData(title, subtitle);

            results.Add(new Result
            {
                Title = title,
                SubTitle = subtitle,
                Icon = () => a.CursorInstance.WorkspaceIcon,
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

        results = results.Where(r => r.Title.Contains(query.Search, StringComparison.InvariantCultureIgnoreCase)).ToList();

        foreach (var x in results)
        {
            if (x.Score == 0)
            {
                x.Score = 100;
            }

            var intersection = Convert.ToInt32(x.Title.ToLowerInvariant().Intersect(query.Search.ToLowerInvariant()).Count() * query.Search.Length);
            var differenceWithQuery = Convert.ToInt32((x.Title.Length - intersection) * query.Search.Length * 0.7);
            x.Score = x.Score - differenceWithQuery + intersection;
        }

        results = results.OrderByDescending(x => x.Score).ToList();
        if (string.IsNullOrWhiteSpace(query.Search))
        {
            results = results.OrderBy(x => x.Title).ToList();
        }

        return results;
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
