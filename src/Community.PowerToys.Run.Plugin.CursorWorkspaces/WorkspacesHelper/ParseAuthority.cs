using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Community.PowerToys.Run.Plugin.CursorWorkspaces.WorkspacesHelper;

public static class ParseAuthority
{
    /// <summary>Windows 上 <c>file://C:/path</c>（两斜杠）会把盘符解析到 authority，需视为本地。</summary>
    private static readonly Regex WindowsDriveAuthority = new(@"^[a-zA-Z]:$", RegexOptions.Compiled);

    /// <summary>Cursor 可能使用不同大小写；必须用不区分大小写查找，否则整条工作区会被丢弃。</summary>
    private static readonly Dictionary<string, WorkspaceEnvironment> EnvironmentTypes = new(StringComparer.OrdinalIgnoreCase)
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

    /// <summary><see cref="Uri.UnescapeDataString(string)"/> 在百分号序列非法时会抛异常，解析工作区 URI 时应使用此方法。</summary>
    public static string SafeUnescapeDataString(string uri)
    {
        try
        {
            return Uri.UnescapeDataString(uri);
        }
        catch (UriFormatException)
        {
            return uri;
        }
    }

    /// <summary>从 <c>vscode-remote://</c> 文件夹 URI 解析出 SSH 侧 host 标识（含解码十六进制 JSON）。</summary>
    public static string? TryGetHostNameFromVscodeRemoteFolderUri(string? uri)
    {
        if (string.IsNullOrEmpty(uri))
        {
            return null;
        }

        var rfc3986Uri = Rfc3986Uri.Parse(SafeUnescapeDataString(uri));
        if (rfc3986Uri is null)
        {
            return null;
        }

        var (workspaceEnv, machineName) = GetWorkspaceEnvironment(rfc3986Uri.Authority);
        if (workspaceEnv != WorkspaceEnvironment.RemoteSSH || machineName is null)
        {
            return null;
        }

        return TryDecodeSshRemoteHostLabel(machineName) ?? machineName;
    }

    /// <summary>
    /// Cursor/VS Code 新版 SSH 在 authority 后缀里用十六进制 UTF-8 保存 <c>{"hostName":"..."}</c>，解析后用于列表展示。
    /// </summary>
    public static string? TryDecodeSshRemoteHostLabel(string authoritySuffix)
    {
        if (string.IsNullOrEmpty(authoritySuffix) || (authoritySuffix.Length % 2) != 0)
        {
            return null;
        }

        foreach (var c in authoritySuffix)
        {
            if (!Uri.IsHexDigit(c))
            {
                return null;
            }
        }

        try
        {
            byte[] bytes = Convert.FromHexString(authoritySuffix);
            string json = Encoding.UTF8.GetString(bytes);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("hostName", out var el))
            {
                return el.GetString();
            }
        }
        catch
        {
            // 非十六进制 JSON 时保持调用方使用原始后缀（如传统 ssh-remote+hostname）。
        }

        return null;
    }
}
