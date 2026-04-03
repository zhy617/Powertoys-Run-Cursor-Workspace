using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Community.PowerToys.Run.Plugin.CursorWorkspaces.WorkspacesHelper;

public partial class Rfc3986Uri
{
    [GeneratedRegex(@"^((?<scheme>[^:/?#]+):)?(//(?<authority>[^/?#]*))?(?<path>[^?#]*)(\?(?<query>[^#]*))?(#(?<fragment>.*))?$")]
    private static partial Regex Rfc3986();

    public string Scheme { get; private set; } = string.Empty;

    public string Authority { get; private set; } = string.Empty;

    public string Path { get; private set; } = string.Empty;

    public string Query { get; private set; } = string.Empty;

    public string Fragment { get; private set; } = string.Empty;

    public static Rfc3986Uri? Parse([StringSyntax("Uri")] string uriString)
    {
        if (uriString is null)
        {
            return null;
        }

        if (Rfc3986().Match(uriString) is { Success: true } match)
        {
            return new Rfc3986Uri
            {
                Scheme = match.Groups["scheme"].Value,
                Authority = match.Groups["authority"].Value,
                Path = match.Groups["path"].Value,
                Query = match.Groups["query"].Value,
                Fragment = match.Groups["fragment"].Value,
            };
        }

        return TryParseVscodeRemoteLoose(uriString);
    }

    /// <summary>新版 Cursor 的 authority 可能极长或含非常规字符，正则不匹配时用 <c>vscode-remote://</c> 前缀做宽松拆分。</summary>
    private static Rfc3986Uri? TryParseVscodeRemoteLoose(string uriString)
    {
        const string prefix = "vscode-remote://";
        if (!uriString.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        ReadOnlySpan<char> rest = uriString.AsSpan(prefix.Length);
        int slash = rest.IndexOf('/');
        if (slash < 0)
        {
            return new Rfc3986Uri
            {
                Scheme = "vscode-remote",
                Authority = rest.ToString(),
                Path = "/",
            };
        }

        string authority = rest[..slash].ToString();
        string path = rest[slash..].ToString();
        if (string.IsNullOrEmpty(path))
        {
            path = "/";
        }

        return new Rfc3986Uri
        {
            Scheme = "vscode-remote",
            Authority = authority,
            Path = path,
        };
    }
}
