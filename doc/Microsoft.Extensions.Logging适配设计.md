# Microsoft.Extensions.Logging 适配设计

## 1. 文档状态

- 状态：待评审
- 目标版本：CodeWF.Log 12.x 后续版本
- 适用项目：`CodeWF.Log.Core`、`CodeWF.LogViewer.Avalonia`、新增的 `CodeWF.Log.Extensions.Logging`
- 设计原则：保持现有静态日志 API 可用，通过标准 `ILogger<T>` 提供可插拔的日志后端能力

## 2. 设计结论

1. 新增独立包 `CodeWF.Log.Extensions.Logging`，实现 Microsoft.Extensions.Logging Provider。
2. 业务代码可以只依赖 `ILogger<T>`，通过启动配置选择 CodeWF、Serilog、NLog 或其他 Provider。
3. CodeWF Provider 可以独立承担文件、控制台、Avalonia 日志视图和通知，也可以与第三方 Provider 并行时只提供用户日志流。
4. `CodeWF.Log.Core` 不引用 Microsoft.Extensions.Logging、Avalonia、Serilog 或 NLog。
5. 新增 `Trace` 级别，但 NuGet 组件的默认最低级别仍为 `Info`；所有 Demo 显式启用 `Trace`。
6. 完整接收 Category、EventId、消息模板、结构化属性、Scope、Activity 上下文和 Exception，不在适配器入口提前拼接成不可恢复的字符串。
7. 文件日志默认记录技术上下文和完整异常；控制台、LogView 和通知默认只展示用户可读消息。
8. `LogNotifications` 只保留 `MinimumLevel`，不再提供 `MaximumLevel`。

## 3. 目标与非目标

### 3.1 目标

- 支持构造函数注入 `ILogger<T>`。
- 支持 Microsoft.Extensions.Logging 的标准级别和 Category 过滤配置。
- 支持标准 Scope、EventId、结构化日志参数和 Activity 跟踪上下文。
- 保持现有 `Logger.Info`、`Logger.Error`、`*ToFile` 等 API 的源代码兼容性。
- 保持“一条完整事件、不同输出端按职责展示”的日志语义。
- 允许 CodeWF Provider 与其他 Provider 同时注册，并避免重复写文件或控制台。
- 输出配置使用强类型选项，避免布尔参数路由和自定义字符串模板解析器。

### 3.2 非目标

- 不直接适配 `Serilog.ILogger`、`NLog.Logger` 等第三方专有 API。
- 不引用或解析 Serilog OutputTemplate、NLog Layout 等第三方配置。
- 不保证直接调用 `Serilog.Log.*` 的日志能够进入 CodeWF 用户日志流。
- 不在本次增加 JSON 日志格式；后续如有明确需求，应以独立 Formatter 实现。
- 不强制已有项目从 CodeWF 静态 API 迁移到 `ILogger<T>`。

## 4. 总体架构

```text
业务代码
   │
   └── ILogger<T>
          │
          ├── CodeWFLoggerProvider
          │      └── CodeWF.Log.Core
          │             ├── File
          │             ├── Console
          │             └── UserLogFeed
          │                    ├── LogView
          │                    └── LogNotifications
          │
          └── 可选的第三方 Provider
                 ├── Serilog
                 ├── NLog
                 └── 其他 ILoggerProvider
```

CodeWF 独立使用时，Provider 默认输出到文件、控制台和用户日志流。与第三方 Provider 并行时，可将 CodeWF Provider 配置为只输出到 `UserLogFeed`，由第三方 Provider 负责文件和控制台。

## 5. 日志级别

### 5.1 CodeWF 级别

为保持现有枚举数值不变，`Trace` 使用 `-1`：

```csharp
public enum LogType
{
    [Description("跟踪")] Trace = -1,
    [Description("调试")] Debug = 0,
    [Description("消息")] Info = 1,
    [Description("警告")] Warn = 2,
    [Description("错误")] Error = 3,
    [Description("严重错误")] Fatal = 4
}
```

需要同步增加：

```csharp
Logger.Trace(...)
Logger.TraceToFile(...)
```

所有依赖级别顺序的比较、颜色映射、Description、Demo 和文档都必须覆盖 `Trace`。

### 5.2 默认值

| 配置项 | 默认值 |
|---|---|
| `LoggerOptions.MinimumLevel` | `Info` |
| `Logger.MinimumLevel` | `Info` |
| `LogView.MinimumLevel` | `Info` |
| `LogView.MaximumLevel` | `Fatal` |
| `LogNotifications.MinimumLevel` | `Error` |

Demo 显式配置 Core 和 LogView 的最低级别为 `Trace`，用于展示完整能力。NuGet 组件不因为增加 Trace 而默认产生更多日志。

### 5.3 Microsoft 级别映射

