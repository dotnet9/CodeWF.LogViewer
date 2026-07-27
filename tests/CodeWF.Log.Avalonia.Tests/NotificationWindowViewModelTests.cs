using CodeWF.Log.Avalonia;
using CodeWF.Log.Avalonia.Notifications.ViewModels;
using CodeWF.Log.Core;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CodeWF.Log.Avalonia.Tests;

public sealed class NotificationWindowViewModelTests
{
    [Fact]
    public void NewLogIndicator_TracksLogsAddedWhileWindowIsOpen()
    {
        var viewModel = CreateViewModel();
        viewModel.AddLogs([(CreateEvent(1, LogLevel.Error), "first")]);

        Assert.False(viewModel.IsNewLogVisible);
        Assert.False(viewModel.IsNavigationVisible);
        Assert.Equal(284, viewModel.MinimumCardHeight);
        Assert.Equal("1 / 1", viewModel.CountText);

        viewModel.OnOpened();
        viewModel.AddLogs([
            (CreateEvent(2, LogLevel.Error), "second"),
            (CreateEvent(3, LogLevel.Critical), "third")
        ]);

        Assert.True(viewModel.IsNewLogVisible);
        Assert.Equal("2条新日志", viewModel.NewLogText);
        Assert.True(viewModel.IsNavigationVisible);
        Assert.Equal(320, viewModel.MinimumCardHeight);
        Assert.Equal("3 / 3", viewModel.CountText);
        Assert.False(viewModel.IsPreviousDisabled);
        Assert.True(viewModel.IsNextDisabled);

        viewModel.SelectPrevious();
        Assert.Equal("2 / 3", viewModel.CountText);
        Assert.Equal("2条新日志", viewModel.NewLogText);
        Assert.False(viewModel.IsPreviousDisabled);
        Assert.False(viewModel.IsNextDisabled);

        viewModel.OnClosed();
        Assert.False(viewModel.IsNewLogVisible);
    }

    [Fact]
    public void SelectedCriticalLog_UsesCriticalTextAndTruncatesExcessiveContent()
    {
        var viewModel = CreateViewModel();
        var content = new string('x', 4100);

        var logEvent = CreateEvent(1, LogLevel.Critical) with
        {
            Message = content,
            UserMessage = content
        };
        viewModel.AddLogs([(logEvent, "formatted template content")]);

        Assert.Equal(LogLevel.Critical, viewModel.Level);
        Assert.Equal("严重错误", viewModel.LevelText);
        Assert.Equal(4003, viewModel.LogContent.Length);
        Assert.EndsWith("...", viewModel.LogContent);
        Assert.Equal("formatted template content", viewModel.SelectedLog?.Content);
        Assert.Equal(420, viewModel.MinimumCardHeight);
    }

    private static NotificationWindowViewModel CreateViewModel()
    {
        var viewModel = new NotificationWindowViewModel();
        viewModel.Configure(
            "Test App",
            TimeSpan.Zero,
            host: null,
            contentTemplate: null,
            DesktopNotificationAttentionMode.None);
        return viewModel;
    }

    private static CodeWFLogEvent CreateEvent(long sequence, LogLevel level) => new()
    {
        Sequence = sequence,
        Timestamp = new DateTimeOffset(2026, 7, 27, 10, 30, 0, TimeSpan.Zero),
        Level = level,
        CategoryName = "Tests",
        Message = $"message-{sequence}",
        UserMessage = $"user-message-{sequence}",
        RequestNotification = true
    };
}
