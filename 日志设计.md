# CodeWF.Log 库设计文档

## 概述

CodeWF.Log 是一个轻量级、高性能的 .NET 日志库，包含两个 NuGet 包：

| 包名 | 说明 |
|------|------|
| **CodeWF.Log.Core** | 核心日志库，仅依赖 .NET，支持所有 C# 程序 |
| **CodeWF.LogViewer.Avalonia** | Avalonia UI 控件，提供日志展示组件 |

采用 `System.Threading.Channels` 实现异步日志处理，具有低延迟、高吞吐量的特点。

## 代码组织

```
src/
├── CodeWF.Log.Core/              # 核心日志库
│   ├── Logger.cs                  # 日志管理器（核心）
│   ├── LogInfo.cs                 # 日志条目结构
│   ├── LogType.cs                 # 日志级别枚举
│   └── Extensions/
│       └── EnumExtensions.cs      # 枚举扩展方法
│
├── CodeWF.LogViewer.Avalonia/     # Avalonia UI 组件
│   └── Views/
│       └── LogView.axaml(.cs)     # 日志展示控件
│
├── ConsoleLogDemo/                # 控制台示例程序
└── AvaloniaLogDemo/               # Avalonia 示例程序
```

### 依赖关系

```
CodeWF.Log.Core
    └── 无外部依赖，仅依赖 .NET BCL

CodeWF.LogViewer.Avalonia
    └── 依赖 CodeWF.Log.Core
        └── 提供 Avalonia 专用的 LogView 控件
```

## 核心组件

### 1. LogInfo - 日志条目

```csharp
public readonly struct LogInfo
{
    public LogType Level { get; }           // 日志级别
    public DateTime RecordTime { get; }      // 记录时间
    public string Description { get; }       // 原始日志内容
    public string FriendlyDescription { get; } // UI显示用友好内容
    public bool Log2UI { get; }              // 是否输出到UI
    public bool Log2File { get; }            // 是否输出到文件
}
```

### 2. LogType - 日志级别

```csharp
public enum LogType
{
    Debug = 0,
    Info = 1,
    Warn = 2,
    Error = 3,
    Fatal = 4
}
```

### 3. Logger - 日志管理器

核心静态类，负责日志的创建、排队和持久化。

## 架构设计

### 双 Channel 架构

```
┌─────────────────────────────────────────────────────────────────┐
│                        日志生产者                                │
│   (任意线程：UI线程、工作线程、后台线程等)                         │
└─────────────────────────────────────────────────────────────────┘
                    │                           │
                    ▼                           ▼
        ┌───────────────────┐       ┌───────────────────┐
        │    LogChannel     │       │     UiChannel     │
        │   (文件日志队列)    │       │    (UI日志队列)    │
        │   容量: 10000     │       │    容量: 5000      │
        │   FullMode: Wait  │       │  FullMode: DropOldest │
        └───────────────────┘       └───────────────────┘
                    │                           │
                    ▼                           ▼
        ┌───────────────────┐       ┌───────────────────┐
        │  RecordToFile()   │       │ ReadAllUiLogsAsync()│
        │   (后台任务)       │       │   (后台任务)        │
        │   批量写入文件      │       │   批量更新UI        │
        └───────────────────┘       └───────────────────┘
                    │                           │
                    ▼                           ▼
        ┌───────────────────┐       ┌───────────────────┐
        │     LogFile        │       │    LogView       │
        │   (磁盘文件)        │       │   (Avalonia UI)  │
        └───────────────────┘       └───────────────────┘
```

### Channel 配置

| Channel | 容量 | FullMode | 说明 |
|---------|------|----------|------|
| LogChannel | 10000 | Wait | 队列满时等待，防止日志丢失 |
| UiChannel | 5000 | DropOldest | UI队列满时丢弃旧日志，保证UI响应 |

**设计理由**：
- 文件日志不能丢失，采用 `Wait` 模式确保日志最终写入
- UI日志可以丢失旧数据，采用 `DropOldest` 避免UI卡顿

### 线程安全

1. **Channel.Writer.TryWrite()** - 线程安全，多个生产者可同时写入
2. **SingleReader = true** - 保证只有一个消费者读取
3. **_isFlushing 锁** - 防止 FlushAsync 与 RecordToFile 冲突

## 工作流程

### 1. 初始化

**控制台程序**：
```csharp
// 程序启动时调用一次
Logger.RecordToFile();
```

**Avalonia 程序（使用 LogView）**：无需调用，`LogView` 内部自动处理

内部启动后台任务：
```csharp
Task.Run(async () =>
{
    var batch = new List<LogInfo>();
    await foreach (var log in LogChannel.Reader.ReadAllAsync(_cts.Token))
    {
        batch.Add(log);
        if (batch.Count >= BatchProcessSize)
        {
            FlushBatchToFile(batch);
            batch.Clear();
        }
    }
});
```

