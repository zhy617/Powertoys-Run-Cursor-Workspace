using Community.PowerToys.Run.Plugin.CursorWorkspaces.WorkspacesHelper;
using Xunit;

namespace Community.PowerToys.Run.Plugin.CursorWorkspaces.Tests;

public sealed class WorkspaceUriCoreTests
{
    /// <summary>Roaming workspaceStorage 中 deepseek-vl2-bridge 的真实 folder 字段（JSON 十六进制 hostName）。</summary>
    public const string DeepseekBridgeUriFromJson =
        "vscode-remote://ssh-remote%2B7b22686f73744e616d65223a22446565705365656b564c32227d/root/fsas/vlm/deepseek-vl2-bridge";

    /// <summary>截图中出现的另一种 authority 后缀（非 JSON 十六进制），用于宽松解析回归。</summary>
    public const string DeepseekBridgeUriScreenshotStyle =
        "vscode-remote://ssh-remote%2B2b22b6ff97b84610d6527fa2f44c652051666c5fa6c4c2227d/root/fsas/vlm/deepseek-vl2-bridge";

    [Fact]
    public void DeepseekBridge_StandardStoredUri_Parses_And_FolderNameIsBridge()
    {
        Assert.True(
            WorkspaceUriCore.TryCreateWorkspace(DeepseekBridgeUriFromJson, null, false, out var parts, out var err),
            err);
        Assert.Null(err);
        Assert.NotNull(parts);
        Assert.Equal("/root/fsas/vlm/deepseek-vl2-bridge", parts!.RelativePath);
        Assert.Equal("deepseek-vl2-bridge", parts.FolderName);
        Assert.Equal(WorkspaceEnvironment.RemoteSSH, parts.WorkspaceEnvironment);
    }

    [Fact]
    public void DeepseekBridge_ScreenshotStyleAuthority_Parses()
    {
        Assert.True(
            WorkspaceUriCore.TryCreateWorkspace(DeepseekBridgeUriScreenshotStyle, null, false, out var parts, out var err),
            err);
        Assert.Null(err);
        Assert.NotNull(parts);
        Assert.Equal("/root/fsas/vlm/deepseek-vl2-bridge", parts!.RelativePath);
        Assert.Equal("deepseek-vl2-bridge", parts.FolderName);
    }

    [Fact]
    public void SafeUnescape_DoesNotThrow_OnMalformedPercent()
    {
        var bad = "vscode-remote://ssh-remote%2Bxx%ZZinvalid/root/foo";
        var s = ParseAuthority.SafeUnescapeDataString(bad);
        Assert.NotNull(s);
        var ok = WorkspaceUriCore.TryCreateWorkspace(bad, null, false, out _, out var reason);
        Assert.True(ok || !string.IsNullOrEmpty(reason));
    }

    [Fact]
    public void Rfc3986_Parse_StoredAndUnescaped_YieldSamePath()
    {
        var stored = DeepseekBridgeUriFromJson;
        var unescaped = ParseAuthority.SafeUnescapeDataString(stored);
        var r1 = Rfc3986Uri.Parse(stored);
        var r2 = Rfc3986Uri.Parse(unescaped);
        Assert.NotNull(r1);
        Assert.NotNull(r2);
        // %2B 与 + 在 authority 中不等价字符串，但 path 应一致。
        Assert.Equal(r1!.Path, r2!.Path);
    }
}
