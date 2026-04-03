using System.IO;
using System.Text;

namespace Community.PowerToys.Run.Plugin.CursorWorkspaces.WorkspacesHelper;

/// <summary>读取 OpenSSH <c>~/.ssh/config</c>（含 <c>Include</c>）中的 <c>Host</c> 别名。</summary>
public static class SshConfigReader
{
    public static IReadOnlyList<string> EnumerateHostAliasesFromDefaultConfig()
    {
        string? env = Environment.GetEnvironmentVariable("SSH_CONFIG");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
        {
            return EnumerateHostAliases(env!);
        }

        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string defaultPath = Path.Combine(userProfile, ".ssh", "config");
        if (!File.Exists(defaultPath))
        {
            return Array.Empty<string>();
        }

        return EnumerateHostAliases(defaultPath);
    }

    public static IReadOnlyList<string> EnumerateHostAliases(string primaryConfigPath)
    {
        var seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        if (File.Exists(primaryConfigPath))
        {
            queue.Enqueue(Path.GetFullPath(primaryConfigPath));
        }

        while (queue.Count > 0)
        {
            string path = queue.Dequeue();
            if (!seenFiles.Add(path))
            {
                continue;
            }

            string[] lines;
            try
            {
                lines = File.ReadAllLines(path, Encoding.UTF8);
            }
            catch
            {
                continue;
            }

            string? configDir = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(configDir))
            {
                configDir = ".";
            }

            foreach (var raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                if (line.StartsWith("Include ", StringComparison.OrdinalIgnoreCase))
                {
                    string rest = line["Include ".Length..].Trim();
                    foreach (string inc in SplitIncludePaths(rest))
                    {
                        foreach (string expanded in ExpandIncludeToExistingFiles(inc, configDir))
                        {
                            queue.Enqueue(expanded);
                        }
                    }

                    continue;
                }

                if (!line.StartsWith("Host ", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string hostsPart = line["Host ".Length..].Trim();
                foreach (string token in hostsPart.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (token is "*" or "?" || token.Contains('*', StringComparison.Ordinal) || token.Contains('?', StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (token.StartsWith('!'))
                    {
                        continue;
                    }

                    aliases.Add(token);
                }
            }
        }

        return aliases.OrderBy(a => a, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IEnumerable<string> SplitIncludePaths(string rest)
    {
        var sb = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < rest.Length; i++)
        {
            char c = rest[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (!inQuotes && char.IsWhiteSpace(c))
            {
                if (sb.Length > 0)
                {
                    yield return sb.ToString();
                    sb.Clear();
                }

                continue;
            }

            sb.Append(c);
        }

        if (sb.Length > 0)
        {
            yield return sb.ToString();
        }
    }

    private static IEnumerable<string> ExpandIncludeToExistingFiles(string raw, string containingDir)
    {
        string p = ExpandUserAndRelative(raw.Trim(), containingDir);
        if (p.Contains('*', StringComparison.Ordinal) || p.Contains('?', StringComparison.Ordinal))
        {
            string? dirName = Path.GetDirectoryName(p);
            string fileName = Path.GetFileName(p);
            if (string.IsNullOrEmpty(dirName) || string.IsNullOrEmpty(fileName))
            {
                yield break;
            }

            if (!Directory.Exists(dirName))
            {
                yield break;
            }

            foreach (string f in Directory.EnumerateFiles(dirName, fileName))
            {
                yield return Path.GetFullPath(f);
            }

            yield break;
        }

        if (File.Exists(p))
        {
            yield return Path.GetFullPath(p);
        }
    }

    private static string ExpandUserAndRelative(string raw, string containingDir)
    {
        if (raw.StartsWith('~'))
        {
            string tail = raw.TrimStart('~').TrimStart('/', '\\');
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.GetFullPath(Path.Combine(profile, tail.Replace('/', Path.DirectorySeparatorChar)));
        }

        if (Path.IsPathRooted(raw))
        {
            return Path.GetFullPath(raw);
        }

        return Path.GetFullPath(Path.Combine(containingDir, raw));
    }
}
