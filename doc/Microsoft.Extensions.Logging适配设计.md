# Microsoft.Extensions.Logging 适配设计

## 1. 定位

- 状态：设计基线，当前实现持续对齐中；实现状态见第 17 节
- 目标版本：CodeWF.Log 全新版本
- 最低运行时：.NET 10；三个 NuGet 包统一目标框架为 `net10.0`，更高版本 .NET 通过兼容资产使用，不再支持 .NET 8
- 核心定位：CodeWF 是 `Microsoft.Extensions.Logging` 生态中的一个 Provider，业务代码优先使用标准 `ILogger<T>`。
- 设计原则：按 .NET 规范设计，约定大于配置，`AddCodeWF()` 零配置可用。
- 包名：`CodeWF.Log.Core`、`CodeWF.Log.Extensions.Logging`、`CodeWF.Log.Avalonia`。
- AOT 约束：`CodeWF.Log.Core` 和当前 Avalonia 包延续已有 AOT 支持；新增 `CodeWF.Log.Extensions.Logging` 不得破坏三个 NuGet 包的 trim/AOT 友好基线。

本文中的 Microsoft.Extensions.Logging 简称 MEL。

## 2. 核心结论

1. 新业务代码的主入口是 MEL 标准 `ILogger<T>`，不设计替代 MEL 的新业务日志 API。
2. CodeWF 的接入形态是 `builder.Logging.AddCodeWF()`，由 MEL `LoggerFactory` 负责 Provider 编排、级别过滤、Category 过滤和多 Provider 并行。
3. `CodeWF.Log.Core` 可以直接引用 `Microsoft.Extensions.Logging`，把 `LogLevel`、`EventId` 等 MEL 类型作为核心公共契约。
4. Core 不再定义 `LogType`，不保留旧枚举值，也不为旧枚举提供 `Trace = -1`、`Info`、`Warn`、`Fatal` 等兼容别名；静态 `Logger.Info/Warn/Fatal` 方法作为迁移门面继续保留。
5. 全局采集过滤交给 MEL 的 `Logging:LogLevel` 和 Provider 专属过滤；CodeWF 只做 Sink 级输出过滤。
6. CodeWF 的差异化能力是统一结构化日志事件流 `LogEventFeed`、文件输出、Avalonia `LogView` 和通知。
7. 普通 `ILogger` 日志与 `LogUser*` 日志都生成同一种 `CodeWFLogEvent`；`LogUser*` 只额外提供可选的 `UserMessage`，不改变事件路由。
8. 用户可见日志通过 `ILogger` 扩展方法表达，例如 `LogUserError(...)`。
9. Sink 失败、队列丢弃、Formatter 失败等内部问题进入 Self Diagnostics，不默认反向破坏业务流程。
10. 配置遵循 .NET Options、`appsettings.json`、Provider Alias、DI 生命周期和 Host 生命周期约定，不引入自定义配置 DSL。
11. `CodeWF.Log.Core` 保留旧式 `Logger.*` 静态门面作为迁移兼容层；`CodeWF.Log.Avalonia` 保留原 `<log:LogView />` 默认用法。
12. MEL Provider 使用 DI 容器内的实例级 pipeline 和 `LogEventFeed`，不借用进程级静态 `LoggerHost`；旧式静态 `Logger.*` 使用独立的 legacy pipeline。
13. File 使用独立 `OutputTemplate`；Console、LogView 和 LogNotifications 严格共享同一个 `LineTemplate`，并对同一种 `CodeWFLogEvent` 使用相同占位符语义。所有字段显示均由模板决定，不提供 `ShowCategoryName`、`ShowEventId`、`ShowTraceId` 等字段布尔开关。
14. 删除 `UserLogEntry` 和公开的 `UserLogPayload`，不保留废弃类型或兼容别名；本次升级按 breaking change 处理。

## 3. 包职责

### 3.1 CodeWF.Log.Core

职责：

- 定义统一的核心事件模型、属性模型和 Scope 模型。
- 实现日志 Pipeline、后台队列、Flush、Shutdown 和 Self Diagnostics。
- 实现 FileSink、ConsoleSink 和 `LogEventFeed`。
- pipeline 必须可以按实例创建和释放，不能只存在于进程级静态 `Logger` 中。
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
- 通过 DI 持有独立的 pipeline 和 `LogEventFeed`，Provider Dispose 只释放自身拥有的实例。
- 支持 `[ProviderAlias("CodeWF")]`、MEL 配置绑定和 Provider 专属过滤。
- 捕获 Category、EventId、消息模板、结构化 State、Scope、Activity 和 Exception。
- 提供 `ILogger` 用户日志扩展方法。
- 必须保持 trim/AOT 友好。

### 3.3 CodeWF.Log.Avalonia

职责：

- 提供 `LogView`。
- 提供 `LogNotifications`。
- LogView 和 LogNotifications 都消费包含完整 `CodeWFLogEvent` 的 `LogEventFeed`。
- `LogContext.Source` 在 Avalonia `Application` 上提供全局 `LogEventFeed`，`LogView.Source` 只作为控件级覆盖；两者均未指定时回退到 legacy 全局事件源。
- 可以按共享 `LineTemplate` 读取 Exception、Scope、Properties、MessageTemplate 和 Activity 等完整事件字段。
- 延续当前 Avalonia 包的 AOT 支持。
- 保留 `<log:LogView />` 默认消费全局事件源的用法；需要多日志源时再显式设置 `Source`。

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
| 文件保留 | 单文件默认 1000 MB，滚动保留最近 30 天日志文件 |
| 控制台输出 | 默认关闭，避免和 `AddConsole()` 重复 |
| 行输出模板 | Console、LogView 和 LogNotifications 严格共享 `LineTemplate` |
| LogEventFeed | 启用；发布全部通过采集过滤的 `CodeWFLogEvent`，RecentCapacity 默认 2000 |
| LogView | `MinimumLevel=Information`，`MaximumLevel=Critical` |
| LogNotifications | `Mode=None`，`MinimumLevel=Error`，Duration 默认 10 秒 |
| Scope 捕获 | 启用 |
| Activity 捕获 | 启用 |
| Activity Tags/Baggage 捕获 | 默认关闭；涉及容量和敏感信息时显式开启 |
| 结构化属性捕获 | 启用 |
| 消息模板捕获 | 启用 |
| Flush/Shutdown | 跟随 Host / LoggerFactory 生命周期；DI Provider 和 legacy 静态 Logger 互不影响 |
| CodeWF pipeline 配置刷新 | 除共享 `LineTemplate` 外启动时固定；修改 File、Console、Queue、EventFeed 等配置后需重启应用 |

