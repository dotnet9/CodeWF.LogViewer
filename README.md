# CodeWF.LogViewer

简单封装一些日志控件

## CodeWF.LogViewer.Avalonia

AvaloniaUI中使用的SelectableTextBlock做为日志展示控件，NuGet包安装：

```shell
Install-Package CodeWF.LogViewer.Avalonia
```

`.axaml`使用：

```html
xmlns:log="https://codewf.com"
```

```html
<log:LogView /> 
```

代码中添加日志

```csharp
Logger.Debug("调试日志");
Logger.Info("普通日志");
Logger.Warn("警告日志");
Logger.Error("错误日志");
Logger.Fatal("严重错误日志");
```

![](doc\imgs\log.gif)

## CodeWF.LogViewer.Avalonia.Log4Net

AvaloniaUI中使用的SelectableTextBlock做为日志展示控件，NuGet包安装：

```shell
Install-Package CodeWF.LogViewer.Avalonia.Log4Net
```

`.axaml`使用：

```html
xmlns:log="https://codewf.com"
```

```html
<log:LogView /> 
```

代码中添加日志

```csharp
LogFactory.Instance.Log.Debug("调试日志");
LogFactory.Instance.Log.Info("普通日志");
LogFactory..Instance.Log.Warn("警告日志");
LogFactory.Instance.Log.Error("错误日志");
LogFactory.Instance.Log.Fatal("严重错误日志");
```

![](doc\imgs\log.gif)

## 更新日志

### V1.0.11.3（2025-09-15）

1. 🐛[修复]-修复自定义日志目录打开异常问题