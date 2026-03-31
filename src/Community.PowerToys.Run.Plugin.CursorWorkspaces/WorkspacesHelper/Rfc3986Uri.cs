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
        return uriString is not null && Rfc3986().Match(uriString) is { Success: true } match
            ? new Rfc3986Uri
            {
                Scheme = match.Groups["scheme"].Value,
                Authority = match.Groups["authority"].Value,
                Path = match.Groups["path"].Value,
                Query = match.Groups["query"].Value,
                Fragment = match.Groups["fragment"].Value,
            }
            : null;
    }
}
