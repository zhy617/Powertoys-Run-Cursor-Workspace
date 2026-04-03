namespace Community.PowerToys.Run.Plugin.CursorWorkspaces;

/// <summary>面向 PowerToys Run 列表与设置的界面文案（简体中文）。</summary>
internal static class PluginStrings
{
    public const string PluginTitle = "Cursor 工作区";
    public const string PluginDescription = "从 Cursor 最近打开列表中搜索并打开文件夹或多根工作区文件。";

    public const string Workspace = "工作区";
    public const string ProjectFolder = "项目文件夹";
    public const string In = "在";
    public const string TypeWorkspaceLocal = "本地";
    public const string TypeWorkspaceDevContainer = "Dev Container";

    public const string CopyPath = "复制路径";
    public const string OpenInExplorer = "在资源管理器中打开";
    public const string OpenInConsole = "在终端中打开";

    /// <summary>仅 ssh config、尚未在 Cursor 中打开过远程文件夹的补充项。</summary>
    public const string SshConfigHostOnly = "ssh config（尚未在 Cursor 打开过远程文件夹）";
}
