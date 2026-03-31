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
