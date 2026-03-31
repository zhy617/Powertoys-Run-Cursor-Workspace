using System.IO;
using System.Text.Json;
using Community.PowerToys.Run.Plugin.CursorWorkspaces.CursorHelper;
using Microsoft.Data.Sqlite;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.CursorWorkspaces.WorkspacesHelper;

public sealed class CursorWorkspacesApi
{
    private CursorWorkspace? ParseUriAndAuthority(string uri, string? authority, CursorInstance cursorInstance, bool isWorkspace = false)
    {
        if (uri is null)
        {
            return null;
        }

        var rfc3986Uri = Rfc3986Uri.Parse(Uri.UnescapeDataString(uri));
        if (rfc3986Uri is null)
        {
            return null;
        }

        var (workspaceEnv, machineName) = ParseAuthority.GetWorkspaceEnvironment(authority ?? rfc3986Uri.Authority);
        if (workspaceEnv is null)
        {
            return null;
        }

        var localPath = rfc3986Uri.Path;

        if (workspaceEnv == WorkspaceEnvironment.Local)
        {
            var auth = authority ?? rfc3986Uri.Authority;
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
            return null;
        }

        var folderName = System.IO.Path.GetFileName(localPath);

        if (string.IsNullOrEmpty(folderName))
        {
            var dirInfo = new DirectoryInfo(localPath);
            folderName = dirInfo.Name.TrimEnd(':');
        }

        return new CursorWorkspace
        {
            Path = uri,
            WorkspaceType = isWorkspace ? WorkspaceType.WorkspaceFile : WorkspaceType.ProjectFolder,
            RelativePath = localPath,
            FolderName = folderName,
            ExtraInfo = machineName,
            WorkspaceEnvironment = workspaceEnv.Value,
            CursorInstance = cursorInstance,
        };
    }

    private static bool DoesPathExist(string path, WorkspaceEnvironment workspaceEnv)
    {
        if (workspaceEnv == WorkspaceEnvironment.Local)
        {
            return Directory.Exists(path) || File.Exists(path);
        }

        return true;
    }

    public List<CursorWorkspace> Workspaces
    {
        get
        {
            var results = new List<CursorWorkspace>();

            foreach (var cursorInstance in CursorInstances.Instances)
            {
                var storageJson = Path.Combine(cursorInstance.AppData, "storage.json");
                var stateDb = Path.Combine(cursorInstance.AppData, "User", "globalStorage", "state.vscdb");

                if (File.Exists(storageJson))
                {
                    results.AddRange(GetWorkspacesInJson(cursorInstance, storageJson));
                }

                if (File.Exists(stateDb))
                {
                    results.AddRange(GetWorkspacesInVscdb(cursorInstance, stateDb));
                }
            }

            return results;
        }
    }

    private List<CursorWorkspace> GetWorkspacesInJson(CursorInstance cursorInstance, string filePath)
    {
        var storageFileResults = new List<CursorWorkspace>();

        string fileContent = File.ReadAllText(filePath);

        try
        {
            var storageFile = JsonSerializer.Deserialize<VSCodeStorageFile>(fileContent);

            if (storageFile?.OpenedPathsList is null)
            {
                return storageFileResults;
            }

            if (storageFile.OpenedPathsList.Workspaces3 is not null)
            {
                foreach (var workspaceUri in storageFile.OpenedPathsList.Workspaces3)
                {
                    var workspace = ParseUriAndAuthority(workspaceUri, null, cursorInstance);
                    if (workspace != null)
                    {
                        storageFileResults.Add(workspace);
                    }
                }
            }

            if (storageFile.OpenedPathsList.Entries is not null)
            {
                foreach (var entry in storageFile.OpenedPathsList.Entries)
                {
                    bool isWorkspaceFile = false;
                    var uri = entry.FolderUri;
                    if (entry.Workspace?.ConfigPath is not null)
                    {
                        isWorkspaceFile = true;
                        uri = entry.Workspace.ConfigPath;
                    }

                    if (uri is null)
                    {
                        continue;
                    }

                    var workspace = ParseUriAndAuthority(uri, entry.RemoteAuthority, cursorInstance, isWorkspaceFile);
                    if (workspace != null)
                    {
                        storageFileResults.Add(workspace);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Exception($"Failed to deserialize {filePath}", ex, GetType());
        }

        return storageFileResults;
    }

    private List<CursorWorkspace> GetWorkspacesInVscdb(CursorInstance cursorInstance, string filePath)
    {
        var dbFileResults = new List<CursorWorkspace>();
        SqliteConnection? sqliteConnection = null;

        try
        {
            sqliteConnection = new SqliteConnection($"Data Source={filePath};Mode=ReadOnly;");
            sqliteConnection.Open();

            if (sqliteConnection.State != System.Data.ConnectionState.Open)
            {
                return dbFileResults;
            }

            using var cmd = sqliteConnection.CreateCommand();
            cmd.CommandText = "SELECT value FROM ItemTable WHERE key LIKE 'history.recentlyOpenedPathsList'";

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                return dbFileResults;
            }

            string entries = reader.GetString(0);
            if (string.IsNullOrEmpty(entries))
            {
                return dbFileResults;
            }

            var vscodeStorageEntries = JsonSerializer.Deserialize<StorageEntries>(entries);
            if (vscodeStorageEntries?.Entries is null)
            {
                return dbFileResults;
            }

            foreach (var entry in vscodeStorageEntries.Entries.Where(x => x != null))
            {
                bool isWorkspaceFile = false;
                var uri = entry.FolderUri;
                if (entry.Workspace?.ConfigPath is not null)
                {
                    isWorkspaceFile = true;
                    uri = entry.Workspace.ConfigPath;
                }

                if (uri is null)
                {
                    continue;
                }

                var workspace = ParseUriAndAuthority(uri, entry.RemoteAuthority, cursorInstance, isWorkspaceFile);
                if (workspace != null)
                {
                    dbFileResults.Add(workspace);
                }
            }
        }
        catch (Exception e)
        {
            Log.Exception($"Failed to retrieve workspaces from db: {filePath}", e, GetType());
        }
        finally
        {
            sqliteConnection?.Close();
        }

        return dbFileResults;
    }
}
