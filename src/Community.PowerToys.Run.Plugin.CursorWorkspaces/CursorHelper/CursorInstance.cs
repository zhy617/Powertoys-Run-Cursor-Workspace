using System.Windows.Media.Imaging;

namespace Community.PowerToys.Run.Plugin.CursorWorkspaces.CursorHelper;

public sealed class CursorInstance
{
    public string ExecutablePath { get; set; } = string.Empty;

    public string AppData { get; set; } = string.Empty;

    /// <summary>供 PowerToys Run 结果列表使用的绝对路径图标（见 Main.Query 中 IcoPath）。</summary>
    public string WorkspaceIcoPath { get; set; } = string.Empty;

    /// <summary>远程工作区（SSH/WSL 等）列表图标绝对路径。</summary>
    public string RemoteIcoPath { get; set; } = string.Empty;

    public BitmapImage WorkspaceIconBitMap { get; set; } = null!;

    public BitmapImage RemoteIconBitMap { get; set; } = null!;
}
