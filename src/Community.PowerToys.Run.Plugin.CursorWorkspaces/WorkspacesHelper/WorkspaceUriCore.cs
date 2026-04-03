using System.IO;

namespace Community.PowerToys.Run.Plugin.CursorWorkspaces.WorkspacesHelper;

/// <summary>从 <c>vscode-remote://</c> / <c>file://</c> URI 解析工作区字段（与 <see cref="CursorWorkspacesApi"/> 共用，便于单测与排查「被丢弃」原因）。</summary>
internal static class WorkspaceUriCore
{
    internal sealed record WorkspaceUriParts(
        string? RemoteAuthority,
        string RelativePath,
        string FolderName,
        string? ExtraInfo,
        WorkspaceEnvironment WorkspaceEnvironment,
        WorkspaceType WorkspaceType);

    /// <summary>
    /// <paramref name="originalUri"/> 须为存储中的原始字符串（用作启动参数）；内部会先安全解码再解析。
    /// </summary>
    internal static bool TryCreateWorkspace(
        string originalUri,
        string? authorityOverride,
        bool isWorkspaceFile,
        out WorkspaceUriParts? parts,
        out string? failureReason)
    {
        parts = null;
        failureReason = null;

        if (string.IsNullOrEmpty(originalUri))
        {
            failureReason = "empty uri";
            return false;
        }

        string unescaped = ParseAuthority.SafeUnescapeDataString(originalUri);
        var rfc3986Uri = Rfc3986Uri.Parse(unescaped);
        if (rfc3986Uri is null)
        {
            failureReason = "Rfc3986Uri.Parse returned null (regex and vscode-remote fallback both failed)";
            return false;
        }

        var (workspaceEnv, machineName) = ParseAuthority.GetWorkspaceEnvironment(authorityOverride ?? rfc3986Uri.Authority);
        if (workspaceEnv is null)
        {
            failureReason =
                $"GetWorkspaceEnvironment returned null (authority from uri='{rfc3986Uri.Authority}', override='{authorityOverride ?? "(null)"}')";
            return false;
        }

        if (workspaceEnv == WorkspaceEnvironment.RemoteSSH && machineName is not null)
        {
            machineName = ParseAuthority.TryDecodeSshRemoteHostLabel(machineName) ?? machineName;
        }

        var localPath = rfc3986Uri.Path;

        if (workspaceEnv == WorkspaceEnvironment.Local)
        {
            var auth = authorityOverride ?? rfc3986Uri.Authority;
            if (ParseAuthority.IsWindowsFileDriveAuthority(auth))
            {
                localPath = auth + localPath;
            }
            else
            {
                localPath = localPath[1..];
            }
        }

        if (!DoesPathExist(localPath, workspaceEnv.Value))
        {
            failureReason = $"DoesPathExist false for local path: {localPath}";
            return false;
        }

        string folderName;
        if (workspaceEnv != WorkspaceEnvironment.Local &&
            (string.IsNullOrEmpty(localPath) || localPath == "/"))
        {
            folderName = machineName ?? "SSH";
        }
        else
        {
            folderName = GetFolderNameFromPath(localPath, workspaceEnv.Value);
            if (string.IsNullOrEmpty(folderName))
            {
                if (workspaceEnv == WorkspaceEnvironment.Local)
                {
                    var dirInfo = new DirectoryInfo(localPath);
                    folderName = dirInfo.Name.TrimEnd(':');
                }
                else
                {
                    folderName = machineName ?? "SSH";
                }
            }
        }

        var effectiveAuthority = string.IsNullOrEmpty(authorityOverride) ? rfc3986Uri.Authority : authorityOverride;
        if (string.IsNullOrEmpty(effectiveAuthority))
        {
            effectiveAuthority = null;
        }

        parts = new WorkspaceUriParts(
            effectiveAuthority,
            localPath,
            folderName,
            machineName,
            workspaceEnv.Value,
            isWorkspaceFile ? WorkspaceType.WorkspaceFile : WorkspaceType.ProjectFolder);

        return true;
    }

    private static bool DoesPathExist(string path, WorkspaceEnvironment workspaceEnv)
    {
        if (workspaceEnv == WorkspaceEnvironment.Local)
        {
            return Directory.Exists(path) || File.Exists(path);
        }

        return true;
    }

    private static string GetFolderNameFromPath(string path, WorkspaceEnvironment workspaceEnv)
    {
        if (workspaceEnv == WorkspaceEnvironment.Local)
        {
            return Path.GetFileName(path.TrimEnd('/'));
        }

        string t = path.TrimEnd('/');
        if (string.IsNullOrEmpty(t) || t == "/")
        {
            return string.Empty;
        }

        int i = t.LastIndexOf('/');
        return i < 0 ? t : t[(i + 1)..];
    }
}