控制台日志遵循 .NET 习惯：应用需要控制台输出时，可以继续使用 Microsoft Console Provider；只有希望 CodeWF 接管控制台格式时，才显式启用 CodeWF ConsoleSink。

文件日志只通过 `File.OutputTemplate` 配置。Console、LogView 和 LogNotifications 使用同一个 `LineTemplate`，保证终端、界面和通知格式一致。CodeWF 不提供 `IncludeEventId`、`IncludeScopes`、`IncludeProperties`、`ShowCategoryName`、`ShowEventId`、`ShowTraceId` 等字段开关；模板中出现哪个占位符就输出哪个字段，未出现的字段不输出。

`LineTemplate` 默认只显示 Timestamp、Level 和 UserMessage；需要 Category、EventId、TraceId、Properties 或 Exception 时由调用方加入相应占位符。`UserMessage` 为空白时回退到 `Message`。为保证三种行输出严格一致，不提供 LogView 或 LogNotifications 的局部模板覆盖。

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

MEL Provider 场景不推荐再配置第二套全局采集级别。`CodeWFLoggerOptions.MinimumLevel` 只作为 CodeWF Provider 内部的保险阈值，默认 `Trace`，正常项目应优先使用 `Logging:LogLevel` 和 `Logging:CodeWF:LogLevel`。旧式静态 API 仍保留 `LoggerOptions.MinimumLevel`，它只服务于非 Host 场景的输出过滤。

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

`CodeWFLogEvent` 是 File、Console、LogView、LogNotifications 和事件 Feed 的唯一事实源，不再创建用户日志投影类型。

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
    public string? UserMessage { get; init; }
    public LogExceptionInfo? Exception { get; init; }
    public IReadOnlyList<LogProperty> Properties { get; init; } = [];
    public IReadOnlyList<LogScope> Scopes { get; init; } = [];
    public string? TraceId { get; init; }
    public string? SpanId { get; init; }
    public string? ParentId { get; init; }
    public string? TraceState { get; init; }
    public ActivityTraceFlags TraceFlags { get; init; }
    public IReadOnlyList<LogProperty> ActivityTags { get; init; } = [];
    public IReadOnlyList<LogProperty> ActivityBaggage { get; init; } = [];
}

public sealed record LogScope(
    string? Text,
    IReadOnlyList<LogProperty> Properties);

public sealed record LogProperty(string Name, LogValue Value);

