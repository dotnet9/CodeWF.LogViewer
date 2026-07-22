using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml;
using CodeWF.Log.Core;
using CodeWF.LogViewer.Avalonia.Notifications.ViewModels;
using System;
using System.Collections.Generic;

namespace CodeWF.LogViewer.Avalonia.Notifications.Views;

internal partial class NotificationWindow : Window
{
    private readonly NotificationWindowViewModel _viewModel;

    public NotificationWindow()
    {
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

    public void AddLogs(IReadOnlyList<LogInfo> logInfos) => _viewModel.AddLogs(logInfos);

    public void CloseNotification() => _viewModel.RequestClose();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
