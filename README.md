# CodeWF.Log

[![NuGet](https://img.shields.io/nuget/v/CodeWF.Log.Core.svg)](https://www.nuget.org/packages/CodeWF.Log.Core/)
[![NuGet](https://img.shields.io/nuget/v/CodeWF.Log.Extensions.Logging.svg)](https://www.nuget.org/packages/CodeWF.Log.Extensions.Logging/)
[![NuGet](https://img.shields.io/nuget/v/CodeWF.Log.Avalonia.svg)](https://www.nuget.org/packages/CodeWF.Log.Avalonia/)
[![License](https://img.shields.io/github/license/dotnet9/CodeWF.LogViewer)](LICENSE)

CodeWF.Log 是面向 .NET 和 Avalonia 的轻量日志组件，按“核心日志、标准日志适配、Avalonia UI”拆分为三个包。纯控制台或单体程序只安装核心包；需要 `Microsoft.Extensions.Logging` 或联合 Serilog 等第三方日志组件时安装适配包；只有需要 Avalonia 日志视图或重要日志弹窗时才安装 UI 包。

完整设计约束见 [CodeWF.Log 设计](doc/CodeWF.Log设计.md)。

## 如何选择 NuGet 包

先按项目类型选择，不需要一次安装全部三个包：

| 项目类型 | 安装包 | 主要能力 |
| --- | --- | --- |
| 控制台、工具或不使用 Host/DI 的无 HMI 单体程序 | `CodeWF.Log.Core` | 静态 `Logger`、控制台输出、文件记录和文件轮转；不包含 Avalonia、`LogView` 或桌面弹窗 |
| ASP.NET Core、Generic Host、依赖注入项目 | `CodeWF.Log.Extensions.Logging` | `Microsoft.Extensions.Logging` Provider、`AddCodeWF()`、结构化日志、Scope 和 Activity；自动依赖 Core |
| 已使用 Serilog 等第三方日志组件 | `CodeWF.Log.Extensions.Logging` | 通过 MEL 统一编排多个 Provider；Serilog 等第三方组件仍需安装各自的适配包 |
| Avalonia 项目，只使用静态 `Logger`，需要 `LogView` 或重要日志弹窗 | `CodeWF.Log.Avalonia` | Avalonia 日志视图和桌面通知；自动依赖 Core，不强制使用 MEL |
| Avalonia 项目，使用 `ILogger<T>`、DI 或联合 Serilog | `CodeWF.Log.Avalonia` + `CodeWF.Log.Extensions.Logging` | 同时获得 Avalonia UI、桌面通知和标准日志生态集成 |

安装示例：

```shell
# 纯控制台/文件日志
dotnet add package CodeWF.Log.Core

# Microsoft.Extensions.Logging、Host、Web API 或第三方 Provider 编排
dotnet add package CodeWF.Log.Extensions.Logging

# Avalonia LogView 或重要日志弹窗
dotnet add package CodeWF.Log.Avalonia
```

`CodeWF.Log.Extensions.Logging` 和 `CodeWF.Log.Avalonia` 都依赖 `CodeWF.Log.Core`，但二者相互独立。纯 Avalonia 静态 API 场景可以只安装 `CodeWF.Log.Avalonia`；Avalonia + MEL/Serilog 场景需要同时安装后两个包。

三个包当前均面向 `net10.0`。

运行 `pack.bat` 可将三个包输出到 `artifacts/packages`。

### 按场景选择示例

| # | 场景 | 需要的包/能力 | Demo |
| --- | --- | --- | --- |
| 1 | Console + File | 只引用 `CodeWF.Log.Core`；关闭 EventFeed，不包含 Avalonia、LogView 或通知 | `ConsoleDemo` |
| 2 | 多 Avalonia LogView + File + 通知 | `CodeWF.Log.Avalonia` + `CodeWF.Log.Extensions.Logging`；三个分级 LogView、模板切换和显式通知 | `LogViewDemo` |
| 3 | 无 Avalonia LogView + File + 通知 | `CodeWF.Log.Avalonia` + `CodeWF.Log.Extensions.Logging`；保留通知能力但不放置 LogView | `FileNotifyDemo` |
| 4 | 配合 Serilog 使用 | `CodeWF.Log.Avalonia` + `CodeWF.Log.Extensions.Logging`；Serilog 负责文件/控制台，CodeWF 负责 LogView 和通知 | `SerilogDemo` |
| 5 | Web API | 只需直接安装 `CodeWF.Log.Extensions.Logging`，通过 `AddCodeWF()` 接入 MEL | `WebApiDemo` |

`ConsoleDemo` 是最小依赖场景：日志组件只做核心 Console/File Pipeline，不会加载 Avalonia，也没有产生系统弹窗的通道。

## Microsoft.Extensions.Logging

安装 `CodeWF.Log.Extensions.Logging` 后，通过标准 .NET 日志入口注册：

```csharp
builder.Logging.AddCodeWF();
```

默认约定：

- MEL 自身负责全局级别、Category 级别和多 Provider 编排。
- CodeWF 默认写入 `AppContext.BaseDirectory/logs`。
- 普通 `ILogger` 与 `LogUser*` 都生成完整 `CodeWFLogEvent`，进入启用的 File、Console 和 `LogEventFeed`。
- `LogUser*` 只额外提供 `UserMessage`；模板中的 `{UserMessage}` 为空白时回退 `{Message}`。
- File 使用独立 `OutputTemplate`；Console、可选的 LogView 和通知管线共享 `LineTemplate`，默认组合式 DesktopWindow 避免重复显示级别和时间。
- 两类模板都可通过各自的 Controller 显式、原子地运行时更新；其他 Pipeline 配置仍需重启生效。

常用配置使用结构化 Options；日志格式由 `OutputTemplate` 决定，不提供 `IncludeEventId`、`IncludeScopes` 这类开关。模板里写了对应占位符就输出，没有写就忽略：

```csharp
builder.Logging.AddCodeWF(options =>
{
    options.File.Enabled = true;
    options.File.DirectoryPath = "Log";
    options.File.OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] ({Category}) {Message} {Properties}{NewLine}{Exception}";

    options.LineTemplate =
        "{Timestamp:HH:mm:ss} 【{Level:u3}】 ({Category}) {UserMessage}{NewLine}";

    options.Console.Enabled = false;
    options.Capture.Scopes = true;
    options.Capture.Activity = true;
    options.Queue.Capacity = 10_000;
});
```

常用模板占位符：`Timestamp`、`Level`、`Category`、`EventId`、`EventName`、`Message`、`MessageTemplate`、`UserMessage`、`Properties`、`Scopes`、`Activity`、`TraceId`、`SpanId`、`Exception`、`NewLine`。

对应的 `appsettings.json`：

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    },
    "CodeWF": {
      "LogLevel": {
        "Default": "Trace",
        "Microsoft.AspNetCore": "Warning"
      },
      "File": {
        "Enabled": true,
        "DirectoryPath": "Log",
        "OutputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] ({Category}) {Message} {Properties}{NewLine}{Exception}"
      },
      "Console": {
        "Enabled": false
      },
      "LineTemplate": "{Timestamp:HH:mm:ss} 【{Level:u3}】 ({Category}) {UserMessage}{NewLine}",
      "EventFeed": {
        "Enabled": true,
        "RecentCapacity": 2000
      }
    }
  }
}
```

如果希望控制台统一使用 CodeWF 的 `LineTemplate`，应避免同时注册其他 Console Provider：

```csharp
builder.Logging.ClearProviders();
builder.Logging.AddCodeWF(options =>
{
    options.Console.Enabled = true;
    options.LineTemplate =
        "{Timestamp:HH:mm:ss} 【{Level:u3}】 ({Category}) {UserMessage}{NewLine}";
});
```

```csharp
logger.LogError(ex, "Failed to parse task file {TaskPath}", taskPath);

logger.LogUserError(
    ex,
    "任务文件加载失败，请检查文件格式后重新打开。",
    "Failed to parse task file {TaskPath}",
    taskPath);
```

## Core 静态 API（无 Host 场景）

控制台、工具和其他非 Host 场景可直接使用 `Logger.Initialize(...)`，无需安装 `CodeWF.Log.Extensions.Logging`：

```csharp
Logger.Initialize(new LoggerOptions
{
    MinimumLevel = LogLevel.Debug,
    EnableConsole = true,
    File = new FileLogOptions
    {
        DirectoryPath = Path.Combine(AppContext.BaseDirectory, "Log")
    }
});
```

静态 API 约定：

- `Logger.Info/Warn/Error/Fatal(...)` 写入完整事件；`userMessage` 是同一事件上的可选字段，`requestNotification` 默认 `false`。
- `Logger.Error(message, exception, userMessage)` 分别保存诊断消息、异常快照和用户消息。
- 只有显式传入 `requestNotification: true` 且日志级别达到 `LogNotifications.MinimumLevel` 时，日志才有资格触发通知。
- `Logger.*ToFile(...)` 只写文件，不进入 Console、LogView 或通知。
- `Logger.MinimumLevel`、`LoggerOptions.MinimumLevel` 使用 MEL 标准 `LogLevel`。

退出前调用：

```csharp
await Logger.ShutdownAsync();
```

运行时切换格式时，DI 场景注入 `ILineTemplateController` 或 `IFileOutputTemplateController`；静态 API 场景使用 `Logger.Events.LineTemplate` 和 `Logger.FileOutputTemplate`。模板校验失败时当前有效格式保持不变。

## Avalonia

`v11.3.14` 分支支持 Avalonia `[11.3.14, 12.0.0)`，组件包版本为 `11.3.14.x`。

`CodeWF.Log.Avalonia` 提供日志通知和 `LogView`，两项能力相互独立。只需要“日志文件 + 重要日志桌面通知”的应用不必在窗口中放置 `<log:LogView />`。

- 使用静态 `Logger` 时，只安装 `CodeWF.Log.Avalonia` 即可，它会自动依赖 Core。
- 使用 `ILogger<T>`、依赖注入或联合 Serilog 时，同时安装 `CodeWF.Log.Extensions.Logging` 和 `CodeWF.Log.Avalonia`。

### 文件日志 + 重要通知（无 LogView）

注册 Provider 时开启文件和事件 Feed。通知通过事件 Feed 接收新日志，所以不能把 `EventFeed.Enabled` 关闭：

```csharp
var logDirectory = Path.Combine(AppContext.BaseDirectory, "Log");

services.AddLogging(builder =>
{
    builder.SetMinimumLevel(LogLevel.Trace);
    builder.AddCodeWF(options =>
    {
        options.File.Enabled = true;
        options.File.DirectoryPath = logDirectory;
        options.Console.Enabled = false;
        options.EventFeed.Enabled = true;
    });
});
```

在 `App.axaml` 上配置通知。`MinimumLevel` 是级别门槛，不会让普通 Error 日志自动弹窗：

```xml
<Application
    xmlns="https://github.com/avaloniaui"
    xmlns:log="https://codewf.com"
    log:LogNotifications.Mode="DesktopWindow"
    log:LogNotifications.MinimumLevel="Error"
    log:LogNotifications.Duration="00:00:10"
    log:LogNotifications.ApplicationName="设备客户端" />
```

MEL/DI 场景还要把当前 Provider 的事件 Feed 交给通知组件：

```csharp
public override void OnFrameworkInitializationCompleted()
{
    LogContext.SetSource(this, Program.Services.GetRequiredService<LogEventFeed>());
    base.OnFrameworkInitializationCompleted();
}
```

普通日志只写入启用的输出；重要日志使用 `LogUserNotification(...)` 显式申请通知：

```csharp
logger.LogError("设备服务首次连接失败，后台继续重试。"); // 记录日志，不弹窗

logger.LogUserNotification(
    LogLevel.Error,
    "设备服务连接已中断，请检查服务状态。",
    "Device service connection failed after {RetryCount} retries",
    retryCount);
```

最终通知条件是：`Mode != None && Level >= MinimumLevel && RequestNotification`。普通 `LogError(...)`、`LogUserError(...)` 和 `Logger.Error(...)` 默认都不弹窗；静态 API 只有显式传入 `requestNotification: true` 才申请通知。`Logger.*ToFile(...)` 不进入事件 Feed，因此永远不会触发通知。

`DesktopWindow` 默认使用 360px 宽的组合式重要日志窗口：Error 与 Critical 分别使用圆形/三角形图标和不同红色渐变；单条、多条、长内容对应 284/320/420px 可见高度。窗口复用期间追加的日志在标题栏显示“`N条新日志`”，多条日志可前后翻页，长内容在正文区域滚动。默认正文显示 `UserMessage`，为空时回退 `Message`，避免与窗口已经独立显示的级别和时间重复；完整 `LineTemplate` 格式化结果仍通过 `LogNotificationContent.Content` 提供给 `DesktopContentTemplate`，InApp 通知也继续使用该结果。

颜色、尺寸、按钮和渐变可以通过 `LogNotificationResourceKeys` 对应的动态资源覆盖；需要完全不同的内容布局时使用 `LogNotifications.DesktopContentTemplate`。组件内嵌默认图标，调用方不需要复制图片文件。

完整可运行示例见 `FileNotifyDemo`，其中没有任何 `LogView`，并提供 Error、Critical、长内容、连续追加、仅写日志和低于阈值等对比按钮。

### 可选的 LogView

需要在应用界面查看实时日志时，再放置 `LogView`。XAML 命名空间保持不变：

```xml
<Window
    xmlns="https://github.com/avaloniaui"
    xmlns:log="https://codewf.com">
    <log:LogView
        MinimumLevel="Information"
        MaximumLevel="Critical"
        MaxDisplayCount="1000" />
</Window>
```

`LogView.MinimumLevel` 和 `LogView.MaximumLevel` 都是 `Microsoft.Extensions.Logging.LogLevel`，每个视图可以独立按区间显示完整事件。默认范围为 Information 至 Critical。

右键“查看日志”使用应用级 `LogContext.LogDirectory`，也可由单个 `LogView.LogDirectory` 覆盖。该路径与事件 Source 独立，适合 CodeWF 只负责界面、Serilog 负责文件的多 Provider 场景：

```csharp
LogContext.SetLogDirectory(this, Path.GetFullPath("Log"));
```

### 通知模式

通知也可以和 `LogView` 同时使用，配置方式不变：

```xml
<Application
    xmlns="https://github.com/avaloniaui"
    xmlns:log="https://codewf.com"
    log:LogNotifications.Mode="DesktopWindow"
    log:LogNotifications.MinimumLevel="Error"
    log:LogNotifications.Duration="00:00:10"
    log:LogNotifications.ApplicationName="CodeWF Log Demo" />
```

`LogNotifications` 只接收 `RequestNotification=true` 且达到 `MinimumLevel` 的新事件，不回放历史，也不会接收 `*ToFile` 日志。静态 Logger 通过 `requestNotification: true` 显式请求；`ILogger<T>` 通过 `LogUserNotification(...)` 请求。InApp 默认最多同时显示 3 条；DesktopWindow 复用一个桌面右下角窗口，并在同一窗口显示新日志计数和翻页状态。通知展示队列溢出时提示查看日志文件，不假定应用存在 `LogView`。

```csharp
Logger.Error(
    "设备服务心跳超时。",
    userMessage: "设备服务连接已中断，请检查服务状态。",
    requestNotification: true);

logger.LogUserNotification(
    LogLevel.Error,
    "设备服务连接已中断，请检查服务状态。",
    "Device service heartbeat timed out for {TaskName}",
    taskName);
```

## Demos

| Demo | Purpose |
| --- | --- |
| `ConsoleDemo` | 仅 `CodeWF.Log.Core`：关闭 EventFeed，只演示静态 Logger、控制台、文件、`*ToFile` 和文件轮转；没有视图或弹窗。 |
| `LogViewDemo` | Avalonia 多 LogView：MEL/DI、分级视图、两类模板、结构化上下文、异常和通知。 |
| `FileNotifyDemo` | Avalonia 无 LogView：文件输出、文件模板切换，以及 Error/Critical、长内容、连续追加和通知门槛对比。 |
| `SerilogDemo` | Avalonia 联合 Provider：Serilog 负责文件/控制台，CodeWF 负责 LogView 和通知。 |
| `WebApiDemo` | ASP.NET Core：`AddCodeWF()`、配置绑定、Scope、Activity、LoggerMessage 和最近事件接口。 |