public sealed record LogExceptionInfo
{
    public required string TypeName { get; init; }
    public required string Message { get; init; }
    public required string Text { get; init; }
    public string? StackTrace { get; init; }
    public string? Source { get; init; }
    public int HResult { get; init; }
    public IReadOnlyList<LogExceptionInfo> InnerExceptions { get; init; } = [];
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
- `Message` 永远是 MEL formatter 生成的诊断消息；`UserMessage` 是 CodeWF 特有的可选用户友好消息，两者是同一事件上的不同字段。
- `{Message}` 始终读取 `Message`；`{UserMessage}` 优先读取非空白的 `UserMessage`，没有显式用户消息时回退到 `Message`。该回退只属于模板格式化语义，不新增 `DisplayMessage` 字段。
- File、Console、LogView 和 LogNotifications 面对同一个事件及相同字段值；通道之间不再通过裁剪事件模型制造字段差异。
- Provider 在日志调用线程把原始 `Exception` 转换成不可变 `LogExceptionInfo`；事件、队列和 Feed 不长期持有原始异常对象及其可能引用的业务对象图。
- `LogExceptionInfo.Text` 保存原始 `Exception.ToString()` 的有界快照，`TypeName`、`Message`、`StackTrace`、`Source`、`HResult` 和 `InnerExceptions` 支持结构化查看；AggregateException 的多个内部异常必须保留顺序。
- 默认不捕获 `Exception.Data`。异常快照默认最大深度 8、最多 32 个内部异常、单字段最多 32 KiB、单个事件的异常快照总计最多 64 KiB；这些限制允许显式配置，截断和快照失败进入 Self Diagnostics。
- `Sequence` 由 pipeline 的单消费者按实际处理顺序分配，生产线程不能在入队前自行分配，避免并发入队造成序号与输出顺序不一致。
- 属性快照必须有最大深度、最大属性数、最大集合元素数和最大字符串长度，并检测循环引用；快照失败应降级并进入 Self Diagnostics，不能把异常抛回业务线程。

## 8. MEL Provider 行为

`CodeWFLoggerProvider`：

- 实现 `ILoggerProvider` 和 `ISupportExternalScope`。
- 标注 `[ProviderAlias("CodeWF")]`。
- 按 Category 创建并缓存 `CodeWFLogger`。
- 使用 DI 容器内共享的实例级 `CodeWFLogPipeline`，不调用静态 `Logger.TryInitialize()`，也不写入静态 `LoggerHost`。
- Dispose 时释放自身资源，并按拥有关系触发本实例 pipeline 的 Flush/Shutdown。
- 同一进程中的多个 Host、多个 `LoggerFactory` 和测试宿主互不抢占配置与生命周期。

`CodeWFLogger`：

- `IsEnabled(LogLevel.None)` 返回 `false`。
- 对已禁用级别尽早返回，减少分配。
- 在 `Log<TState>()` 中调用 MEL formatter 一次，得到 `Message`。
- 即使 `Message` 为空，只要存在 Exception、EventId、Properties 或 UserMessage，也允许记录。
- 从 State 提取 `{OriginalFormat}` 和结构化属性。
- 支持 `LoggerMessage` / 源生成日志产生的 State 形状，不依赖 `FormattedLogValues` 等具体实现类型。
- State、Scope 和属性必须在调用线程转换为不可变快照，不把可变对象引用交给后台队列。
- State、Scope、枚举器或 `ToString()` 发生异常时降级记录，并通过独立 Self Diagnostics 报告，不默认向业务调用方抛出。
- 从 `IExternalScopeProvider` 捕获 Scope 快照。
- 从 `Activity.Current` 捕获 TraceId、SpanId、ParentId、TraceState。
- TraceFlags 默认捕获；Activity Tags 和 Baggage 支持显式配置捕获，默认关闭，并受属性数量、字符串长度和嵌套深度限制。Baggage 可能包含账号、租户或 Token，启用前必须评估 LogView 和通知展示风险。
- 构造不可变 `CodeWFLogEvent` 并写入 pipeline。
- 不把 Exception 堆栈拼接到 `Message`。

## 9. Message 与 UserMessage 语义

普通 MEL 日志生成标准事件：

```csharp
logger.LogError(ex, "Failed to parse task file {TaskPath}", taskPath);
```

默认结果：

- FileSink：按 `File.OutputTemplate` 输出。
- ConsoleSink：启用且达到 Console 最低级别时，按共享 `LineTemplate` 输出。
- LogEventFeed：发布完整事件，LogView 按自身级别区间展示。
- LogNotifications：达到 `MinimumLevel` 时通知，不要求存在显式 `UserMessage`。

需要单独提供用户友好消息时，使用 CodeWF 的 `ILogger` 扩展方法：

```csharp
logger.LogUserError(
    exception: ex,
    userMessage: "任务文件加载失败，请检查文件格式后重新打开。",
    messageTemplate: "Failed to parse task file {TaskPath}",
    taskPath);
```

默认结果：

- 四个通道仍处理同一个完整事件。
- `{Message}` 输出诊断消息；`{UserMessage}` 输出显式用户消息。
- `UserMessage` 为 `null`、空字符串或纯空白时，`{UserMessage}` 回退到 `Message`。
- `LogUser*` 不改变事件是否进入 Console、LogView 或通知，只补充 `UserMessage`。

统一事件模型不再提供结构上的用户安全隔离。默认 `LineTemplate` 不展示 Exception、Properties、Scopes 等详细字段；调用方把这些占位符加入 `LineTemplate` 时，代表明确允许它们出现在 Console、LogView 和系统通知中。日志消息和属性仍可能包含路径、账号、Token 等敏感信息，组件不是自动脱敏器。

旧式 `Logger.*` 静态门面的兼容语义：

- `Logger.Info/Warn/Error/Fatal(...)`：生成统一 `CodeWFLogEvent`，进入 legacy pipeline 的 File、Console 和事件 Feed。
- `Logger.Error(message, exception, userMessage)`：同一事件的 `Message`、`Exception` 和 `UserMessage` 分别保存对应值。
- `Logger.*ToFile(...)`：通过 legacy pipeline 的内部路由信息只进入 FileSink；不为此创建另一种公开事件类型。
- `Logger.MinimumLevel` 不再使用 `LogType`，迁移为 MEL `LogLevel`。

## 10. LogEventFeed

`LogEventFeed` 发布 pipeline 已处理的完整 `CodeWFLogEvent`，供 LogView、LogNotifications 和应用代码订阅：

- Feed 与 File/Console 共享同一个不可变事件实例，不创建 `UserLogEntry` 或其他裁剪副本。
- Feed 包含普通 MEL 日志和具有 `UserMessage` 的日志，不提供 `ExplicitOnly` / `FormattedMessage` 模式。
- Feed 是后台 pipeline 的最终一致输出；调用日志 API 返回后，不保证对应事件已经可以从 Feed 中读取。
- Feed 的 recent buffer 默认保留最近 2000 条，容量满后按 `Sequence` 淘汰最旧事件；完整 Exception、Properties、Scopes 和 Activity 数据会增加内存占用。
- LogView 通过 Feed 的原子“订阅并重放”操作连接 Source：Feed 先建立订阅边界，再按 Sequence 交付 recent buffer 和后续事件，保证切换期间不遗漏、不重复且顺序稳定。
- LogNotifications 永远不重放 recent buffer，只处理通知启用并完成订阅后的新事件；Console 直接消费 pipeline，也不通过 Feed 重放。
- 订阅者回调不得在 pipeline 单消费者线程上执行慢操作；慢订阅者不能阻塞文件写入并最终反向阻塞业务线程。
- pipeline 创建的 `LogEventFeed` 关联同一实例级 `ILineTemplateController`，使 LogView 可从 Source 解析模板状态；模板更新仍只通过 Controller API 完成。

## 11. Pipeline 与 Sink

```csharp
public interface ILogPipeline : IAsyncDisposable
{
    void Write(CodeWFLogEvent logEvent);
    Task FlushAsync(CancellationToken cancellationToken = default);
    Task ShutdownAsync(CancellationToken cancellationToken = default);
}
```

`Write(...)` 是同步入口，不直接执行慢 IO。慢 IO 由后台队列和 Sink 完成。只有显式选择 `Wait` 策略时，调用线程才允许因队列背压而等待；其他模式不得无限阻塞业务线程。

队列满策略：

```csharp
public enum LogQueueFullMode
{
    Wait,
    DropNewest,
    DropTraceAndDebug
}
```

命名约定：

- `Message`：MEL formatter 生成的诊断消息。
- `UserMessage`：同一 `CodeWFLogEvent` 上可选的 CodeWF 用户友好消息。

语义约定：

- `Wait`：等待队列出现空间，保证不主动丢弃；该模式可能放大慢磁盘或慢 Sink 对业务延迟的影响，只允许调用方显式启用。
- `DropNewest`：队列满时丢弃当前事件，优先保护业务线程。
- `DropTraceAndDebug`：当前事件为 Trace/Debug 时直接丢弃；Information 及以上事件最多等待 `EnqueueTimeout`，超时后丢弃。该模式用于抵御低级别日志突发，同时避免 Sink 长期故障时无限阻塞业务线程。
- 默认使用 `DropTraceAndDebug`，`EnqueueTimeout` 默认 100 ms；`Wait` 模式不使用该超时。
- 所有丢弃行为都通过独立于日志队列的健康状态统计总数和各级别数量；Self Diagnostics 不能再次写入同一日志队列。
- `Sequence` 在单消费者取出事件时分配，表示实际处理顺序。
- `FlushAsync` 是队列屏障：保证调用前已经成功入队的事件完成 Sink Flush；与 Flush 并发且尚未入队的事件不在保证范围内。
- Shutdown 开始后拒绝新事件；并发到达但未成功入队的事件允许丢弃，并计入关闭期丢弃统计。

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
- 配置 `OutputTemplate` 后按模板输出；模板未包含的字段不输出。
- 相对路径在 Host 场景下相对 `IHostEnvironment.ContentRootPath`，非 Host 场景下相对 `AppContext.BaseDirectory`。
- 默认单文件上限为 1000 MB，按日期和大小滚动；`RetentionDays` 默认为 30，自动清理超过 30 天的日志文件。默认不设置目录总容量和文件数量上限，30 天内的文件不会仅因总容量或数量被提前删除。
- `RetainedFileCountLimit` 和 `MaxDirectorySizeBytes` 默认为空；调用方显式配置后，它们作为额外空间保护，可以提前删除 30 天内最旧的滚动文件。清理失败进入 Self Diagnostics，不中断当前日志写入。
- 明确单进程写入约束。若允许多进程共享目录，文件名必须包含进程标识并避免滚动竞争；第一版可以声明同一日志文件只支持单进程写入。
- 默认目录不可创建或不可写时，启动阶段应给出明确的 Options/初始化错误；运行期故障则降级到 Self Diagnostics，不反向破坏业务流程。

ConsoleSink：

- 默认关闭。
- 直接消费全部通过 MEL 采集过滤和 Console Sink 级过滤的 `CodeWFLogEvent`。
- 与 LogView、LogNotifications 严格使用同一个全局 `LineTemplate` 和同一套占位符语义。
- 不单独提供 `Console.OutputTemplate`；模板包含 `{Exception}`、`{Properties}`、`{Scopes}` 等字段时，Console 与 Avalonia 输出都能读取同一事件中的对应值。
- 希望 CodeWF 完全接管控制台时，由应用显式 `ClearProviders()` 后只注册 CodeWF；Provider 不擅自移除 Microsoft Console、Serilog 等其他 Provider。

EventFeedSink：

- 默认启用。
- 把全部通过采集过滤的完整 `CodeWFLogEvent` 发布到实例级 `LogEventFeed`。
- 不复制或裁剪事件；recent buffer 保存同一个不可变事件实例。

## 12. Avalonia

`LogView` 消费 `LogEventFeed`：

```xml
<log:LogView Source="{Binding LogEvents}"
             MinimumLevel="Information"
             MaximumLevel="Critical" />
```

设计约定：

- `LogContext.Source` 是设置在 Avalonia `Application` 上的全局 `LogEventFeed`；普通单 Host 应用只需配置一次，LogView 和 Notification 自动共享。
- `LogView.Source` 是可选的控件级覆盖，仅用于同一应用需要展示多个日志源的场景。
- LogView 的数据源解析顺序为：控件局部 `LogView.Source` → Application 全局 `LogContext.Source` → legacy 全局事件源。
- LogNotifications 的数据源解析顺序为：Application 全局 `LogContext.Source` → legacy 全局事件源。
- MEL/DI Avalonia 应用在 Application 初始化时把容器中的实例级 `LogEventFeed` 设置为全局 Source；原 `<log:LogView />` 无 Source 用法仍可在 legacy 静态 Logger 场景工作。
- `MinimumLevel` 和 `MaximumLevel` 使用 MEL `LogLevel`。
- LogView 默认 `MinimumLevel=Information`、`MaximumLevel=Critical`；保留 `MaximumLevel`，支持多个 LogView 按区间展示。
- 每行字段完全由 `LineTemplate` 决定，不提供 `ShowCategoryName`、`ShowEventId`、`ShowTraceId` 等字段开关。
- `LineTemplate` 支持完整 `CodeWFLogEvent` 的字段，包括 `Timestamp`、`Level`、`Message`、`UserMessage`、`MessageTemplate`、`Category`、`EventId`、`EventName`、`Properties`、`Scopes`、`Exception`、Activity/Trace 字段和 `NewLine`。
- “严格共享”以 pipeline/Source 为边界：Console 使用所属 pipeline 的 Controller；LogView 使用其最终解析 Source 关联的 Controller；Application 级 LogNotifications 只使用全局 Source 的 Controller，不跟随某个局部 LogView.Source。
- 同一 Source 下的 Console、所有 LogView 和 LogNotifications 严格读取同一个模板状态，不提供控件级或通知级模板覆盖；Source 未提供模板时使用 `{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:zh}] {UserMessage}{NewLine}`。

MEL/DI Avalonia 应用的全局 Source 示例：

```csharp
var logEvents = services.GetRequiredService<LogEventFeed>();
LogContext.SetSource(this, logEvents);
```

`LogNotifications` 是阈值语义，只保留 `MinimumLevel`：

```xml
log:LogNotifications.MinimumLevel="Error"
log:LogNotifications.MaxVisibleCount="3"
log:LogNotifications.QueueCapacity="100"
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

LogNotifications 只按 `MinimumLevel` 判断，不检查是否存在显式 `UserMessage`。通知读取 Source 携带的全局 `LineTemplate`；标题、图标、颜色和按钮仍由通知样式决定，`LineTemplate` 只负责可变日志正文。

通知突发处理：

- `MinimumLevel` 是启用通知时唯一的事件资格条件；不再增加 `UserMessage`、Category、Exception 等隐式过滤。
- 达到级别的事件按 `Sequence` 顺序提交到有界通知展示队列；队列只影响呈现节奏，不影响事件进入 `LogEventFeed` 和 LogView。
- `InApp` 默认最多同时显示 3 条通知，其余事件排队；最大可见数量允许显式配置。
- `DesktopWindow` 每个 Avalonia Application 只复用一个桌面工作区右下角窗口，在同一窗口内按顺序滚动展示，不为每个事件创建独立窗口。
- 通知展示队列默认容量为 100。容量耗尽时不阻塞 pipeline，不继续创建窗口；累计溢出数量并在现有通知区域显示“另有 N 条日志”。通知队列溢出不删除 `LogEventFeed` 中的事件，Feed 仍按自身 recent buffer 容量独立保留。
- `Mode=None` 时不建立展示积压；重新启用通知后只处理新事件，不补弹关闭期间的历史事件。

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
      "LineTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {UserMessage}{NewLine}",
      "File": {
        "Enabled": true,
        "DirectoryPath": "logs",
        "MaxFileSizeBytes": 1048576000,
        "RetentionDays": 30,
        "OutputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] ({Category}) {Message} {Properties}{NewLine}{Exception}"
      },
      "Console": {
        "Enabled": false
      },
      "EventFeed": {
        "Enabled": true,
        "RecentCapacity": 2000
      },
      "Capture": {
        "Scopes": true,
        "Activity": true,
        "ActivityTags": false,
        "ActivityBaggage": false
      },
      "Queue": {
        "Capacity": 10000,
        "FullMode": "DropTraceAndDebug",
        "EnqueueTimeout": "00:00:00.100"
      }
    }
  }
}
```

代码配置：

```csharp
builder.Logging.AddCodeWF(options =>
{
    options.LineTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {UserMessage}{NewLine}";
    options.File.Enabled = true;
    options.File.DirectoryPath = "logs";
    options.File.MaxFileSizeBytes = 1_000L * 1024 * 1024;
    options.File.RetentionDays = 30;
    options.File.OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] ({Category}) {Message} {Properties}{NewLine}{Exception}";

    options.Console.Enabled = false;
    options.EventFeed.Enabled = true;
    options.EventFeed.RecentCapacity = 2_000;
    options.Capture.Scopes = true;
    options.Capture.Activity = true;
    options.Capture.ActivityTags = false;
    options.Capture.ActivityBaggage = false;
    options.Queue.Capacity = 10_000;
    options.Queue.FullMode = LogQueueFullMode.DropTraceAndDebug;
    options.Queue.EnqueueTimeout = TimeSpan.FromMilliseconds(100);
});
```

正式 API 同时支持 `appsettings.json` 和 `AddCodeWF(options => ...)` 代码配置，并遵循 .NET Options 的正常合并顺序。MicrosoftLoggingWebApiDemo 为了集中验证配置绑定，故意只使用 `appsettings.json`；这不表示产品 API 不支持代码配置。

`File.OutputTemplate` 与共享 `LineTemplate` 使用同一套占位符：

| 占位符 | 含义 |
|---|---|
| `Timestamp` | 时间，支持 `{Timestamp:yyyy-MM-dd HH:mm:ss.fff}` |
| `Level` | MEL 日志级别，支持 `{Level:u3}`、`{Level:u4}`、`{Level:zh}` |
| `Category` / `CategoryName` | 日志 Category |
| `EventId` / `EventName` | MEL EventId |
| `Message` | MEL formatter 生成的消息 |
| `MessageTemplate` | `{OriginalFormat}` 原始模板 |
| `UserMessage` | CodeWF 用户消息；为空白时回退到 `Message` |
| `Properties` | 结构化属性 |
| `Scopes` | Scope 快照 |
| `Activity` / `TraceId` / `SpanId` | Activity/Trace 信息 |
| `TraceFlags` / `ActivityTags` / `ActivityBaggage` | Activity 采样标记、Tags 和 Baggage；后两项需显式启用捕获 |
| `Exception` | 异常详情 |
| `NewLine` | 当前平台换行 |

模板中未出现的字段不会输出，因此不再设计字段布尔配置。`File.OutputTemplate` 和共享 `LineTemplate` 都可以使用完整 `CodeWFLogEvent` 的全部占位符；两者的区别只是应用到 File 还是 Console、LogView、LogNotifications。

与 Serilog/NLog/log4net 并行时，如果第三方 Provider 已负责文件和控制台，CodeWF 只保留 `LogEventFeed`、LogView 和 LogNotifications：

```csharp
builder.Logging.AddSerilog();
builder.Logging.AddCodeWF(options =>
{
    options.File.Enabled = false;
    options.Console.Enabled = false;
    options.EventFeed.Enabled = true;
});
```

只有明确希望 CodeWF 成为唯一 Provider 时，才清空默认 Provider：

```csharp
builder.Logging.ClearProviders();
builder.Logging.AddCodeWF(options =>
{
    options.Console.Enabled = true;
    options.LineTemplate =
        "{Timestamp:HH:mm:ss} [{Level:u3}] ({Category}) {UserMessage}{NewLine}";
});
```

实现要求：

- Options 使用普通公开属性，支持标准配置绑定。
- Options 必须有显式校验，目录、容量、时间间隔、级别、模式等非法值应在启动阶段给出清晰错误。
- Host 场景优先使用 `IValidateOptions<TOptions>` / `ValidateOnStart`。
- Host 场景通过 `IHostEnvironment.ContentRootPath` 解析相对日志目录；非 Host 场景才使用 `AppContext.BaseDirectory`。
- `Logging:LogLevel` 和 `Logging:CodeWF:LogLevel` 继续由 MEL 按标准方式动态刷新。
- File、Console、EventFeed、Capture 和 Queue 属于 pipeline 启动配置，第一版不做部分热更新；配置文件变化后需重启应用。不能使用 `IOptionsMonitor` 只更新其中少数字段而让其余字段静默保持旧值。
- 共享 `LineTemplate` 是唯一例外：模板必须先完成解析和校验，再通过共享模板状态原子替换；无效模板不得覆盖上一份有效值。LogView 重新渲染当前保留日志，已经写出的 Console 行和已经弹出的通知不追溯修改。
- 运行时修改通过实例级 `ILineTemplateController` 完成，不直接修改 Options，也不把更新方法塞入 `LogEventFeed`：

```csharp
public interface ILineTemplateController
{
    string Current { get; }
    bool TryUpdate(string template, out string? error);
}
```

- 每个 DI pipeline 和 legacy pipeline 各自持有一个 Controller；Console、该 pipeline 的 Feed、LogView 和 LogNotifications 共享它。多 Host 之间互不影响。
- 配置文件和代码 Options 只确定 Controller 的初始模板；第一版不监听配置文件变化做隐式模板热重载。Demo 的预设和手工编辑器通过注入的 Controller 显式更新。
- AOT 场景不得依赖复杂反射绑定；必要时提供代码配置、手写绑定或 source-generated binding 路径。

## 14. ILogger 用户日志扩展

建议提供：

```csharp
public static class CodeWFLoggerExtensions
{
    public static void LogUser(
        this ILogger logger,
        LogLevel level,
        string userMessage,
        string messageTemplate,
        params object?[] args);

    public static void LogUser(
        this ILogger logger,
        LogLevel level,
        EventId eventId,
        Exception? exception,
        string userMessage,
        string messageTemplate,
        params object?[] args);

    public static void LogUserTrace(
        this ILogger logger,
        string userMessage,
        string messageTemplate,
        params object?[] args);

    public static void LogUserDebug(
        this ILogger logger,
        string userMessage,
        string messageTemplate,
        params object?[] args);

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
- CodeWF Provider 通过内部 State marker/interface 识别用户消息，并写入同一 `CodeWFLogEvent.UserMessage`；不创建 `UserLogPayload` 或 `UserLogEntry`。
- 用户消息不能作为 `CodeWF.UserMessage` 等普通枚举属性暴露给其他 Provider。
- State 对其他 Provider 枚举时只返回标准诊断参数和 `{OriginalFormat}`；Serilog/NLog/log4net 不应被 CodeWF 私有用户元数据污染。
- 扩展方法必须保留原始 `messageTemplate`、参数顺序和 `{OriginalFormat}`，不能破坏 MEL 标准结构化日志语义。
- 提供 Trace、Debug、Information、Warning、Error、Critical 六个对称的便捷方法，同时保留接受 `LogLevel`、`EventId` 和 Exception 的通用 `LogUser(...)`。
- `LogUser*` 的诊断格式化结果必须与相同模板和参数的标准 MEL `LogTrace/Debug/Information/Warning/Error/Critical` 保持一致，包括对齐、格式说明符、转义花括号、集合、Culture 和参数数量异常行为。

## 15. AOT 与 trimming

三个 `net10.0` 包都必须 trim/AOT 友好：

- 不使用运行时程序集扫描发现 Sink、Formatter 或扩展方法。
- 不使用动态代码生成、表达式编译、`Reflection.Emit`。
- 不依赖按字符串解析类型名来创建对象。
- Options 类型使用公开无参构造和可设置属性。
- Provider、Sink、Formatter 通过显式 API 或 DI 注册。
- 确实需要反射时，必须通过 trim annotation、source generator 或手写绑定消除 AOT 风险。
- CI 分为两个 SDK 基线：三个 NuGet 库使用稳定版 .NET 10 SDK 完成 build/test/pack，所有 Demo 使用 .NET 11 SDK 完成 build/smoke；不使用一个根 `global.json` 强制两类项目共享同一 SDK。
- CI 增加 `net10.0` Native AOT 控制台 smoke app，并执行 Avalonia trim/publish smoke test，验证三个 NuGet 包没有不可接受的 trim/AOT 警告。

## 16. Demo

所有 Avalonia Demo 的共同要求：

- 日志消息模板保持固定，便于 MEL/Serilog 按模板聚合；设备号、序号、耗时、温度、TaskId、CorrelationId、异常类型和时间等结构化参数每次点击动态生成，避免反复输出完全相同的硬编码内容。
- 提供独立的 Trace、Debug、Information、Warning、Error、Critical 按钮，以及“全部级别”“普通诊断日志”“用户友好日志”“带异常”“EventId + Scope + Activity”“并发突发”等组合按钮。
- 页面显示本次动态上下文，方便用户对照 Serilog 文件、CodeWF LogView 和通知中的字段。
- 动态场景生成逻辑放入仅供 Demo 使用的共享项目或共享源码，避免多个 Avalonia Demo 复制同一批随机数据和按钮处理代码；该项目不参与 NuGet 打包。

### 16.1 MicrosoftLoggingAvaloniaDemo

- 使用 `AddCodeWF()` 注册 Provider。
- Demo 保持 `net11.0` / `net11.0-windows`，验证目标框架高于库目标框架时的正常引用和运行。
- UI、ViewModel、Service 全部通过注入的 `ILogger<T>` 输出。
- 从 DI 注入实例级 `LogEventFeed`，在 Avalonia Application 上设置一次全局 `LogContext.Source`；普通 LogView 不再重复设置 Source，另增加一个局部 Source 示例验证多日志源覆盖。
- 页面提供共享 `LineTemplate` 选择和编辑区。下拉框内置“简洁格式”和“上下文格式”两套预设；用户修改预设文本后，选择状态自动变为“自定义”。
- 简洁格式默认使用 `{Timestamp:HH:mm:ss} [{Level:zh}] {UserMessage}{NewLine}`；上下文格式用于演示 Category、EventId、TraceId、Properties、Exception 等字段。
- 模板采用“编辑、即时预览、应用”的交互：编辑过程只更新预览，点击应用并通过占位符及格式校验后才影响实际行输出；校验失败时保留上一份有效模板并显示明确错误。
- 模板编辑同时作用于 Console、LogView 和 LogNotifications 严格共享的 `LineTemplate`，不修改独立的 `File.OutputTemplate`，也不把 Demo 的运行时选择写回 `appsettings.json`。
- 应用新模板后，LogView 重新渲染当前保留的日志；已经写出的 Console 行和已经弹出的通知不追溯修改，后续使用 `LineTemplate` 的输出采用新模板。
- 编辑区同时展示支持的占位符和恢复预设按钮，避免用户必须查文档才能试用模板。
- 展示 Trace、Debug、Information、Warning、Error、Critical。
- 展示 EventId、结构化属性、Scope、Activity。
- 展示普通 `LogError(...)` 同时进入 File、启用的 Console、LogView，并在达到通知 `MinimumLevel` 时通知；默认 `{UserMessage}` 回退显示诊断 `Message`。
- 展示 `LogUserError(...)` 在同一事件上同时具有 `Message`、`UserMessage` 和 Exception，切换模板即可对照字段。
- 验证退出时自动 Flush 和 Shutdown。

### 16.2 MicrosoftLoggingWebApiDemo

- 使用 `WebApplication.CreateBuilder(args)`。
- 使用 `builder.Logging.AddCodeWF()`。
- CodeWF 的 File、Console、EventFeed、Capture 和 Queue 配置只写在 `appsettings.json`；Program 中不再用 `AddCodeWF(options => ...)` 重复覆盖，以真实验证配置绑定。
- 相对目录 `logs` 由 Provider 按 `builder.Environment.ContentRootPath` 解析，Demo 不在 Program 中手工拼接绝对路径。
- Controller/Service 通过 `ILogger<T>` 输出。
- 使用 `appsettings.json` 展示 MEL 全局过滤、Category 过滤和 CodeWF Provider 配置。
- 展示 Trace/Category/Provider 专属过滤、EventId、结构化参数、Scope、TraceId、SpanId 和 `LoggerMessage` 源生成日志。
- `/recent-logs` 明确说明 Feed 为最终一致且包含完整事件，只用于 Demo，生产接口不得无鉴权暴露日志。
- 验证 Host 停止时自动 Flush。

### 16.3 MultiProviderAvaloniaDemo

- 同时注册 Serilog Provider 与 CodeWF Provider，业务代码仍然只依赖 `ILogger<T>`。
- Serilog 负责诊断文件和控制台；CodeWF 的 File/Console 禁用，只启用 `LogEventFeed`、LogView 和 LogNotifications。
- 普通 `logger.LogError(...)` 同时进入 Serilog 和 CodeWF `LogEventFeed`；CodeWF LogView 展示完整事件，达到通知 `MinimumLevel` 时通知。
- `logger.LogUserError(...)` 在 CodeWF 事件中额外设置 `UserMessage`；Serilog 仍接收标准诊断 `Message`、结构化属性和 Exception。
- 验证 Serilog 不会收到 CodeWF 私有 UserMessage 元数据，只收到标准 MEL State 和 `{OriginalFormat}`。
- 页面提供通知模式选择：`None`、`InApp`、`DesktopWindow`，运行时切换并立即验证。
- `InApp` 在当前 Avalonia TopLevel 内弹出；`DesktopWindow` 使用独立 Avalonia 窗口显示在桌面工作区右下角。
- 并发突发场景验证 InApp 最多同时显示 3 条、DesktopWindow 复用单窗口、队列溢出计数可见，且通知溢出不额外删除 LogView 的 Feed 事件。
- 提供各日志级别、动态用户消息、普通诊断与用户日志对照、异常、Scope/Activity/EventId、源生成 `LoggerMessage` 和并发突发按钮。
- Serilog 和 CodeWF 的配置优先写入 `appsettings.json`，代码配置示例保留在文档或独立注释中。

## 17. 当前实现状态

本表用于区分当前代码和目标设计，避免把路线图误读为已发布能力。功能完成后应在同一提交中更新状态。

| 能力 | 当前状态 | 说明 |
|---|---|---|
| `AddCodeWF()`、Provider Alias、基础配置绑定 | 已实现 | 已能作为 MEL Provider 注册 |
| Trace 至 Critical、EventId、结构化 State、Scope、Activity、Exception 捕获 | 部分实现 | 当前仍持有原始 Exception；目标设计改为有界不可变 `LogExceptionInfo` 快照 |
| 统一 `CodeWFLogEvent` 与完整 `LogEventFeed` | 未实现 | 当前实现仍存在 `UserLogEntry`/用户日志投影，需要 breaking 重构 |
| File `OutputTemplate` | 已实现 | Sink 级过滤、保留策略待完善 |
| Console/LogView/LogNotifications 共享 `LineTemplate` | 未实现 | 当前 Console 仍有独立模板，Avalonia 使用固定格式 |
| 实例级 DI pipeline 与 legacy 静态 pipeline 分离 | 未实现 | 当前 Provider 仍复用进程级静态 `LoggerHost` |
| 多 Host、重复启动和并行 `LoggerFactory` 隔离 | 未实现 | 依赖实例级 pipeline 改造 |
| Queue FullMode、超时、丢弃计数、顺序保证 | 部分实现 | 当前只有有界 Wait 队列 |
| Options 启动校验与明确的重启生效语义 | 部分实现 | 当前存在部分热更新语义不一致 |
| Host ContentRoot 相对路径解析 | 未实现 | 当前仍基于 `AppContext.BaseDirectory`，Demo 手工指定绝对路径 |
| `LogContext.Source`、`LogView.Source`、共享 `LineTemplate` | 未实现 | 当前 Avalonia 组件仍固定使用全局 Feed 和固定行格式 |
| 实例级 `ILineTemplateController` 与原子模板更新 | 未实现 | Demo 和三个行输出通道需要共享同一 Controller |
| MultiProviderAvaloniaDemo | 未实现 | 使用 Serilog 验证第三方 Provider 不接收 CodeWF 私有用户元数据 |
| 自动化测试、trim、Native AOT CI | 未实现 | 当前只有 Demo 和 TrimmerRoot 配置 |

## 18. 当前版本发布门槛

以下为当前版本发布必须满足的验收标准：

- 三个包统一目标框架为 `net10.0`，使用稳定版 .NET 10 SDK 完成 Release build/test/pack；所有 Demo 使用 .NET 11 SDK 完成构建和 smoke test。
- MEL Provider 使用实例级 pipeline；多个 Host、多个 `LoggerFactory`、顺序重启和并行测试互不抢占配置与生命周期。
- legacy `Logger.*` 继续可用，但不与 MEL Provider 共用静态 Host；原 `<log:LogView />` 无 Source 用法仍可回退到 legacy Feed。
- `ILogger<T>` 的 Trace、Debug、Information、Warning、Error、Critical 均能进入 CodeWF Provider。
- MEL 全局过滤、Category 过滤、Provider 专属过滤和 CodeWF Sink 级过滤均生效。
- EventId、`{OriginalFormat}`、结构化 State、Scope、Activity 和 Exception 可被捕获。
- `LoggerMessage` / 源生成日志的 State 可被正确捕获。
- 普通 `logger.LogError(...)` 与 `logger.LogUserError(...)` 都生成统一 `CodeWFLogEvent` 并进入 `LogEventFeed`；后者只额外设置 `UserMessage`。
- `LogUser*` 与标准 MEL 日志的诊断消息和结构化属性语义一致，且不向其他 Provider 暴露 CodeWF 私有用户消息元数据。
- `{Message}` 始终输出诊断消息；`{UserMessage}` 优先输出用户消息并在其为空白时回退到 `Message`。
- Avalonia Application 可配置全局 `LogEventFeed`，LogView 支持局部 Source 覆盖；同一 pipeline/Source 的 Console、所有 LogView 和 LogNotifications 严格共享一个实例级 `ILineTemplateController`。
- LogView 默认 MinimumLevel=Information、MaximumLevel=Critical；LogNotifications 默认 Mode=None、MinimumLevel=Error、Duration=10 秒，启用后只按 MinimumLevel 判断。
- LogView 的 recent buffer 重放与实时订阅具有原子边界，不遗漏、不重复且保持 Sequence 顺序；LogNotifications 不重放历史事件。
- `CodeWFLogEvent` 只保存有界不可变 `LogExceptionInfo`，不把原始 Exception 对象引用交给后台队列或 recent buffer；Exception.Data 默认不捕获。
- InApp 通知具有最大可见数量，DesktopWindow 复用单窗口，通知展示队列有界且溢出数量可见；通知突发不能阻塞 pipeline。
- Provider 与 Serilog/NLog/log4net 至少一种并行时，业务代码无需修改，CodeWF 可只启用 LogEventFeed、LogView 和通知。
- Sequence 与实际单消费者处理顺序一致；LogEventFeed 的最终一致性和完整事件暴露风险在 API 与 Demo 中有明确说明。
- Queue FullMode 和 EnqueueTimeout 生效，丢弃数量通过独立健康状态可观测，慢订阅者不能阻塞 pipeline。
- Host 停止或 LoggerFactory Dispose 时可以 Flush 和 Shutdown；Flush 屏障语义通过并发测试验证。
- Sink、Formatter、State 快照和订阅者失败不会默认抛回业务线程。
- Options 非法值在启动阶段给出清晰错误；除 `LineTemplate` 的原子运行时切换外，pipeline 配置明确为修改后重启生效，不存在部分静默热更新。
- FileSink 具备有界保留策略，并明确单进程/多进程写入约束。
- MicrosoftLoggingWebApiDemo 只通过 `appsettings.json` 配置 CodeWF。
- MultiProviderAvaloniaDemo 验证 Serilog 负责诊断输出、CodeWF 只负责 LogView 和 InApp/DesktopWindow 通知，并覆盖动态日志按钮场景。
- `CodeWF.Log.Core`、`CodeWF.Log.Extensions.Logging`、`CodeWF.Log.Avalonia` 通过 trim/Native AOT smoke test。

## 19. 后续增强项

以下能力不阻塞第一版，但设计时应保留扩展空间：

- pipeline/sink 配置的原子热重载。
- EventSource、OpenTelemetry Metrics 或公开 `CodeWFLogHealth` 健康指标。
- 可插拔自定义 Sink/Formatter 公共扩展契约。
- 多进程安全的共享日志目录和跨进程滚动协调。
- 可插拔的属性脱敏与模板输出策略。

## 20. 实施顺序

实现必须按依赖顺序推进，每个阶段先完成对应自动化验证，再进入下一阶段，避免在 Demo 中掩盖 Core 或 Provider 的模型问题。

### 20.1 工程基线

- 三个 NuGet 包删除 `net8.0`，统一目标框架为 `net10.0`。
- Avalonia 和 Web API Demo 保持 `net11.0` / `net11.0-windows`，Console Demo 保持 `net11.0`，用于验证 .NET 11 应用引用 `net10.0` 库资产。
- 不增加强制整个解决方案使用 .NET 10 SDK 的根 `global.json`；库与 Demo 分别使用 .NET 10、.NET 11 SDK 验证，中央包版本继续使用 MEL 10.x。
- 在修改公共模型前记录当前 Release 构建和打包基线，保留现有文档改动，不覆盖无关工作区内容。

验收：三个库能在稳定版 .NET 10 SDK 下完成 Release build/test/pack；全部 Demo 能在 .NET 11 SDK 下完成 Release 构建，并确认引用的是三个库的 `net10.0` 资产。

### 20.2 Core 统一事件模型

- breaking 删除 `UserLogEntry`、公开 `UserLogPayload`、`UserLogFeed` 和 `UserLogMode`。
- 完成统一不可变 `CodeWFLogEvent`、可选 `UserMessage`、`LogExceptionInfo` 和有界属性/异常快照。
- 实现完整 `LogEventFeed`、recent buffer、按 Sequence 淘汰及原子“订阅并重放”。
- 实现实例级 `ILineTemplateController`、模板解析校验、原子更新和 `{UserMessage}` 空白回退语义。
- File 使用 `OutputTemplate`；Console 直接消费全部事件并使用共享 `LineTemplate`；EventFeedSink 发布同一事件实例。
- 保留 legacy `Logger.*`；`Logger.*ToFile` 通过内部路由信息只进入 FileSink，不引入第二种公开事件模型。
- 完成队列满策略、Sequence、Flush 屏障、Self Diagnostics 和文件滚动保留策略。

验收：Formatter、异常快照、Feed 并发重放、队列顺序/丢弃、Flush、文件滚动和清理均有自动化测试。

### 20.3 MEL Provider

- Provider 改为实例级 pipeline、`LogEventFeed` 和 `ILineTemplateController`，不直接复用静态 `LoggerHost`。
- 完成标准 MEL State、`{OriginalFormat}`、EventId、Scope、Activity、Exception 和 `LoggerMessage` 捕获。
- 用内部 State marker/interface 传递 `UserMessage`，第三方 Provider 只能枚举标准诊断参数。
- 补齐 Trace、Debug、Information、Warning、Error、Critical 六个 `LogUser*` 方法及通用 `LogUser(...)`。
- 完成 Options 绑定、启动校验、ContentRoot 相对路径、MEL 过滤、Provider Dispose、静态桥接和多 Host 隔离。

验收：标准日志与 `LogUser*` 的诊断格式化一致；多 Provider、多 Host、重复启动/释放和元数据隔离测试通过。

### 20.4 Avalonia

- `LogContext.Source` 和 `LogView.Source` 统一使用 `LogEventFeed`；全局、局部和 legacy Source 回退顺序按第 12 节实现。
- LogView 默认 Information 至 Critical，使用原子订阅重放，并在 `LineTemplate` 更新后重新渲染当前 recent buffer。
- LogNotifications 只按 MinimumLevel 判断，不重放历史事件；实现 None、InApp、DesktopWindow 三种模式。
- InApp 默认最多同时显示 3 条；DesktopWindow 复用一个右下角窗口；展示队列默认容量 100，溢出计数可见且不阻塞 pipeline。
- Console、同一 Source 下的所有 LogView 和 Notification 读取同一个实例级模板 Controller，不提供控件级模板覆盖。

验收：Source 切换、模板热切换、UI 线程调度、窗口重建、通知模式切换、突发队列和资源释放通过自动化或可重复 smoke test。

### 20.5 Demo

- 抽取只供 Demo 使用的动态场景生成器，所有按钮使用固定消息模板和动态结构化参数。
- 更新 AvaloniaLogDemo、ConsoleLogDemo、MicrosoftLoggingAvaloniaDemo 和 MicrosoftLoggingWebApiDemo 适配 breaking API。
- MicrosoftLoggingAvaloniaDemo 增加“简洁格式、上下文格式、自定义”模板编辑、预览、校验、应用和恢复交互。
- MicrosoftLoggingWebApiDemo 的 CodeWF 配置只保留在 `appsettings.json`，接口改为 `/recent-logs`。
- 新增 MultiProviderAvaloniaDemo：Serilog 负责文件/控制台，CodeWF 只启用 EventFeed、LogView 和通知；覆盖 InApp/DesktopWindow 及并发突发。

验收：所有 Demo 的六级别、普通/用户消息对照、Exception、EventId、Scope、Activity、LoggerMessage 和并发场景均可重复验证。

### 20.6 测试、AOT 与发布文档

- 新增 Core、MEL Provider 和 Avalonia 相关测试项目；UI 逻辑优先拆成可独立测试的状态与调度组件。
- 增加 Native AOT 控制台 smoke app、Avalonia trim/publish smoke test和 NuGet pack 验证。
- 更新 README、UpdateLog、包说明、配置示例和 breaking migration，删除所有旧 `UserLog*` 投影 API 示例。
- 最后执行完整 Release build、test、publish smoke 和 pack；同一提交同步更新第 17 节实现状态。

验收：满足第 18 节全部发布门槛后才进入发布流程。
