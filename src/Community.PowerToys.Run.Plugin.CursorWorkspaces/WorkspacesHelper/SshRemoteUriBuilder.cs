using System.Text;
using System.Text.Json;

namespace Community.PowerToys.Run.Plugin.CursorWorkspaces.WorkspacesHelper;

/// <summary>与 Cursor 存储格式一致的 <c>vscode-remote://ssh-remote%2B&lt;hex&gt;/path</c> 构造。</summary>
public static class SshRemoteUriBuilder
{
    public static string BuildFolderUri(string hostNameAlias, string remotePath = "/")
    {
        if (string.IsNullOrEmpty(remotePath))
        {
            remotePath = "/";
        }

        if (!remotePath.StartsWith('/'))
        {
            remotePath = "/" + remotePath;
        }

        string json = JsonSerializer.Serialize(new { hostName = hostNameAlias });
        string hex = Convert.ToHexString(Encoding.UTF8.GetBytes(json)).ToLowerInvariant();
        return $"vscode-remote://ssh-remote%2B{hex}{remotePath}";
    }
}
