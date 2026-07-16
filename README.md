# CodeWF.Log

[![NuGet](https://img.shields.io/nuget/v/CodeWF.Log.Core.svg)](https://www.nuget.org/packages/CodeWF.Log.Core/)
[![NuGet](https://img.shields.io/nuget/dt/CodeWF.Log.Core.svg)](https://www.nuget.org/dt/CodeWF.Log.Core.svg)
[![NuGet](https://img.shields.io/nuget/v/CodeWF.LogViewer.Avalonia.svg)](https://www.nuget.org/packages/CodeWF.LogViewer.Avalonia/)
[![NuGet](https://img.shields.io/nuget/dt/CodeWF.LogViewer.Avalonia.svg)](https://www.nuget.org/packages/CodeWF.LogViewer.Avalonia/)
[![License](https://img.shields.io/github/license/dotnet9/CodeWF.LogViewer)](LICENSE)

轻量级、高性能 .NET 日志库，支持控制台和 Avalonia UI 应用程序。

## 仓库规范

- 当前版本：`12.1.0.4`，版本号统一维护在根目录 `Directory.Build.props` 的 `<Version>` 节点。
- NuGet 包项目统一支持 `net8.0;net10.0`；Demo、App、测试与内部应用项目统一使用 `net11.0` / `net11.0-windows`。
- 根目录 `logo.svg`、`logo.png`、`logo.ico` 是唯一图标源，子工程只通过 MSBuild `Link` 引用，不维护图标副本。
- 运行时帮助、Markdown 示例、内置备忘录、设计说明等业务文档按功能保留；仓库级入口文档使用根目录 `README.md` 和 `UpdateLog.md`。

## 两个 NuGet 包

| 包名 | 说明 | 适用场景 |
|------|------|---------|
| **CodeWF.Log.Core** | 核心日志库，仅依赖 .NET | 控制台程序、WPF、Avalonia 等所有 C# 程序 |
| **CodeWF.LogViewer.Avalonia** | Avalonia UI 控件，依赖 CodeWF.Log.Core | Avalonia UI 程序，提供日志展示控件 |

## 脚本

- `pack.bat`：还原、构建并打包 `CodeWF.Log.Core` 与 `CodeWF.LogViewer.Avalonia` 到 `artifacts\packages`。

---

## CodeWF.Log.Core

核心日志库，NuGet 包安装：

```shell
Install-Package CodeWF.Log.Core
```

### 基本使用

```csharp
Logger.Debug("调试日志");
Logger.Info("普通日志");
Logger.Warn("警告日志");
Logger.Error("错误日志");
Logger.Fatal("严重错误日志");
```

### 控制台程序初始化（重要）

控制台程序使用文件日志时，需要在启动时初始化：

```csharp
// Program.cs 或 Main 方法中调用一次
Logger.RecordToFile();

// 程序退出时刷新缓冲区
await Logger.FlushAsync();
```

### 日志输出目标控制

每个日志方法支持参数控制输出目标：

```csharp
Logger.Info(
    content: "写入文件的内容",
    uiContent: "UI显示的友好内容",  // 可选，默认为null，此时UI显示content参数的内容
    log2UI: true,       // 是否输出到UI
    log2File: true,      // 是否输出到文件
    log2Console: true    // 是否输出到控制台
);

// 快捷方法
Logger.InfoToFile("仅写入文件");           // log2UI=false, log2Console=false
Logger.LogToUI(LogType.Info, "仅显示UI");  // log2File=false
```

### 配置参数

```csharp
Logger.Level = LogType.Info;                    // 日志级别，低于此级别的日志被忽略
Logger.LogDir = "/path/to/logs";                // 日志文件存储目录
Logger.BatchProcessSize = 200;                  // 批量写入的日志条数阈值
Logger.MaxLogFileSizeMB = 500;                   // 单个日志文件最大大小（MB）
Logger.EnableConsoleOutput = true;               // 是否输出到控制台
```

---

## CodeWF.LogViewer.Avalonia

Avalonia UI 日志展示控件，NuGet 包安装：

```shell
Install-Package CodeWF.LogViewer.Avalonia
```

### XAML 使用

```html
xmlns:log="https://codewf.com"
```

```html
<log:LogView />
```

### Avalonia 程序初始化

```csharp
// 程序退出时调用
await Logger.FlushAsync();
```

> 注意：`LogView` 控件内部会自动调用 `RecordToFile()` 启动文件日志记录，并从 UI 通道消费日志显示到界面。

### 日志消费

`LogView` 内部使用 `await foreach` 异步枚举模式消费 UI 通道日志，支持批量处理和防抖机制：
- 日志数量达到 `BatchProcessSize` 时立即刷新 UI
- 未达阈值时，最长延迟 `LogUIDuration`（默认100ms）后刷新

### 重要日志弹出通知

`LogView` 可以把达到指定级别的日志显示为应用内 Notification 或桌面右下角独立窗口。
通知订阅独立于 UI 日志通道，因此 `log2UI=false` 或 `ErrorToFile`、`FatalToFile` 产生的重要日志仍然可以弹出。

默认值：

| 属性 | 默认值 | 说明 |
| --- | --- | --- |
| `NotificationMode` | `None` | `None`、`InApp` 或 `DesktopWindow` |
| `NotificationApplicationName` | 当前进程名 | 通知标题中的应用名称或标识 |
| `NotificationLevel` | `Error` | 最低弹出级别，默认弹出 Error 和 Fatal |
| `NotificationDuration` | `00:00:10` | 每条日志的自动显示时间；`TimeSpan.Zero` 表示不自动关闭 |
| `NotificationHost` | `null` | 未设置时自动使用 `LogView` 所属的 TopLevel |
| `DesktopNotificationContentTemplate` | `null` | 桌面窗口正文的自定义模板 |

最简桌面窗口配置：

```html
<log:LogView NotificationMode="DesktopWindow" />
```

应用内 Notification：

```html
<log:LogView NotificationMode="InApp" />
```

后台配置：

```csharp
MainLogView.NotificationMode = LogNotificationMode.DesktopWindow;
MainLogView.NotificationApplicationName = "NURES历史数据服务";
MainLogView.NotificationLevel = LogType.Error;
MainLogView.NotificationDuration = TimeSpan.FromSeconds(10);
MainLogView.NotificationHost = this; // Window / TopLevel，可省略并自动获取
```

桌面窗口支持：

- 标题栏显示进程/应用名和倒计时；Hover 时隐藏倒计时。
- 正文依次显示级别图标、日志级别、可选择复制的日志内容和日志时间。
- 序号仅显示在上一条/下一条导航区，单条日志时隐藏导航区。
- 初始批次从第一条开始，每条自动显示指定时间；实时新增日志会立即切换到最新一条。
- 上一条、下一条导航，鼠标 Hover 暂停，移出后重新开始倒计时，最后 3 秒渐隐。
- 最多保留最近 100 条，无人操作的单次自动轮播最长 2 分钟。
- `Enter`/`Esc` 关闭，`←`/`→` 切换日志。

桌面窗口样式全部使用 `DynamicResource`。调用方可在 `Application.Resources` 中覆盖公开资源 Key：

```html
<Application.Resources>
    <x:Double x:Key="CodeWFLogDesktopNotificationWindowHeight">460</x:Double>
    <x:Double x:Key="CodeWFLogDesktopNotificationContentMaxHeight">230</x:Double>
    <SolidColorBrush x:Key="CodeWFLogDesktopNotificationConfirmBackground">#7C3AED</SolidColorBrush>
    <SolidColorBrush x:Key="CodeWFLogDesktopNotificationTitleBarBackground">#EEF4FF</SolidColorBrush>
</Application.Resources>
```

窗体默认高度、最小/最大高度和正文最大高度均可覆盖；可用 Key 定义在 `LogNotificationResourceKeys`。默认资源包含在现有 `CodeWF.LogViewer.Avalonia` 包内，
不需要额外引入 Semi、Ursa 或 Theme 包。

---

## 更新日志

### V1.0.12（2026-04）

1. ✨[优化]-重构为 Channel 架构，提升性能
2. ✨[优化]-添加防抖机制，避免日志频繁刷新
3. ✨[优化]-UI消费使用 `await foreach` 异步枚举模式
4. 🐛[修复]-修复 FlushAsync 方法

### V1.0.11.3（2025-09-15）

1. 🐛[修复]-修复自定义日志目录打开异常问题

TODO

## 第三方开源组件审计（2026-05-20）

检查方式：NuGet 元数据、恢复后的 `project.assets.json`、NuGet.org 与源码仓库信息。优先接受 MIT / Apache-2.0 / BSD。

| 包 | 使用范围 | 协议 | 源码/项目地址 | 结论 |
| --- | --- | --- | --- | --- |
| `Avalonia` / `Avalonia.Desktop` / `Avalonia.Fonts.Inter` / `Avalonia.Themes.Fluent` | Avalonia 日志查看器和示例 | MIT | https://github.com/AvaloniaUI/Avalonia | 通过 |
| `CodeWF.Tools.Core` | 日志核心辅助能力 | MIT | https://github.com/dotnet9/CodeWF.Tools | 自研开源包，已更新到 `1.3.13.2` |
| `VC-LTL` | Windows 示例运行时兼容 | EPL-2.0 | https://github.com/Chuyu-Team/VC-LTL5 | 源码开放，按“非优先但可追溯”通过 |
| `YY-Thunks` | Windows 示例运行时兼容 | MIT | https://github.com/Chuyu-Team/YY-Thunks | 通过 |

传递依赖检查结论：Avalonia/SkiaSharp/ANGLE 链均有公开源码，许可证为 MIT 或 BSD-style。未发现 `Semi.Avalonia.Dock`、`Semi.Avalonia.ProDataGrid`、`Semi.Avalonia.AvaloniaEdit` 或其它黑盒主题包。
## 包版本维护约定

XML 文件统一使用两个空格缩进。`Directory.Packages.props` 统一承载 NuGet 中央包管理开关和包版本变量，包括 `AvaloniaVersion` 等共享版本属性；`Directory.Build.props` 仅保留项目构建、编译选项和 NuGet 元数据。仓库如引用 `VC-LTL`、`YY-Thunks`，这两个兼容旧版操作系统的特殊包必须使用最新预览版。