### 2. 记录日志

```csharp
Logger.Info("消息内容");
```

流程：
1. 检查日志级别是否启用
2. 写入控制台（如启用）
3. 创建 LogInfo 对象
4. 根据 log2File/log2UI 分别写入对应 Channel

### 3. 消费日志（UI）

```csharp
// 在后台任务中异步消费
await foreach (var log in Logger.ReadAllUiLogsAsync(cancellationToken))
{
    // 处理日志
}
```

### 4. 刷新退出

```csharp
// 程序退出时调用
await Logger.FlushAsync();
```

流程：
1. 加锁防止冲突
2. 从 LogChannel 取出所有待写入日志
3. 批量写入文件
4. 清空 UiChannel
5. 解锁

## 配置参数

| 参数 | 默认值 | 说明 |
|------|--------|------|
| Level | Info | 日志级别，低于此级别的日志被忽略 |
| LogDir | AppDomain.CurrentDomain.BaseDirectory | 日志文件存储目录 |
| BatchProcessSize | 200 | 批量写入的日志条数阈值 |
| MaxLogFileSizeMB | 500 | 单个日志文件最大大小（MB） |
| LogFileDuration | 500 | 批量写入时间间隔（ms） |
| MaxUIDisplayCount | 1000 | UI最多显示的日志条数 |
| LogUIDuration | 100 | UI刷新间隔（ms） |
| TimeFormat | "yyyy-MM-dd HH:mm:ss" | 时间戳格式 |
| EnableConsoleOutput | true | 是否输出到控制台 |

## 性能优化

### 1. 批量写入 + 防抖机制

不是每条日志都写文件，而是积累到 `BatchProcessSize` 条或 `LogFileDuration` 时间后才写入，减少 I/O 操作。

**防抖逻辑：**
- 当日志数量达到 `BatchProcessSize` 时立即处理
- 未达到时启动防抖定时器
- 如果在防抖期间有新日志到达，取消之前的定时器并重新计时
- 确保即使日志量很少，也能在合理时间内（最多 `LogFileDuration`/`LogUIDuration`）被处理

### 2. 异步处理

使用 `Channel<T>` 实现生产者-消费者模式，日志方法立即返回，不阻塞调用线程。

### 3. 分离通道

文件和 UI 使用独立的 Channel，互不干扰：
- 文件通道：保证日志不丢失
- UI通道：保证 UI 响应性

### 4. `await foreach` 异步枚举

使用 C# 异步流特性消费日志，有日志时自动唤醒，无日志时自然等待，无需手动轮询。

```csharp
// Logger 内部实现
public static async IAsyncEnumerable<LogInfo> ReadAllUiLogsAsync(
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    await foreach (var log in UiChannel.Reader.ReadAllAsync(cancellationToken))
    {
        yield return log;
    }
}

// LogView 消费端
await foreach (var log in Logger.ReadAllUiLogsAsync(token))
{
    // 处理日志
}
```

### 5. 有界队列

防止内存无限增长，满时自动处理（等待或丢弃）。

## 使用示例

### CodeWF.Log.Core - 控制台程序

```csharp
class Program
{
    static async Task Main(string[] args)
    {
        Logger.RecordToFile();

        Logger.Debug("调试信息");
        Logger.Info("普通信息");
        Logger.Warn("警告信息");
        Logger.Error("错误信息");
        Logger.Fatal("致命错误");

        await Logger.FlushAsync();
    }
}
```

### CodeWF.LogViewer.Avalonia - Avalonia 程序

如果**不使用** `LogView` 控件，需要手动初始化：

```csharp
// App.axaml.cs
protected override void OnLaunched(LaunchedEventArgs args)
{
    Logger.RecordToFile();
}

// 程序退出
protected override async void OnExit(ExitEventArgs args)
{
    await Logger.FlushAsync();
    base.OnExit(args);
}
```

如果**使用** `LogView` 控件，控件内部已自动处理文件写入和日志消费，无需调用上述方法。

### 仅输出到文件

```csharp
Logger.InfoToFile("这条日志只写入文件");
```

### 仅输出到UI

```csharp
Logger.LogToUI(LogType.Info, "这条日志只显示在UI上");
```

### 自定义输出目标

```csharp
Logger.Info(
    content: "文件内容",
    uiContent: "UI显示内容",  // 可选
    log2UI: true,
    log2File: true,
    log2Console: true
);
```

## 注意事项

1. **RecordToFile() 仅控制台程序需要调用**，Avalonia 程序使用 `LogView` 时会自动调用
2. **FlushAsync() 在退出时调用**，确保所有日志写入文件
3. **Channel 是不可变的**，创建后不能修改
