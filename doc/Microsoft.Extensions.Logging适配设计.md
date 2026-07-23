# Microsoft.Extensions.Logging 适配设计

## 1. 定位

- 状态：设计讨论稿
- 目标版本：CodeWF.Log 全新版本
- 核心定位：CodeWF 是 `Microsoft.Extensions.Logging` 生态中的一个 Provider，业务代码优先使用标准 `ILogger<T>`。
- 设计原则：按 .NET 规范设计，约定大于配置，`AddCodeWF()` 零配置可用。
- 包名：`CodeWF.Log.Core`、`CodeWF.Log.Extensions.Logging`、`CodeWF.Log.Avalonia`。
- AOT 约束：`CodeWF.Log.Core` 和当前 Avalonia 包延续已有 AOT 支持；新增 `CodeWF.Log.Extensions.Logging` 不得破坏三个 NuGet 包的 trim/AOT 友好基线。

本文中的 Microsoft.Extensions.Logging 简称 MEL。

## 2. 核心结论

1. 新业务代码的主入口是 MEL 标准 `ILogger<T>`，不设计替代 MEL 的新业务日志 API。
2. CodeWF 的接入形态是 `builder.Logging.AddCodeWF()`，由 MEL `LoggerFactory` 负责 Provider 编排、级别过滤、Category 过滤和多 Provider 并行。
3. `CodeWF.Log.Core` 可以直接引用 `Microsoft.Extensions.Logging`，把 `LogLevel`、`EventId` 等 MEL 类型作为核心公共契约。
4. Core 不再定义 `LogType`，不保留旧枚举值，不提供 `Trace = -1`、`Info`、`Warn`、`Fatal` 等旧版兼容别名。
5. 全局采集过滤交给 MEL 的 `Logging:LogLevel` 和 Provider 专属过滤；CodeWF 只做 Sink 级输出过滤。
6. CodeWF 的差异化能力是文件诊断日志、用户安全日志流 `UserLogFeed`、Avalonia `LogView` 和通知。
7. 普通 `ILogger` 日志默认是诊断日志，不默认进入 `UserLogFeed`。
8. 用户可见日志通过 `ILogger` 扩展方法表达，例如 `LogUserError(...)`。
9. Sink 失败、队列丢弃、Formatter 失败等内部问题进入 Self Diagnostics，不默认反向破坏业务流程。
10. 配置遵循 .NET Options、`appsettings.json`、Provider Alias、DI 生命周期和 Host 生命周期约定，不引入自定义配置 DSL。
11. `CodeWF.Log.Core` 保留旧式 `Logger.*` 静态门面作为迁移兼容层；`CodeWF.Log.Avalonia` 保留原 `<log:LogView />` 默认用法。

## 3. 包职责

### 3.1 CodeWF.Log.Core

职责：

- 定义核心事件模型、属性模型、Scope 模型和 `UserLogEntry`。
- 实现日志 Pipeline、后台队列、Flush、Shutdown 和 Self Diagnostics。
- 实现 FileSink、ConsoleSink、UserLogSink、UserLogFeed。
- 提供默认文本 Formatter 和 Sink 抽象。
- 保留 `Logger.Info/Warn/Error/Fatal`、`Logger.*ToFile`、`Logger.FlushAsync`、`Logger.ShutdownAsync` 等静态门面，内部转发到默认 pipeline。
- 保留 `Logger.Initialize(...)` 作为非 Host/旧项目迁移入口；新项目优先使用 `AddCodeWF()`。

依赖：

- 直接引用 `Microsoft.Extensions.Logging`。
- 可以使用 `LogLevel`、`EventId`、`ILogger`、`ILoggerFactory` 等 MEL 标准类型。
- 不依赖 Avalonia。
- 不依赖 Serilog、NLog、log4net。
- 必须保持 trim/AOT 友好。

### 3.2 CodeWF.Log.Extensions.Logging

职责：

- 实现 `CodeWFLoggerProvider` 和 `CodeWFLogger`。
- 提供 `ILoggingBuilder.AddCodeWF(...)`。
- 支持 `[ProviderAlias("CodeWF")]`、MEL 配置绑定和 Provider 专属过滤。
- 捕获 Category、EventId、消息模板、结构化 State、Scope、Activity 和 Exception。
- 提供 `ILogger` 用户日志扩展方法。
- 必须保持 trim/AOT 友好。

### 3.3 CodeWF.Log.Avalonia

职责：

