using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml;
using CodeWF.Log.Core;
using CodeWF.Log.Avalonia.Notifications.ViewModels;
using System;
using System.Collections.Generic;

namespace CodeWF.Log.Avalonia.Notifications.Views;

internal partial class NotificationWindow : Window
{
    private readonly NotificationWindowViewModel _viewModel;

    public NotificationWindow()
    {
        // App.axaml 可能在应用级通知初始化后替换 Resources，创建窗口前再次确认默认资源仍在。
        LogNotificationResources.EnsureRegistered();
        InitializeComponent();
        _viewModel = new NotificationWindowViewModel();
        DataContext = _viewModel;
    }

    public bool IsClosing => _viewModel.IsClosing;

    public void Configure(
        string applicationName,
        TimeSpan duration,
        TopLevel? host,
        IDataTemplate? contentTemplate,
        DesktopNotificationAttentionMode attentionMode) =>
        _viewModel.Configure(applicationName, duration, host, contentTemplate, attentionMode);

    public void AddLogs(IReadOnlyList<(CodeWFLogEvent Entry, string Content)> logEntries) => _viewModel.AddLogs(logEntries);

    public void CloseNotification() => _viewModel.RequestClose();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
