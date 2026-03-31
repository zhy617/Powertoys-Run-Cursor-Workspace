using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Community.PowerToys.Run.Plugin.CursorWorkspaces.CursorHelper;

public sealed class CursorInstance
{
    public string ExecutablePath { get; set; } = string.Empty;

    public string AppData { get; set; } = string.Empty;

    public ImageSource WorkspaceIcon => WorkspaceIconBitMap;

    public BitmapImage WorkspaceIconBitMap { get; set; } = null!;

    public BitmapImage RemoteIconBitMap { get; set; } = null!;
}