- 提供 `LogView`。
- 提供 `LogNotifications`。
- 消费 `UserLogFeed`，不直接消费完整诊断事件。
- 不读取 Exception、完整 Scope、完整 Properties 和 MessageTemplate。
- 延续当前 Avalonia 包的 AOT 支持。
- 保留 `<log:LogView />` 默认消费全局 `UserLogFeed` 的用法；需要多日志源时再显式设置 `Source`。

## 4. 默认约定

最小接入：

```csharp
builder.Logging.AddCodeWF();
```

默认行为：

| 能力 | 默认约定 |
|---|---|
| 全局级别过滤 | 使用 MEL `Logging:LogLevel` |
| Category 过滤 | 使用 MEL Category 规则 |
| Provider 专属过滤 | 使用 `Logging:CodeWF:LogLevel` |
| 文件日志 | 启用 |
| 文件目录 | Host 场景为 `IHostEnvironment.ContentRootPath/logs`；非 Host 场景为 `AppContext.BaseDirectory/logs` |
| 控制台输出 | 默认关闭，避免和 `AddConsole()` 重复 |
| UserLogFeed | 启用 |
| UserLog 模式 | `ExplicitOnly` |
| Scope 捕获 | 启用 |
| Activity 捕获 | 启用 |
| 结构化属性捕获 | 启用 |
| 消息模板捕获 | 启用 |
| Flush/Shutdown | 跟随 Host / LoggerFactory 生命周期 |

控制台日志遵循 .NET 习惯：应用需要控制台输出时，可以继续使用 Microsoft Console Provider；只有希望 CodeWF 接管控制台格式时，才显式启用 CodeWF ConsoleSink。

## 5. 级别模型

全新版本统一使用 MEL 标准级别：

```csharp
Microsoft.Extensions.Logging.LogLevel
```

| MEL `LogLevel` | UI 默认显示 |
|---|---|
| `Trace` | 跟踪 |
| `Debug` | 调试 |
| `Information` | 消息 |
| `Warning` | 警告 |
| `Error` | 错误 |
| `Critical` | 严重错误 |
| `None` | 不记录 |

CodeWF 内部、配置、Provider 和 Avalonia 控件的公开级别属性都使用 `LogLevel`。UI 的中文文字、颜色、图标只是显示层映射，不改变核心级别类型。
迁移旧项目时，本地配置模型中的日志级别属性也改为 `LogLevel`，配置值使用 `Information`、`Warning`、`Critical` 等 MEL 名称。

## 6. 过滤模型

