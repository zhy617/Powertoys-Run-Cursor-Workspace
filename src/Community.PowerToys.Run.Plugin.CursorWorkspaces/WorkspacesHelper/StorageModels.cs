using System.Text.Json.Serialization;

namespace Community.PowerToys.Run.Plugin.CursorWorkspaces.WorkspacesHelper;

public class OpenedPathsList
{
    [JsonPropertyName("workspaces3")]
    public List<string>? Workspaces3 { get; set; }

    [JsonPropertyName("entries")]
    public List<WorkspaceEntry>? Entries { get; set; }
}

public class VSCodeStorageFile
{
    [JsonPropertyName("openedPathsList")]
    public OpenedPathsList? OpenedPathsList { get; set; }

    /// <summary>Cursor/VS Code 在 <c>User/globalStorage/storage.json</c> 中常用；结构与 <see cref="OpenedPathsList.Entries"/> 类似。</summary>
    [JsonPropertyName("backupWorkspaces")]
    public BackupWorkspaces? BackupWorkspaces { get; set; }
}

public class BackupWorkspaces
{
    [JsonPropertyName("folders")]
    public List<WorkspaceEntry>? Folders { get; set; }

    /// <summary>多根 <c>.code-workspace</c> 等；条目格式与 <see cref="WorkspaceEntry"/> 可能不同，按需扩展。</summary>
    [JsonPropertyName("workspaces")]
    public List<WorkspaceEntry>? Workspaces { get; set; }
}

public class StorageEntries
{
    [JsonPropertyName("entries")]
    public List<WorkspaceEntry>? Entries { get; set; }
}

public class WorkspaceEntry
{
    [JsonPropertyName("folderUri")]
    public string? FolderUri { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("remoteAuthority")]
    public string? RemoteAuthority { get; set; }

    [JsonPropertyName("workspace")]
    public WorkspaceProperty? Workspace { get; set; }
}

public class WorkspaceProperty
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("configPath")]
    public string? ConfigPath { get; set; }
}

/// <summary>
/// <c>User/workspaceStorage/&lt;hash&gt;/workspace.json</c>：每个已打开过的窗口一条，含完整
/// <c>vscode-remote://</c> URI（含主机身份），比 <c>history.recentlyOpenedPathsList</c> 更不易被合并丢失。
/// </summary>
public class WorkspaceStorageRoot
{
    [JsonPropertyName("folder")]
    public string? Folder { get; set; }

    /// <summary>与 <see cref="Folder"/> 拆开存储时（如 <c>vscode-remote:///path</c>）需要一并解析。</summary>
    [JsonPropertyName("remoteAuthority")]
    public string? RemoteAuthority { get; set; }

    [JsonPropertyName("workspace")]
    public WorkspaceProperty? Workspace { get; set; }
}