| Microsoft `LogLevel` | CodeWF `LogType` |
|---|---|
| `Trace` | `Trace` |
| `Debug` | `Debug` |
| `Information` | `Info` |
| `Warning` | `Warn` |
| `Error` | `Error` |
| `Critical` | `Fatal` |
| `None` | 不记录 |

## 6. 核心事件模型

Microsoft.Extensions.Logging 提供的不只是消息和 Exception。Provider 应完整接收以下信息：

- CategoryName
- LogLevel
- EventId.Id 和 EventId.Name
- 格式化后的消息
- 原始消息模板 `{OriginalFormat}`
- State 中的结构化属性
- Exception
- 外部 Scope
- 通过标准 Scope 传递的 Activity TraceId、SpanId 等信息

Core 不引用 Microsoft 类型，使用自己的不可变模型：

```csharp
public readonly record struct LogEventId(int Id, string? Name);

public readonly record struct LogProperty(string Name, string? Value);

public sealed record LogContext
{
    public string? CategoryName { get; init; }

    public LogEventId? EventId { get; init; }

    public string? MessageTemplate { get; init; }

    public IReadOnlyList<LogProperty> Properties { get; init; } = [];

    public IReadOnlyList<string> Scopes { get; init; } = [];
}

public sealed record LogRecord
{
    public required LogType Level { get; init; }

    public required string Message { get; init; }

    public string? UserMessage { get; init; }

    public Exception? Exception { get; init; }

    public LogContext? Context { get; init; }

    public LogTargets Targets { get; init; } = LogTargets.Default;
}
```

适配器在调用线程中将 State、Properties 和 Scope 转换为不可变快照，避免后台日志线程处理时原始对象已经变化。

`Logger.Log(LogRecord record)` 作为适配器和后续扩展的统一入口。现有静态方法内部构造 `LogRecord`，公开调用方式不变。

## 7. 输出目标

使用强类型 Flags 枚举代替 `UserVisible` 等布尔路由：

```csharp
[Flags]
public enum LogTargets
{
    None = 0,
    File = 1,
    Console = 2,
    UserFeed = 4,
    Default = File | Console | UserFeed
}
```

现有 API 的固定语义：

| API | Targets |
|---|---|
| `Logger.Trace/Debug/Info/Warn/Error/Fatal` | `Default` |
| `Logger.TraceToFile/DebugToFile/...` | `File` |
| `Logger.Log(LogRecord)` | 使用 `LogRecord.Targets` |

通知不是独立 Target。`LogView` 和 `LogNotifications` 都是 `UserLogFeed` 的消费者，是否弹出通知由 Avalonia 通知配置决定。

## 8. Microsoft.Extensions.Logging 适配包

### 8.1 项目

```text
CodeWF.Log.Extensions.Logging
```

依赖：

- `CodeWF.Log.Core`
- `Microsoft.Extensions.Logging`

不依赖 Avalonia 或任何第三方日志实现。

### 8.2 关键类

```text
CodeWFLoggerProvider
CodeWFLogger
CodeWFLoggerOptions
CodeWFLoggingBuilderExtensions
```

`CodeWFLoggerProvider`：

- 实现 `ILoggerProvider` 和 `ISupportExternalScope`。
- 按 Category 创建并缓存 `CodeWFLogger`。
- Provider Dispose 不负责关闭全局 Core。

`CodeWFLogger`：

- 将 Microsoft LogLevel 映射为 LogType。
- 调用 Microsoft 提供的 Formatter 一次，得到用户消息。
- 提取 `{OriginalFormat}` 和结构化 Properties。
- 获取当前 Scope 快照。
- 构造 `LogRecord` 并写入 Core。
- 不把异常堆栈追加到用户消息。

### 8.3 Provider 配置

```csharp
public sealed record CodeWFLoggerOptions
{
    public LogTargets Targets { get; init; } = LogTargets.Default;

    public bool CaptureScopes { get; init; } = true;

    public bool CaptureProperties { get; init; } = true;

    public bool CaptureMessageTemplate { get; init; } = true;
}
```

不在 Provider 选项中重复定义最低日志级别。级别和 Category 过滤统一使用 Microsoft.Extensions.Logging 标准配置。

独立使用：

```csharp
builder.Logging.AddCodeWFLog(options =>
{
    options.Targets = LogTargets.Default;
});
```

与其他 Provider 并行：

```csharp
builder.Logging.AddCodeWFLog(options =>
{
    options.Targets = LogTargets.UserFeed;
});
```

## 9. Microsoft 标准配置支持范围

以下能力直接复用 Microsoft.Extensions.Logging：

- `ILogger<T>` 注入
- 默认最低级别
- 按 Category 设置最低级别
- Provider 专属过滤
- EventId
- 结构化 State
- Scope
- Activity 跟踪上下文
- 多 Provider 并行

