# CodeWF.LogViewer

简单封装一些日志控件

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