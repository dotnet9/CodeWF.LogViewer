# CodeWF.Log

[![NuGet](https://img.shields.io/nuget/v/CodeWF.Log.Core.svg)](https://www.nuget.org/packages/CodeWF.Log.Core/)
[![NuGet](https://img.shields.io/nuget/v/CodeWF.Log.Extensions.Logging.svg)](https://www.nuget.org/packages/CodeWF.Log.Extensions.Logging/)
[![NuGet](https://img.shields.io/nuget/v/CodeWF.Log.Avalonia.svg)](https://www.nuget.org/packages/CodeWF.Log.Avalonia/)
[![License](https://img.shields.io/github/license/dotnet9/CodeWF.LogViewer)](LICENSE)

CodeWF.Log 是面向 .NET 和 Avalonia 的轻量日志组件。新版本以 `Microsoft.Extensions.Logging` 为主入口，同时保留 `Logger.Info/Warn/Error/Fatal`、`Logger.*ToFile`、`<log:LogView />` 等原有常用方式，方便旧项目逐步迁移。

## Packages

| Package | Purpose | Targets |
| --- | --- | --- |
| `CodeWF.Log.Core` | 文件日志、用户日志 feed、静态 `Logger` API | `net8.0;net10.0` |
| `CodeWF.Log.Extensions.Logging` | `Microsoft.Extensions.Logging` Provider 和 `AddCodeWF()` | `net8.0;net10.0` |
| `CodeWF.Log.Avalonia` | `LogView` 和日志通知 | `net8.0;net10.0` |

运行 `pack.bat` 可将三个包输出到 `artifacts/packages`。

## Microsoft.Extensions.Logging

推荐新项目使用标准 .NET 日志入口：

```csharp
builder.Logging.AddCodeWF();
```

默认约定：

- MEL 自身负责全局级别、Category 级别和多 Provider 编排。
- CodeWF 默认写入 `AppContext.BaseDirectory/logs`。
- 普通 `ILogger` 日志默认是诊断日志，只进入文件等诊断 sink。
- 用户可见日志通过 `LogUserInformation/LogUserWarning/LogUserError/LogUserCritical` 表达，并进入 `UserLogFeed`、`LogView` 和通知。

常用配置使用结构化 Options；日志格式由 `OutputTemplate` 决定，不提供 `IncludeEventId`、`IncludeScopes` 这类开关。模板里写了对应占位符就输出，没有写就忽略：

```csharp
builder.Logging.AddCodeWF(options =>
{
    options.File.Enabled = true;
    options.File.DirectoryPath = "Log";
    options.File.OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] ({Category}) {Message} {Properties}{NewLine}{Exception}";

    // Web API 默认已有 Microsoft Console Provider；只有想让 CodeWF 接管控制台格式时才启用。
    options.Console.Enabled = false;
    options.UserLog.Mode = UserLogMode.ExplicitOnly;
    options.Capture.Scopes = true;
    options.Capture.Activity = true;
    options.Queue.Capacity = 10_000;
});
```

常用模板占位符：`Timestamp`、`Level`、`Category`、`EventId`、`EventName`、`Message`、`MessageTemplate`、`UserMessage`、`Properties`、`UserProperties`、`Scopes`、`Activity`、`TraceId`、`SpanId`、`Exception`、`NewLine`。

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
      "UserLog": {
        "Mode": "ExplicitOnly"
      }
    }
  }
}
```

如果希望 Web API 控制台也使用 CodeWF 的 `OutputTemplate`，需要让 CodeWF 接管控制台输出：

```csharp
builder.Logging.ClearProviders();
builder.Logging.AddCodeWF(options =>
{
    options.Console.Enabled = true;
    options.Console.OutputTemplate =
        "{Timestamp:HH:mm:ss} [{Level:u3}] ({Category}) {Message}{NewLine}{Exception}";
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

- `Logger.Info/Warn/Error/Fatal(...)` 写入诊断日志，并发布用户安全日志。
- `Logger.Error(message, exception, userMessage)` 文件记录技术消息和异常，UI 显示 `userMessage`。
- `Logger.*ToFile(...)` 只写诊断日志，不进入 UI、通知或控制台用户输出。
- `Logger.MinimumLevel`、`LoggerOptions.MinimumLevel` 使用 MEL 标准 `LogLevel`。

退出前调用：

```csharp
await Logger.ShutdownAsync();
```

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

`LogView.MinimumLevel` 和 `LogView.MaximumLevel` 都是 `Microsoft.Extensions.Logging.LogLevel`，每个视图可以独立按区间显示用户日志。

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

`LogNotifications` 只接收启用后的新用户日志，不回放历史，也不会接收 `*ToFile` 日志。

## Demos

| Demo | Purpose |
| --- | --- |
| `ConsoleLogDemo` | 验证传统静态 `Logger.*`、`*ToFile`、文件轮转和控制台用户输出。 |
| `AvaloniaLogDemo` | 验证传统静态 `Logger.*` 接入 Avalonia `LogView` 和通知。 |
| `MicrosoftLoggingAvaloniaDemo` | 验证 `ILogger<T>`、DI、`AddCodeWF()`、`LogUser*`、Avalonia `LogView` 和通知。 |
| `MicrosoftLoggingWebApiDemo` | 验证 .NET Web API 中的 `builder.Logging.AddCodeWF()`、普通诊断日志、用户日志、Scope 和 Activity。 |