示例：

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information",
      "MyCompany.Device": "Trace"
    }
  }
}
```

Activity 使用 Microsoft 标准配置，例如：

```csharp
services.Configure<LoggerFactoryOptions>(options =>
{
    options.ActivityTrackingOptions =
        ActivityTrackingOptions.TraceId |
        ActivityTrackingOptions.SpanId |
        ActivityTrackingOptions.ParentId;
});
```

Microsoft.Extensions.Logging 只规定事件模型和过滤机制，不规定所有 Provider 共用的输出布局。因此 CodeWF 不读取 Serilog OutputTemplate、NLog Layout 或 Microsoft ConsoleFormatter 配置。

## 10. 输出格式设计

### 10.1 原则

- File：技术诊断输出，默认包含完整上下文和 Exception 堆栈。
- Console：用户输出，默认只包含时间、级别和用户消息，不自动输出异常堆栈。
- UserLogFeed：安全用户数据，不携带 Exception、完整 Scope 或完整 Properties。
- LogView：消费 UserLogFeed，并决定界面展示范围和少量安全字段。

原生 CodeWF API 和 `ILogger<T>` 共用同一套 Core 输出格式，不能形成两套文件或控制台规则。

### 10.2 文件格式

在 `FileLogOptions` 中增加：

```csharp
public sealed record FileLogFormatOptions
{
    public bool IncludeCategoryName { get; init; } = true;

    public bool IncludeEventId { get; init; } = true;

    public bool IncludeMessageTemplate { get; init; }

    public bool IncludeProperties { get; init; } = true;

    public bool IncludeScopes { get; init; } = true;

    public bool SingleLine { get; init; }
}
```

只要事件包含 Exception，文件日志始终记录完整异常，不提供关闭堆栈的配置。

默认输出示例：

```text
2026-07-23 18:20:10.321 [错误] 任务文件加载失败，请检查文件格式后重新打开。
分类：S_ComponentC.TaskPersistenceService
事件：1002 LoadTaskFailed
属性：TaskName=task3, TaskPath=E:\TaskFolder\task3\task.xml
范围：RequestId=8b234...
System.InvalidOperationException: There is an error in XML document...
```

### 10.3 控制台格式

在 `LoggerOptions` 中增加：

```csharp
public sealed record ConsoleLogFormatOptions
{
    public string TimestampFormat { get; init; } = "yyyy-MM-dd HH:mm:ss.fff";

    public bool UseUtcTimestamp { get; init; }

    public bool IncludeCategoryName { get; init; }

    public bool IncludeEventId { get; init; }

    public bool IncludeScopes { get; init; }

    public bool IncludeProperties { get; init; }

