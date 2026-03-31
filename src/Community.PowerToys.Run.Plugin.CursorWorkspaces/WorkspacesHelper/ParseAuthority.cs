namespace Community.PowerToys.Run.Plugin.CursorWorkspaces.WorkspacesHelper;

public static class ParseAuthority
{
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
        return EnvironmentTypes.TryGetValue(remoteName, out WorkspaceEnvironment workspace)
            ? (workspace, machineName)
            : (null, null);
    }
}
