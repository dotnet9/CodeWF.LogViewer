# CodeWF.LogViewer

�򵥷�װһЩ��־�ؼ�

## CodeWF.LogViewer.Avalonia.Log4Net

AvaloniaUI��ʹ�õ�SelectableTextBlock��Ϊ��־չʾ�ؼ���NuGet����װ��

```shell
Install-Package CodeWF.LogViewer.Avalonia.Log4Net
```

`.axaml`ʹ�ã�

```html
xmlns:log="https://codewf.com"
```

```html
<log:LogView /> 
```

�����������־

```csharp
LogFactory.Instance.Log.Debug("������־");
LogFactory.Instance.Log.Info("��ͨ��־");
LogFactory..Instance.Log.Warn("������־");
LogFactory.Instance.Log.Error("������־");
LogFactory.Instance.Log.Fatal("���ش�����־");
```