全局采集过滤完全交给 MEL：

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "MyCompany.Device": "Trace"
    }
  }
}
```

CodeWF 不再定义第二套 `LoggerOptions.MinimumLevel`，避免出现 `appsettings.json` 配了 `Trace` 但 Core 默认值又拦掉日志的双重过滤问题。

Provider 专属过滤使用标准 MEL 约定：

```json
{
  "Logging": {
    "CodeWF": {
      "LogLevel": {
        "Default": "Trace",
        "Microsoft": "Warning"
      }
    }
  }
}
```

CodeWF Sink 可以有自己的最低输出级别，但它只决定事件是否进入某个输出端，不负责全局采集。

## 7. 核心事件模型

`CodeWFLogEvent` 是完整诊断事件，是所有 Sink 的事实源。

```csharp
public sealed record CodeWFLogEvent
{
    public required long Sequence { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required LogLevel Level { get; init; }
    public required string CategoryName { get; init; }
    public EventId EventId { get; init; }
    public string? MessageTemplate { get; init; }
    public required string Message { get; init; }
    public UserLogPayload? UserLog { get; init; }
    public Exception? Exception { get; init; }
    public IReadOnlyList<LogProperty> Properties { get; init; } = [];
    public IReadOnlyList<LogScope> Scopes { get; init; } = [];
    public string? TraceId { get; init; }
    public string? SpanId { get; init; }
    public string? ParentId { get; init; }
    public string? TraceState { get; init; }
}

public sealed record UserLogPayload
{
    public required string Message { get; init; }
    public IReadOnlyList<LogProperty> Properties { get; init; } = [];
}

public sealed record LogScope(
    string? Text,
    IReadOnlyList<LogProperty> Properties);

public sealed record LogProperty(
    string Name,
    LogValue Value,
    LogPropertyVisibility Visibility = LogPropertyVisibility.Diagnostic);

public enum LogPropertyVisibility
{
    Diagnostic,
    UserSafe
}

public abstract record LogValue;
public sealed record ScalarLogValue(object? Value) : LogValue;
public sealed record SequenceLogValue(IReadOnlyList<LogValue> Values) : LogValue;
public sealed record StructureLogValue(string? TypeName, IReadOnlyList<LogProperty> Properties) : LogValue;
```

设计要点：

- `Message` 是 MEL formatter 生成的诊断消息。
- `MessageTemplate` 来自 `{OriginalFormat}`。
- `Properties` 保留结构化值，不提前压成字符串。
- `Scopes` 同时保留文本和结构化属性。
- `UserLog` 是用户安全日志投影的输入，不等同于诊断消息。
- `Exception` 默认只进入诊断 Sink，不进入 `UserLogFeed`。

## 8. MEL Provider 行为

`CodeWFLoggerProvider`：

- 实现 `ILoggerProvider` 和 `ISupportExternalScope`。
- 标注 `[ProviderAlias("CodeWF")]`。
- 按 Category 创建并缓存 `CodeWFLogger`。
- 使用共享 `CodeWFLogPipeline`。
- Dispose 时释放自身资源，并按拥有关系触发 pipeline Flush/Shutdown。

`CodeWFLogger`：

- `IsEnabled(LogLevel.None)` 返回 `false`。
- 对已禁用级别尽早返回，减少分配。
- 在 `Log<TState>()` 中调用 MEL formatter 一次，得到 `Message`。
- 即使 `Message` 为空，只要存在 Exception、EventId、Properties 或 UserLog，也允许记录。
- 从 State 提取 `{OriginalFormat}` 和结构化属性。
- 支持 `LoggerMessage` / 源生成日志产生的 State 形状，不依赖 `FormattedLogValues` 等具体实现类型。
- State、Scope 和属性必须在调用线程转换为不可变快照，不把可变对象引用交给后台队列。
- 从 `IExternalScopeProvider` 捕获 Scope 快照。
- 从 `Activity.Current` 捕获 TraceId、SpanId、ParentId、TraceState。
- 构造不可变 `CodeWFLogEvent` 并写入 pipeline。
- 不把 Exception 堆栈拼接到 `Message`。

## 9. 用户日志语义

普通 MEL 日志默认是诊断日志：

```csharp
logger.LogError(ex, "Failed to parse task file {TaskPath}", taskPath);
```

默认结果：

- FileSink：记录诊断消息、属性、Scope、Activity 和 Exception。
- ConsoleSink：如果启用，按 Console 配置输出。
- UserLogFeed：不记录。
- LogNotifications：不通知。

用户可见日志通过 CodeWF 的 `ILogger` 扩展方法表达：

```csharp
logger.LogUserError(
    exception: ex,
    userMessage: "任务文件加载失败，请检查文件格式后重新打开。",
    messageTemplate: "Failed to parse task file {TaskPath}",
    taskPath);
```

默认结果：

- FileSink：记录完整诊断事件。
- UserLogFeed：记录 `userMessage`。
- LogView：显示 `userMessage`。
- LogNotifications：按通知级别规则提醒。

UserLog 模式：

```csharp
public enum UserLogMode
{
    Disabled,
    ExplicitOnly,
    FormattedMessage
}
```

| 模式 | 行为 |
|---|---|
| `Disabled` | 不生成 `UserLogEntry` |
| `ExplicitOnly` | 只有带 `UserLogPayload` 的事件进入 `UserLogFeed` |
| `FormattedMessage` | 没有 `UserLogPayload` 时使用诊断 `Message` 作为用户消息 |

默认值为 `ExplicitOnly`。Demo 或小工具可以显式启用 `FormattedMessage`，生产应用不应默认把技术日志投影给 UI。

旧式 `Logger.*` 静态门面的兼容语义：

- `Logger.Info/Warn/Error/Fatal(...)`：写入诊断 Sink，并生成 `UserLogPayload` 进入 `UserLogFeed`。
- `Logger.Error(message, exception, userMessage)`：诊断 Sink 使用 `message` 和 `exception`，`UserLogFeed` 使用 `userMessage`。
- `Logger.*ToFile(...)`：只进入诊断 Sink，不进入 `UserLogFeed`。
- `Logger.MinimumLevel` 不再使用 `LogType`，迁移为 MEL `LogLevel`。

## 10. UserLogEntry

`UserLogEntry` 是安全投影，不是完整日志事件。

```csharp
public sealed record UserLogEntry
{
    public required long Sequence { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required LogLevel Level { get; init; }
    public required string Message { get; init; }
    public string? CategoryName { get; init; }
    public EventId EventId { get; init; }
    public string? TraceId { get; init; }
    public IReadOnlyList<LogProperty> Properties { get; init; } = [];
}
```

安全边界：

- 不包含 Exception。
- 不包含完整 Scope。
- 不包含完整 Properties。
- 不包含 MessageTemplate。
- 只允许复制标记为 `UserSafe` 的属性。
- 超长消息按配置截断。

## 11. Pipeline 与 Sink

```csharp
public interface ILogPipeline : IAsyncDisposable
{
    void Write(CodeWFLogEvent logEvent);
    Task FlushAsync(CancellationToken cancellationToken = default);
    Task ShutdownAsync(CancellationToken cancellationToken = default);
}
```

`Write(...)` 是同步入口，不直接执行慢 IO。慢 IO 由后台队列和 Sink 完成。

队列满策略：

```csharp
public enum LogQueueFullMode
{
    Wait,
    DropNewest,
    DropTraceAndDebug
}
```

所有丢弃行为都进入 Self Diagnostics，并统计丢弃数量。

Sink 抽象：

```csharp
public interface ILogSink : IAsyncDisposable
{
    bool IsEnabled(CodeWFLogEvent logEvent);
    ValueTask EmitAsync(CodeWFLogEvent logEvent, CancellationToken cancellationToken);
    Task FlushAsync(CancellationToken cancellationToken);
}
```

FileSink：

- 默认启用。
- 默认记录 Timestamp、Level、Category、EventId、Message、MessageTemplate、Properties、Scopes、Activity 和完整 Exception。
- 相对路径在 Host 场景下相对 `IHostEnvironment.ContentRootPath`，非 Host 场景下相对 `AppContext.BaseDirectory`。

ConsoleSink：

- 默认关闭。
- 启用后默认输出 Timestamp、Level、Message。
- 默认不输出完整 Exception 堆栈。

UserLogSink：

- 默认启用。
- 默认 `ExplicitOnly`。
- 负责把 `CodeWFLogEvent.UserLog` 投影成 `UserLogEntry`。

## 12. Avalonia

`LogView` 消费 `UserLogFeed`：

```xml
<log:LogView Source="{Binding UserLogs}"
             MinimumLevel="Information"
             MaximumLevel="Critical"
             ShowCategoryName="False"
             ShowEventId="False"
             ShowTraceId="False" />
```

设计约定：

- `Source` 可显式绑定 `UserLogFeed`。
- 未设置 `Source` 时可回退到默认 `UserLogFeed`。
- `MinimumLevel` 和 `MaximumLevel` 使用 MEL `LogLevel`。
- 保留 `MaximumLevel`，支持多个 LogView 按区间展示。

`LogNotifications` 是阈值语义，只保留 `MinimumLevel`：

```xml
log:LogNotifications.MinimumLevel="Error"
```

判断规则：

```csharp
entry.Level >= minimumLevel
```

关闭通知使用：

```xml
log:LogNotifications.Mode="None"
```

不提供 `MaximumLevel`，不使用特殊日志级别表达“关闭通知”。

## 13. 配置

最小配置：

```csharp
builder.Logging.AddCodeWF();
```

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  }
}
```

常见配置：

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "MyCompany.Device": "Trace"
    },
    "CodeWF": {
      "File": {
        "DirectoryPath": "logs"
      }
    }
  }
}
```

与 Serilog/NLog/log4net 并行时，如果第三方 Provider 已负责文件和控制台，CodeWF 只保留 `UserLogFeed`：

```csharp
builder.Logging.AddSerilog();
builder.Logging.AddCodeWF(options =>
{
    options.File.Enabled = false;
    options.Console.Enabled = false;
    options.UserLog.Enabled = true;
});
```

只有明确希望 CodeWF 成为唯一 Provider 时，才清空默认 Provider：

```csharp
builder.Logging.ClearProviders();
builder.Logging.AddCodeWF(options =>
{
    options.Console.Enabled = true;
});
```

实现要求：

- Options 使用普通公开属性，支持标准配置绑定。
- Options 必须有显式校验，目录、容量、时间间隔、级别、模式等非法值应在启动阶段给出清晰错误。
- Host 场景优先使用 `IValidateOptions<TOptions>` / `ValidateOnStart`。
- AOT 场景不得依赖复杂反射绑定；必要时提供代码配置、手写绑定或 source-generated binding 路径。

## 14. ILogger 用户日志扩展

建议提供：

```csharp
public static class CodeWFLoggerExtensions
{
    public static void LogUserInformation(
        this ILogger logger,
        string userMessage,
        string messageTemplate,
        params object?[] args);

