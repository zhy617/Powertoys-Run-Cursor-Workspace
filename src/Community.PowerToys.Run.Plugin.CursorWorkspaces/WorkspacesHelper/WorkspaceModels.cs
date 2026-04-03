using Community.PowerToys.Run.Plugin.CursorWorkspaces.CursorHelper;
using PluginStrings = Community.PowerToys.Run.Plugin.CursorWorkspaces.PluginStrings;

namespace Community.PowerToys.Run.Plugin.CursorWorkspaces.WorkspacesHelper;

public sealed class CursorWorkspace
{
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// 与 <see cref="Path"/> 配套的远程 authority（如 <c>ssh-remote+host</c>）。
    /// Cursor 可能复用相同的 folderUri，仅靠此项区分不同 SSH 主机上的同一路径。
    /// </summary>
    public string? RemoteAuthority { get; set; }

    public string RelativePath { get; set; } = string.Empty;

    public string FolderName { get; set; } = string.Empty;

    public string? ExtraInfo { get; set; }

    /// <summary>Cursor 在 <c>history.recentlyOpenedPathsList</c> 等 JSON 中的 <c>label</c>（含远程目录与 <c>[SSH: …]</c>）。</summary>
    public string? CursorLabel { get; set; }

    public WorkspaceEnvironment WorkspaceEnvironment { get; set; }

    public WorkspaceType WorkspaceType { get; set; }

    public CursorInstance CursorInstance { get; set; } = null!;

    /// <summary>来自 <c>~/.ssh/config</c>，尚无 Cursor 工作区缓存时的占位项。</summary>
    public bool IsFromSshConfigOnly { get; set; }

    public string WorkspaceEnvironmentToString()
    {
        return WorkspaceEnvironment switch
        {
            WorkspaceEnvironment.Local => PluginStrings.TypeWorkspaceLocal,
            WorkspaceEnvironment.Codespaces => "Codespaces",
            WorkspaceEnvironment.RemoteSSH => "SSH",
            WorkspaceEnvironment.RemoteWSL => "WSL",
            WorkspaceEnvironment.DevContainer => PluginStrings.TypeWorkspaceDevContainer,
            WorkspaceEnvironment.RemoteTunnel => "Tunnel",
            _ => string.Empty,
        };
    }
}

public enum WorkspaceEnvironment
{
    Local = 1,
    Codespaces = 2,
    RemoteWSL = 3,
    RemoteSSH = 4,
    DevContainer = 5,
    RemoteTunnel = 6,
}

public enum WorkspaceType
{
    ProjectFolder = 1,
    WorkspaceFile = 2,
}
