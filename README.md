# CodeWF.Log

[![NuGet](https://img.shields.io/nuget/v/CodeWF.Log.Core.svg)](https://www.nuget.org/packages/CodeWF.Log.Core/)
[![NuGet](https://img.shields.io/nuget/v/CodeWF.Log.Extensions.Logging.svg)](https://www.nuget.org/packages/CodeWF.Log.Extensions.Logging/)
[![NuGet](https://img.shields.io/nuget/v/CodeWF.Log.Avalonia.svg)](https://www.nuget.org/packages/CodeWF.Log.Avalonia/)
[![License](https://img.shields.io/github/license/dotnet9/CodeWF.LogViewer)](LICENSE)

CodeWF.Log 是面向 .NET 和 Avalonia 的轻量日志组件。新版本以 `Microsoft.Extensions.Logging` 为主入口，同时保留 `Logger.Info/Warn/Error/Fatal`、`Logger.*ToFile`、`<log:LogView />` 等原有常用方式，方便旧项目逐步迁移。

## Packages

| Package | Purpose | Targets |
| --- | --- | --- |
| `CodeWF.Log.Core` | 文件/控制台日志、完整事件 Feed、静态 `Logger` API | `net10.0` |
| `CodeWF.Log.Extensions.Logging` | `Microsoft.Extensions.Logging` Provider 和 `AddCodeWF()` | `net10.0` |
| `CodeWF.Log.Avalonia` | `LogView` 和日志通知 | `net10.0` |

运行 `pack.bat` 可将三个包输出到 `artifacts/packages`。

## Microsoft.Extensions.Logging

推荐新项目使用标准 .NET 日志入口：

```csharp
builder.Logging.AddCodeWF();
```

默认约定：

- MEL 自身负责全局级别、Category 级别和多 Provider 编排。
- CodeWF 默认写入 `AppContext.BaseDirectory/logs`。
- 普通 `ILogger` 与 `LogUser*` 都生成完整 `CodeWFLogEvent`，进入启用的 File、Console 和 `LogEventFeed`。
- `LogUser*` 只额外提供 `UserMessage`；模板中的 `{UserMessage}` 为空白时回退 `{Message}`。
- File 使用独立 `OutputTemplate`；Console、LogView 和通知严格共享 `LineTemplate`。
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
        "{Timestamp:HH:mm:ss} [{Level:u3}] ({Category}) {UserMessage}{NewLine}";

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
      "LineTemplate": "{Timestamp:HH:mm:ss} [{Level:u3}] ({Category}) {UserMessage}{NewLine}",
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
        "{Timestamp:HH:mm:ss} [{Level:u3}] ({Category}) {UserMessage}{NewLine}";
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

## Legacy Static API

非 Host 场景仍可使用 `Logger.Initialize(...)`：

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

- `Logger.Info/Warn/Error/Fatal(...)` 写入完整事件；`userMessage` 是同一事件上的可选字段。
- `Logger.Error(message, exception, userMessage)` 分别保存诊断消息、异常快照和用户消息。
- `Logger.*ToFile(...)` 只写文件，不进入 Console、LogView 或通知。
- `Logger.MinimumLevel`、`LoggerOptions.MinimumLevel` 使用 MEL 标准 `LogLevel`。

退出前调用：

```csharp
await Logger.ShutdownAsync();
```

运行时切换格式时，DI 场景注入 `ILineTemplateController` 或 `IFileOutputTemplateController`；静态 API 场景使用 `Logger.Events.LineTemplate` 和 `Logger.FileOutputTemplate`。模板校验失败时当前有效格式保持不变。

## Avalonia

XAML 命名空间保持不变：

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

通知只保留阈值语义：

```xml
<Application
    xmlns="https://github.com/avaloniaui"
    xmlns:log="https://codewf.com"
    log:LogNotifications.Mode="DesktopWindow"
    log:LogNotifications.MinimumLevel="Error"
    log:LogNotifications.Duration="00:00:10"
    log:LogNotifications.ApplicationName="CodeWF Log Demo" />
```

`LogNotifications` 只按 `MinimumLevel` 接收启用后的新事件，不回放历史，也不会接收 `*ToFile` 日志。InApp 默认最多同时显示 3 条；DesktopWindow 复用一个桌面右下角窗口。

## Demos

| Demo | Purpose |
| --- | --- |
| `ConsoleLogDemo` | 验证传统静态 `Logger.*`、`*ToFile`、文件轮转和控制台用户输出。 |
| `AvaloniaLogDemo` | 验证传统静态 `Logger.*`、LineTemplate/OutputTemplate 切换、Avalonia `LogView` 和通知。 |
| `MicrosoftLoggingAvaloniaDemo` | 验证 `ILogger<T>`、DI、`AddCodeWF()`、两类模板切换、Avalonia `LogView` 和通知。 |
| `MicrosoftLoggingWebApiDemo` | 验证 .NET Web API 中的 `builder.Logging.AddCodeWF()`、普通诊断日志、用户日志、Scope 和 Activity。 |
| `MultiProviderAvaloniaDemo` | 验证 Serilog 负责文件/控制台、CodeWF 负责 LogView/通知和 LineTemplate 切换，以及私有 `UserMessage` 元数据隔离。 |
