using Community.PowerToys.Run.Plugin.CursorWorkspaces.CursorHelper;
using PluginStrings = Community.PowerToys.Run.Plugin.CursorWorkspaces.PluginStrings;

namespace Community.PowerToys.Run.Plugin.CursorWorkspaces.WorkspacesHelper;

public sealed class CursorWorkspace
{
    public string Path { get; set; } = string.Empty;

    public string RelativePath { get; set; } = string.Empty;

    public string FolderName { get; set; } = string.Empty;

    public string? ExtraInfo { get; set; }

    public WorkspaceEnvironment WorkspaceEnvironment { get; set; }

    public WorkspaceType WorkspaceType { get; set; }

    public CursorInstance CursorInstance { get; set; } = null!;

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
