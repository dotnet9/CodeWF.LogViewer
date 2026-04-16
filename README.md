# CodeWF.Log

[![NuGet](https://img.shields.io/nuget/v/CodeWF.Log.Core.svg)](https://www.nuget.org/packages/CodeWF.Log.Core/)
[![NuGet](https://img.shields.io/nuget/dt/CodeWF.Log.Core.svg)](https://www.nuget.org/dt/CodeWF.Log.Core.svg)
[![NuGet](https://img.shields.io/nuget/v/CodeWF.LogViewer.Avalonia.svg)](https://www.nuget.org/packages/CodeWF.LogViewer.Avalonia/)
[![NuGet](https://img.shields.io/nuget/dt/CodeWF.LogViewer.Avalonia.svg)](https://www.nuget.org/packages/CodeWF.LogViewer.Avalonia/)
[![License](https://img.shields.io/github/license/dotnet9/CodeWF.LogViewer)](LICENSE)

轻量级、高性能 .NET 日志库，支持控制台和 Avalonia UI 应用程序。

## 两个 NuGet 包

| 包名 | 说明 | 适用场景 |
|------|------|---------|
| **CodeWF.Log.Core** | 核心日志库，仅依赖 .NET | 控制台程序、WPF、Avalonia 等所有 C# 程序 |
| **CodeWF.LogViewer.Avalonia** | Avalonia UI 控件，依赖 CodeWF.Log.Core | Avalonia UI 程序，提供日志展示控件 |

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
// App.OnLaunched 中调用一次（启动文件日志记录）
Logger.RecordToFile();

// 程序退出时调用
await Logger.FlushAsync();
```

> 注意：`LogView` 控件仅负责从 UI 通道消费日志并显示到界面，不处理文件写入。文件日志由 `RecordToFile()` 启动的后台任务处理。

### 日志消费

`LogView` 内部使用 `await foreach` 异步枚举模式消费 UI 通道日志，支持批量处理和防抖机制：
- 日志数量达到 `BatchProcessSize` 时立即刷新 UI
- 未达阈值时，最长延迟 `LogUIDuration`（默认100ms）后刷新

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