    public static void LogUserWarning(
        this ILogger logger,
        string userMessage,
        string messageTemplate,
        params object?[] args);

    public static void LogUserError(
        this ILogger logger,
        Exception? exception,
        string userMessage,
        string messageTemplate,
        params object?[] args);

    public static void LogUserCritical(
        this ILogger logger,
        Exception? exception,
        string userMessage,
        string messageTemplate,
        params object?[] args);
}
```

要求：

- 扩展方法必须走标准 `ILogger.Log(...)`。
- 扩展方法不能绕过 MEL 过滤。
- 其他 Provider 至少能收到普通诊断日志。
- CodeWF Provider 额外识别用户消息并生成 `UserLogEntry`。
- 用户消息通过保留属性传递，建议使用 `CodeWF.UserMessage` 和 `CodeWF.UserProperty.*`。
- 保留属性默认不作为普通诊断属性重复输出，避免污染文件日志和第三方 Provider 的结构化字段。
- 扩展方法必须保留原始 `messageTemplate`、参数顺序和 `{OriginalFormat}`，不能破坏 MEL 标准结构化日志语义。

## 15. AOT 与 trimming

三个包都必须 trim/AOT 友好：

- 不使用运行时程序集扫描发现 Sink、Formatter 或扩展方法。
- 不使用动态代码生成、表达式编译、`Reflection.Emit`。
- 不依赖按字符串解析类型名来创建对象。
- Options 类型使用公开无参构造和可设置属性。
- Provider、Sink、Formatter 通过显式 API 或 DI 注册。
- 确实需要反射时，必须通过 trim annotation、source generator 或手写绑定消除 AOT 风险。
- CI 保留或增加 Native AOT 示例应用，验证三个 NuGet 包没有不可接受的 trim/AOT 警告。

## 16. Demo

### 16.1 MicrosoftLoggingAvaloniaDemo

- 使用 `AddCodeWF()` 注册 Provider。
- UI、ViewModel、Service 全部通过注入的 `ILogger<T>` 输出。
- 展示 Trace、Debug、Information、Warning、Error、Critical。
- 展示 EventId、结构化属性、Scope、Activity。
- 展示普通 `LogError(...)` 只进文件、不进 LogView。
- 展示 `LogUserError(...)` 同时进文件、LogView 和通知。
- 验证退出时自动 Flush 和 Shutdown。

### 16.2 MicrosoftLoggingWebApiDemo

- 使用 `WebApplication.CreateBuilder(args)`。
- 使用 `builder.Logging.AddCodeWF()`。
- Controller/Service 通过 `ILogger<T>` 输出。
- 使用 `appsettings.json` 展示 MEL 全局过滤、Category 过滤和 CodeWF Provider 配置。
- 展示 EventId、结构化参数、Scope、TraceId、SpanId。
- 验证 Host 停止时自动 Flush。

### 16.3 MultiProviderDemo

- 同时注册 Serilog/NLog/log4net 中至少一个 Provider 与 CodeWF Provider。
- 第三方 Provider 负责文件或控制台。
- CodeWF 禁用 File/Console，只启用 UserLogFeed。
- 业务代码仍然只依赖 `ILogger<T>`。

## 17. 验收标准

- `ILogger<T>` 的 Trace、Debug、Information、Warning、Error、Critical 均能进入 CodeWF Provider。
- MEL 全局过滤、Category 过滤、Provider 专属过滤均生效。
- CodeWF Sink 级最低级别生效。
- EventId、`{OriginalFormat}`、结构化 State、Scope、Activity 和 Exception 可被捕获。
- `LoggerMessage` / 源生成日志的 State 可被正确捕获。
- 普通 `logger.LogError(...)` 默认不进入 UserLogFeed。
- `logger.LogUserError(...)` 默认进入 UserLogFeed。
- 用户日志扩展不破坏其他 Provider 对普通诊断日志的接收。
- UserLogEntry 不包含 Exception、完整 Scope、完整 Properties 和 MessageTemplate。
- LogView 只消费 UserLogFeed。
- LogNotifications 只使用 MinimumLevel。
- Provider 与 Serilog/NLog/log4net 并行时，业务代码无需修改。
- CodeWF 可配置为只启用 UserLogFeed，避免重复写文件和控制台。
- Host 停止或 LoggerFactory Dispose 时可以 Flush 和 Shutdown。
- Sink 失败不会默认抛回业务线程。
- 队列满策略可配置，丢弃数量进入 Self Diagnostics。
- Options 非法值在启动阶段给出清晰错误。
- `CodeWF.Log.Core`、`CodeWF.Log.Extensions.Logging`、`CodeWF.Log.Avalonia` 三个包通过 trim/AOT 验证。