    public bool SingleLine { get; init; } = true;
}
```

默认输出：

```text
2026-07-23 18:20:10.321 [错误] 任务文件加载失败，请检查文件格式后重新打开。
```

控制台可以配置显示 Category、EventId、Scope 和 Properties，但不自动输出 Exception 堆栈。

### 10.4 UserLogFeed 与 LogView

`UserLogEntry` 增加安全上下文：

```csharp
public sealed record UserLogEntry
{
    public required long Sequence { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public required LogType Level { get; init; }

    public required string Message { get; init; }

    public string? CategoryName { get; init; }

    public LogEventId? EventId { get; init; }
}
```

UserLogFeed 不保存 Exception、完整 Scope、完整 Properties 和 MessageTemplate，避免界面或通知意外暴露技术细节，并控制历史缓存占用。

LogView 增加可选显示属性：

```xml
<log:LogView MinimumLevel="Info"
             MaximumLevel="Fatal"
             ShowCategoryName="False"
             ShowEventId="False" />
```

LogView 保留 `MaximumLevel`，因为多个视图需要按区间展示，例如 Info 单级视图或 Warn 至 Fatal 视图。

## 11. LogNotifications 级别语义

通知是阈值语义：日志级别大于或等于 `MinimumLevel` 时提醒。因此只保留：

```xml
log:LogNotifications.MinimumLevel="Error"
```

判断规则：

```csharp
entry.Level >= minimumLevel
```

删除：

- `MaximumLevelProperty`
- `GetMaximumLevel` / `SetMaximumLevel`
- Presenter 中的最大级别字段和参数
- Demo 和调用端 XAML 中的 `MaximumLevel`

关闭通知继续使用：

```xml
log:LogNotifications.Mode="None"
```

不使用特殊日志级别表达“关闭通知”。

默认 `MinimumLevel=Error`，因此 Error 和 Fatal 会通知；配置为 Fatal 时只通知 Fatal；配置为 Warn 时通知 Warn、Error 和 Fatal。

## 12. 初始化、关闭和有效级别

Provider 不拥有全局 Core 生命周期：

1. 应用先调用 `Logger.Initialize(LoggerOptions)`。
2. 应用注册 `AddCodeWFLog(...)`。
3. 应用退出时调用并等待 `Logger.ShutdownAsync()`。

`ILoggerProvider.Dispose()` 只释放 Provider 自身资源，不能隐式关闭仍可能被静态 API 使用的 Core。

有效最低级别由两层共同决定：

1. Microsoft.Extensions.Logging 的级别和 Category 过滤。
2. `LoggerOptions.MinimumLevel`。

最终结果是两者中更严格的限制。使用 `ILogger<T>` 的 Demo 将 Core 最低级别设置为 Trace，让 Microsoft 配置成为主要过滤入口。

## 13. Demo 设计

### 13.1 MicrosoftLoggingAvaloniaDemo

新增独立 Avalonia Demo，只引用 CodeWF 和 Microsoft.Extensions.Logging，不引用 Serilog、NLog。

要求：

- 所有业务和界面日志只通过注入的 `ILogger<T>` 输出。
- Core 与 LogView 显式启用 Trace。
- 提供 Trace、Debug、Information、Warning、Error、Critical 按钮。
- 提供 EventId、结构化属性、Scope 和友好异常示例。
- 提供两个 LogView，验证不同级别区间。
- Error 和 Critical 验证桌面通知。
- 验证异常堆栈只出现在文件，不出现在控制台、LogView 和通知。
- 验证应用退出时日志 Flush 和 Shutdown。

### 13.2 MicrosoftLoggingWebApiDemo

新增独立 .NET Web API Demo，只使用 CodeWF Provider。

要求：

- 使用 `WebApplication.CreateBuilder(args)`。
- 清除默认 Provider 后注册 CodeWF Provider。
- Controller 或 Service 通过 `ILogger<T>` 输出日志。
- 使用 `appsettings.json` 展示默认级别和 Category 过滤。
- 展示 EventId、结构化参数、Scope、TraceId 和 SpanId。
- 提供 Trace、Information、Warning、Error 和 Exception 示例接口。
- Console 输出用户消息，File 输出完整技术上下文和异常。
- `RunAsync()` 的 `finally` 中等待 `Logger.ShutdownAsync()`。

### 13.3 现有 Demo

- `ConsoleLogDemo`：继续展示 CodeWF 原生静态 API，并增加 Trace。
- `AvaloniaLogDemo`：继续展示原生 LogView、默认通知和自定义通知样式，并增加 Trace。
- NuGet 组件默认仍为 Info，所有 Demo 显式改为 Trace。

## 14. 兼容性与迁移

### 14.1 保持兼容

- 保留所有现有 Logger 静态方法。
- 保留 LogView 的 MinimumLevel 和 MaximumLevel。
- 保留 LoggerOptions.MinimumLevel 默认 Info。
- 保留普通日志进入文件、控制台和 UserLogFeed 的语义。
- 保留 `*ToFile` 只进入文件的语义。

### 14.2 需要迁移

- Avalonia 调用端删除 `LogNotifications.MaximumLevel` XAML 配置。
- 依赖 LogType 整数范围的代码需要允许 Trace=-1。
- Core 内部 `UserVisible` 路由改为 LogTargets。
- UserLogEntry 扩展后，内部构造和展示代码需要同步更新。

## 15. 实施顺序

1. 增加 Trace，并更新所有级别比较、颜色和 Demo。
2. 引入 LogTargets、LogRecord 和 LogContext，重构 Core 内部事件管线。
3. 增加文件与控制台格式选项，验证现有输出语义不变。
4. 扩展 UserLogEntry 和 LogView 的可选安全上下文显示。
5. 简化 LogNotifications，只保留 MinimumLevel。
6. 新增 CodeWF.Log.Extensions.Logging Provider。
7. 新增 Avalonia 和 Web API 两个 Microsoft Logging Demo。
8. 更新 README、更新日志、包说明和示例配置。
9. 构建所有目标框架，并人工验证两个新 Demo。

## 16. 验收标准

- `ILogger<T>` 的 Trace 至 Critical 均正确映射。
- Microsoft Category 过滤和 Provider 过滤生效。
- EventId、模板、Properties、Scope 和 Activity 信息可进入文件日志。
- Exception 完整堆栈只进入文件日志。
- 控制台、LogView 和通知默认显示用户可读消息。
- LogView 默认 MinimumLevel 为 Info，Demo 可显式显示 Trace。
- LogNotifications 只使用 MinimumLevel，Fatal 不会被最大级别错误排除。
- CodeWF Provider 可配置为只写 UserLogFeed，与其他 Provider 并行时不重复写文件和控制台。
- 原有 Logger 静态 API 调用无需修改。
- 两个新 Demo 均不引用 Serilog、NLog 或其他第三方日志实现。
