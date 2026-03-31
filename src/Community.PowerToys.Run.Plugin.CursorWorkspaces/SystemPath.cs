using System.Text.RegularExpressions;

namespace Community.PowerToys.Run.Plugin.CursorWorkspaces;

internal static class SystemPath
{
    private static readonly Regex WindowsPath = new(@"^([a-zA-Z]:)", RegexOptions.Compiled);

    public static string RealPath(string path)
    {
        if (WindowsPath.IsMatch(path))
        {
            string windowsPath = path.Replace("/", "\\", StringComparison.Ordinal);
            return $"{windowsPath[0]}".ToUpperInvariant() + windowsPath.Remove(0, 1);
        }

        return path;
    }
}
