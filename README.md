# CodeWF.Log

[![NuGet](https://img.shields.io/nuget/v/CodeWF.Log.Core.svg)](https://www.nuget.org/packages/CodeWF.Log.Core/)
[![NuGet](https://img.shields.io/nuget/v/CodeWF.LogViewer.Avalonia.svg)](https://www.nuget.org/packages/CodeWF.LogViewer.Avalonia/)
[![License](https://img.shields.io/github/license/dotnet9/CodeWF.LogViewer)](LICENSE)

面向 .NET 和 Avalonia 的轻量日志组件。当前版本为 `12.1.0.16`，核心目标是把“给用户看的内容”和“给维护人员排障的内容”明确分开。

## 包

| 包 | 用途 | 目标框架 |
| --- | --- | --- |
| `CodeWF.Log.Core` | 文件日志、控制台输出和用户日志 Feed | `net8.0;net10.0` |
| `CodeWF.LogViewer.Avalonia` | `LogView` 和日志通知 | `net8.0;net10.0` |

版本统一维护在根目录 `Directory.Build.props`。运行 `pack.bat` 可将两个包输出到 `artifacts/packages`。

## 初始化

每个进程必须在首次写日志或创建 `LogView` 前初始化一次：

```csharp
Logger.Initialize(new LoggerOptions
{
    MinimumLevel = LogType.Info,
    EnableConsole = true,
    QueueCapacity = 10_000,
    RecentUserLogCapacity = 2_000,
    File = new FileLogOptions
    {
        DirectoryPath = Path.Combine(Environment.CurrentDirectory, "Log"),
        MaxFileSizeBytes = 100L * 1024 * 1024,
        BatchSize = 200,
        FlushInterval = TimeSpan.FromMilliseconds(500)
    }
});
```

`DirectoryPath` 是最终日志目录，组件不会再追加子目录。相对路径会在初始化时按当时的工作目录转换为绝对路径，因此多实例程序应由调用方按实例工作目录传入路径。

程序正常退出前调用：

```csharp
await Logger.ShutdownAsync();
```

需要在运行中确保已落盘时调用 `await Logger.FlushAsync()`；它不能替代退出时的 `ShutdownAsync()`。

## 用户内容与技术内容

普通方法写文件，并把用户内容发送到已启用的控制台、`LogView` 和通知订阅方：

```csharp
Logger.Info("任务已保存。");

Logger.Error(
    $"反序列化任务文件失败：{taskFilePath}",
    exception,
    "无法打开任务：任务文件内容不正确或与当前版本不兼容，请重新导出任务文件。");
```

- 文件记录技术消息、用户提示和完整异常堆栈。
- 控制台、`LogView` 和通知只接收 `userMessage`。
- 未传 `userMessage` 时，普通 `message` 同时作为用户内容显示。
- 用户日志对象 `UserLogEntry` 不持有异常对象，展示层不会意外泄露堆栈。

只写文件时使用：

```csharp
Logger.DebugToFile("连接参数：127.0.0.1:2700");
Logger.ErrorToFile("关闭后台队列失败。", exception);
```

可用方法包括 `Debug/Info/Warn/Error/Fatal` 和对应的 `*ToFile` 方法。`Logger.MinimumLevel` 是全局采集门槛，低于该级别的日志不会进入任何输出端。

## Avalonia LogView

```xml
<Window
    xmlns="https://github.com/avaloniaui"
    xmlns:log="https://codewf.com">
    <Grid>
        <log:LogView
            MinimumLevel="Warn"
            MaximumLevel="Error"
            MaxDisplayCount="1000"
            RefreshInterval="00:00:00.100"
            TimestampFormat="yyyy-MM-dd HH:mm:ss.fff" />
    </Grid>
</Window>
```

`MinimumLevel` 和 `MaximumLevel` 都是强类型 `LogType` 属性，可以绑定到 ViewModel 中的枚举属性：

```csharp
public LogType MinimumLevel { get; set; } = LogType.Info;
public LogType MaximumLevel { get; set; } = LogType.Error;
```

每个 `LogView` 独立过滤和清空，互不影响。修改过滤范围时，控件会从最近用户日志缓存重新构建当前视图。

全局级别与视图级别的关系：

- `Logger.MinimumLevel` 决定日志是否被采集，是全局门槛。
- `LogView.MinimumLevel/MaximumLevel` 只决定当前控件显示哪些已采集日志。
- 视图不能显示低于全局门槛、从未被采集的日志。

## Avalonia 通知

通知独立于 `LogView`，在 `App.axaml` 中统一配置一次：

```xml
<Application
    xmlns="https://github.com/avaloniaui"
    xmlns:log="https://codewf.com"
    log:LogNotifications.Mode="DesktopWindow"
    log:LogNotifications.MinimumLevel="Error"
    log:LogNotifications.MaximumLevel="Fatal"
    log:LogNotifications.Duration="00:00:10"
    log:LogNotifications.ApplicationName="设备服务客户端">
</Application>
```

`Mode` 支持：

- `None`：关闭通知。
- `InApp`：在当前活动窗口内显示通知。
- `DesktopWindow`：显示桌面通知窗口。

通知只接收启用后的新用户日志，不回放历史，也不会接收 `*ToFile` 日志。最小/最大级别同样是强类型 `LogType` 属性。组件在显示时动态选择活动窗口或主窗口，多窗口程序仍只创建一个通知订阅。桌面通知是主窗口的 owned window，主窗口关闭时会立即随之关闭，不会延迟应用退出。桌面窗口继续支持自定义正文模板、显示时间和 `DesktopNotificationAttentionMode`。

## Demo

- `ConsoleLogDemo`：演示用户/技术内容分流、异常堆栈、文件专用日志、并发写入、Flush 和文件轮转。
- `AvaloniaLogDemo`：用操作按钮和三个日志视图演示友好异常、文件专用日志、并发写入以及独立级别过滤；通知在 `App.axaml` 中统一配置，默认无需注册主题资源，并可动态切换一组自定义资源覆盖。

## 12.1 升级提示

这是一次不保留旧语义的主版本升级。需要删除旧的 `RecordToFile`、`LogDir`、`EnableConsoleOutput`、`log2UI/log2File/log2Console` 和 `LogView.Notification*` 用法，统一改为：

1. 启动时调用 `Logger.Initialize(LoggerOptions)`。
2. 技术异常使用 `Logger.Error(technicalMessage, exception, userMessage)`。
3. 内部诊断使用 `*ToFile`。
4. 通知在 `App.axaml` 中使用应用级 `LogNotifications` 附加属性。
5. 退出时调用 `Logger.ShutdownAsync()`。
