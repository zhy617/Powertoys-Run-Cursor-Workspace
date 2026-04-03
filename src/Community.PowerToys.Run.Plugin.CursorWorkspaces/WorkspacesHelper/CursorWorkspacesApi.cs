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

        if (!WorkspaceUriCore.TryCreateWorkspace(uri, authority, isWorkspace, out var parts, out _))
        {
            return null;
        }

        return new CursorWorkspace
        {
            Path = uri,
            RemoteAuthority = parts!.RemoteAuthority,
            WorkspaceType = parts.WorkspaceType,
            RelativePath = parts.RelativePath,
            FolderName = parts.FolderName,
            ExtraInfo = parts.ExtraInfo,
            WorkspaceEnvironment = parts.WorkspaceEnvironment,
            CursorInstance = cursorInstance,
        };
    }

    public List<CursorWorkspace> Workspaces
    {
        get
        {
            var results = new List<CursorWorkspace>();

            foreach (var cursorInstance in CursorInstances.Instances)
            {
                var batch = new List<CursorWorkspace>();

                var stateDb = Path.Combine(cursorInstance.AppData, "User", "globalStorage", "state.vscdb");

                foreach (var storageJson in GetCursorStorageJsonFilePaths(cursorInstance.AppData))
                {
                    batch.AddRange(GetWorkspacesInJson(cursorInstance, storageJson));
                }

                if (File.Exists(stateDb))
                {
                    batch.AddRange(GetWorkspacesInVscdb(cursorInstance, stateDb));
                }

                batch.AddRange(GetWorkspacesFromWorkspaceStorage(cursorInstance));
                results.AddRange(batch);
            }

            // 必须在「所有」实例的 workspaceStorage / 历史都合并后再判断：否则多 Cursor.exe（Scoop/商店等）
            // 时，先处理的实例可能没有该主机缓存，会误加 ssh config 占位项并标成「尚未打开」。
            var globalKnownSshHosts = CollectKnownSshHostAliases(results);
            foreach (var cursorInstance in CursorInstances.Instances)
            {
                results.AddRange(GetWorkspacesFromOpenSshConfig(cursorInstance, globalKnownSshHosts));
            }

            return results;
        }
    }

    private static HashSet<string> CollectKnownSshHostAliases(IEnumerable<CursorWorkspace> workspaces)
    {
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var w in workspaces)
        {
            if (w.WorkspaceEnvironment != WorkspaceEnvironment.RemoteSSH)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(w.ExtraInfo))
            {
                known.Add(w.ExtraInfo);
            }

            string? fromUri = ParseAuthority.TryGetHostNameFromVscodeRemoteFolderUri(w.Path);
            if (!string.IsNullOrEmpty(fromUri))
            {
                known.Add(fromUri);
            }
        }

        return known;
    }

    /// <summary>
    /// 与 VS Code / Cursor 一致：主文件在 <c>User/globalStorage/storage.json</c>；部分便携/旧版在数据根目录的 <c>storage.json</c>。
    /// Scoop 版数据目录仍由 <see cref="CursorInstances.ResolveCursorAppDataRoot"/> 解析（Roaming 或 persist），与安装方式无关。
    /// </summary>
    private static IEnumerable<string> GetCursorStorageJsonFilePaths(string appDataRoot)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in new[]
        {
            Path.Combine(appDataRoot, "User", "globalStorage", "storage.json"),
            Path.Combine(appDataRoot, "storage.json"),
        })
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            string full = Path.GetFullPath(candidate);
            if (seen.Add(full))
            {
                yield return candidate;
            }
        }
    }

    private List<CursorWorkspace> GetWorkspacesInJson(CursorInstance cursorInstance, string filePath)
    {
        var storageFileResults = new List<CursorWorkspace>();

        string fileContent = File.ReadAllText(filePath);

        try
        {
            var storageFile = JsonSerializer.Deserialize<VSCodeStorageFile>(fileContent);
            if (storageFile is null)
            {
                return storageFileResults;
            }

            if (storageFile.OpenedPathsList is not null)
            {
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

                AppendWorkspacesFromWorkspaceEntries(storageFileResults, storageFile.OpenedPathsList.Entries, cursorInstance);
            }

            if (storageFile.BackupWorkspaces is not null)
            {
                AppendWorkspacesFromWorkspaceEntries(storageFileResults, storageFile.BackupWorkspaces.Folders, cursorInstance);
                AppendWorkspacesFromWorkspaceEntries(storageFileResults, storageFile.BackupWorkspaces.Workspaces, cursorInstance);
            }
        }
        catch (Exception ex)
        {
            Log.Exception($"Failed to deserialize {filePath}", ex, GetType());
        }

        return storageFileResults;
    }

    private void AppendWorkspacesFromWorkspaceEntries(
        List<CursorWorkspace> storageFileResults,
        IEnumerable<WorkspaceEntry>? entries,
        CursorInstance cursorInstance)
    {
        if (entries is null)
        {
            return;
        }

        foreach (var entry in entries.Where(x => x != null))
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
                if (!string.IsNullOrWhiteSpace(entry.Label))
                {
                    workspace.CursorLabel = entry.Label.Trim();
                }

                storageFileResults.Add(workspace);
            }
        }
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
                    if (!string.IsNullOrWhiteSpace(entry.Label))
                    {
                        workspace.CursorLabel = entry.Label.Trim();
                    }

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

    /// <summary>
    /// 扫描 <c>User/workspaceStorage/*/workspace.json</c>。此处保留「每个远程连接」的完整 folder URI，
    /// 而 <see cref="GetWorkspacesInVscdb"/> 中的最近打开列表常按路径去重，同路径多 SSH 会只剩一条。
    /// </summary>
    private List<CursorWorkspace> GetWorkspacesFromWorkspaceStorage(CursorInstance cursorInstance)
    {
        var list = new List<CursorWorkspace>();
        var baseDir = Path.Combine(cursorInstance.AppData, "User", "workspaceStorage");
        if (!Directory.Exists(baseDir))
        {
            return list;
        }

        foreach (var dir in Directory.EnumerateDirectories(baseDir))
        {
            var wsJson = Path.Combine(dir, "workspace.json");
            if (!File.Exists(wsJson))
            {
                continue;
            }

            try
            {
                string json = File.ReadAllText(wsJson);
                var model = JsonSerializer.Deserialize<WorkspaceStorageRoot>(json);
                if (model is null)
                {
                    continue;
                }

                bool isWorkspaceFile = false;
                string? uri = model.Folder;
                if (model.Workspace?.ConfigPath is not null)
                {
                    isWorkspaceFile = true;
                    uri = model.Workspace.ConfigPath;
                }

                if (uri is null)
                {
                    continue;
                }

                var workspace = ParseUriAndAuthority(uri, model.RemoteAuthority, cursorInstance, isWorkspaceFile);
                if (workspace != null)
                {
                    list.Add(workspace);
                }
            }
            catch (Exception ex)
            {
                Log.Exception($"Failed to read workspace storage {wsJson}", ex, GetType());
            }
        }

        return list;
    }

    /// <summary>
    /// <c>~/.ssh/config</c> 里配置的 Host 在「尚未用 Cursor 打开过远程文件夹」时不会出现在 workspaceStorage / 历史中；
    /// 为每个尚未出现的别名补一条可启动的 <c>vscode-remote://</c> 项。
    /// </summary>
    /// <param name="globalKnownSshHosts">
    /// 已在任意 Cursor 数据目录中出现过的 SSH 主机标识（可变异；占位项加入后也会写入，避免多实例重复占位）。
    /// </param>
    private List<CursorWorkspace> GetWorkspacesFromOpenSshConfig(CursorInstance cursorInstance, HashSet<string> globalKnownSshHosts)
    {
        var list = new List<CursorWorkspace>();

        IReadOnlyList<string> aliases;
        try
        {
            aliases = SshConfigReader.EnumerateHostAliasesFromDefaultConfig();
        }
        catch
        {
            return list;
        }

        foreach (var alias in aliases)
        {
            if (globalKnownSshHosts.Contains(alias))
            {
                continue;
            }

            string uri = SshRemoteUriBuilder.BuildFolderUri(alias, "/");
            var workspace = ParseUriAndAuthority(uri, null, cursorInstance, false);
            if (workspace == null)
            {
                continue;
            }

            workspace.IsFromSshConfigOnly = true;
            list.Add(workspace);
            globalKnownSshHosts.Add(alias);
        }

        return list;
    }
}
