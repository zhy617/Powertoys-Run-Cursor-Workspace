using System.Text.RegularExpressions;

namespace Community.PowerToys.Run.Plugin.CursorWorkspaces.WorkspacesHelper;

public static class ParseAuthority
{
    /// <summary>Windows 上 <c>file://C:/path</c>（两斜杠）会把盘符解析到 authority，需视为本地。</summary>
    private static readonly Regex WindowsDriveAuthority = new(@"^[a-zA-Z]:$", RegexOptions.Compiled);

    private static readonly Dictionary<string, WorkspaceEnvironment> EnvironmentTypes = new()
    {
        { string.Empty, WorkspaceEnvironment.Local },
        { "ssh-remote", WorkspaceEnvironment.RemoteSSH },
        { "wsl", WorkspaceEnvironment.RemoteWSL },
        { "vsonline", WorkspaceEnvironment.Codespaces },
        { "dev-container", WorkspaceEnvironment.DevContainer },
        { "tunnel", WorkspaceEnvironment.RemoteTunnel },
    };

    private static string GetRemoteName(string authority)
    {
        int pos = authority.IndexOf('+');
        if (pos < 0)
        {
            return authority;
        }

        return authority[..pos];
    }

    public static (WorkspaceEnvironment? WorkspaceEnvironment, string? MachineName) GetWorkspaceEnvironment(string? authority)
    {
        string remoteName = GetRemoteName(authority ?? string.Empty);

        string? machineName = authority is not null && remoteName.Length < authority.Length
            ? authority[(remoteName.Length + 1)..]
            : null;

        if (WindowsDriveAuthority.IsMatch(remoteName))
        {
            return (WorkspaceEnvironment.Local, machineName);
        }

        return EnvironmentTypes.TryGetValue(remoteName, out WorkspaceEnvironment workspace)
            ? (workspace, machineName)
            : (null, null);
    }

    public static bool IsWindowsFileDriveAuthority(string? authority) =>
        authority is not null && WindowsDriveAuthority.IsMatch(authority);
}
