# Cursor 工作区 · PowerToys Run 插件

在 **PowerToys Run** 里快速搜索并打开 [Cursor](https://cursor.com/) 的最近工作区：本地文件夹、`.code-workspace` 多根工作区，以及 **Dev Container** 等远程环境条目。

[![PowerToys](https://img.shields.io/badge/PowerToys-Run-0078D4?style=flat&logo=microsoft)](https://github.com/microsoft/PowerToys)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat&logo=dotnet)](https://dotnet.microsoft.com/)

---

## 功能概览

| 能力 | 说明 |
|------|------|
| **列表来源** | 读取 Cursor 用户数据中的最近打开记录（与 VS Code 类似的存储布局） |
| **本地工作区** | 项目文件夹与 `.code-workspace` 文件 |
| **远程 / 容器** | 识别 Dev Container 等环境，并在标题中标注类型与附加信息 |
| **打开方式** | 使用 Cursor 的 `--folder-uri` / `--file-uri` 启动对应工作区 |
| **右键菜单** | 复制路径、在资源管理器中打开、在终端中打开（含快捷键提示） |

默认 **激活词** 为 `(`（可在 PowerToys → PowerToys Run → 插件 中修改）。  
在 Run 中输入 `(` 后键入关键字即可按名称筛选；无关键字时按标题排序浏览。

---

## 环境要求

- **Windows 10 / 11**（x64 或 ARM64）
- 已安装 [**PowerToys**](https://github.com/microsoft/PowerToys)（需包含 **PowerToys Run**）
- 本机已安装 **Cursor**，且曾用 Cursor 打开过工作区（否则列表可能为空）

---

## 安装

### 方式一：从 Releases 安装（推荐）

1. 打开 [**Releases**](https://github.com/zhy617/Powertoys-Run-Cursor-Workspace/releases)，下载与系统架构匹配的压缩包：  
   - **x64**：`CursorWorkspace-v*.*.*-win-x64.zip`  
   - **ARM64**：`CursorWorkspace-v*.*.*-win-arm64.zip`
2. 解压到 PowerToys Run 插件目录下的**单独子文件夹**，例如：

   `%LocalAppData%\Microsoft\PowerToys\PowerToys Run\Plugins\CursorWorkspaces\`

   解压后该目录内应直接可见 `plugin.json`、`Community.PowerToys.Run.Plugin.CursorWorkspaces.dll` 与 `Images` 等文件。
3. **重启 PowerToys**（或至少重启 PowerToys Run），在 **PowerToys 设置 → PowerToys Run → 插件** 中确认 **「Cursor 工作区」** 已启用。

> 若升级版本，建议先退出 PowerToys，用新文件覆盖旧插件目录后再启动。

### 方式二：从源码构建

见下文 [从源码构建](#从源码构建)。构建产物同样需放入上述 `Plugins` 下的独立文件夹中使用。

---

## 使用说明

1. 使用你习惯的快捷键打开 **PowerToys Run**（默认 `Alt + Space`，以你的设置为准）。
2. 输入激活词 **`(`**（若未改为全局插件，需先输入该前缀）。
3. 继续输入项目名称或路径片段进行筛选；**回车** 用 Cursor 打开该项。
4. 选中条目时可通过 **上下文菜单**（快捷键见菜单标题）复制路径、在资源管理器或终端中打开。

---

## 从源码构建

**依赖**：[.NET SDK 8](https://dotnet.microsoft.com/download/dotnet/8.0)（Windows）

在仓库根目录执行：

```powershell
dotnet build CursorWorkspaces.sln -c Release -p:Platform=x64
# 或 ARM64
dotnet build CursorWorkspaces.sln -c Release -p:Platform=ARM64
```

输出位于：

`src\Community.PowerToys.Run.Plugin.CursorWorkspaces\bin\<平台>\Release\`

打包为发布用 zip、生成校验和、以及使用 GitHub CLI 创建 Release 等步骤，可参见 [`scripts/README.md`](scripts/README.md)。

---

## 常见问题

- **列表为空**  
  请先用 Cursor 正常打开几次项目或工作区文件，再重试 Run；并确认 Cursor 安装在常见路径（如 `%LocalAppData%\Programs\cursor\`）。

- **插件显示「初始化错误」**  
  本插件在首次查询时再加载图标与实例信息；若仍报错，可查看 PowerToys 日志并确认仅使用与系统架构一致的构建（x64 / ARM64）。

- **激活词冲突**  
  在 PowerToys Run 插件设置中为「Cursor 工作区」指定其他 **操作关键字**，或按需开启全局模式（若你自行修改 `plugin.json` 中的相关选项，请自行承担兼容性风险）。

---

## 仓库与反馈

- **源码**：<https://github.com/zhy617/Powertoys-Run-Cursor-Workspace>  
- 问题与建议请通过 [Issues](https://github.com/zhy617/Powertoys-Run-Cursor-Workspace/issues) 反馈。

---

<p align="center">
  <sub>与 PowerToys 及 Cursor 无官方隶属关系；名称与商标归各自权利人所有。</sub>
</p>
