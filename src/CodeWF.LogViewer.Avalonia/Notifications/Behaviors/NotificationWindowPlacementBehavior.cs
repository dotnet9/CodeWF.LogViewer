using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Xaml.Interactivity;
using CodeWF.LogViewer.Avalonia.Notifications.ViewModels;
using System;

namespace CodeWF.LogViewer.Avalonia.Notifications.Behaviors;

/// <summary>
/// 将通知窗口定位到宿主所在屏幕的工作区右下角。
/// </summary>
public sealed class NotificationWindowPlacementBehavior : Behavior<Window>
{
    private const int ScreenMargin = 16;
    private const double DefaultWindowWidth = 390;
    private const double FallbackWindowHeight = 430;

    private NotificationWindowViewModel? _viewModel;
    private bool _isOpened;
    private bool _positionScheduled;

    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject == null) return;

        AssociatedObject.Opened += OnOpened;
        AssociatedObject.Closed += OnClosed;
        AssociatedObject.SizeChanged += OnSizeChanged;
        AssociatedObject.DataContextChanged += OnDataContextChanged;
        SubscribeViewModel(AssociatedObject.DataContext as NotificationWindowViewModel);
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject != null)
        {
            AssociatedObject.Opened -= OnOpened;
            AssociatedObject.Closed -= OnClosed;
            AssociatedObject.SizeChanged -= OnSizeChanged;
            AssociatedObject.DataContextChanged -= OnDataContextChanged;
        }

        DetachScreenEvents();
        SubscribeViewModel(null);
        base.OnDetaching();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        SubscribeViewModel(AssociatedObject?.DataContext as NotificationWindowViewModel);
    }

    private void SubscribeViewModel(NotificationWindowViewModel? viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel)) return;

        if (_viewModel != null) _viewModel.PlacementRequested -= OnPlacementRequested;
        _viewModel = viewModel;
        if (_viewModel != null) _viewModel.PlacementRequested += OnPlacementRequested;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        _isOpened = true;
        if (AssociatedObject != null) AssociatedObject.Screens.Changed += OnScreensChanged;
        RequestPosition();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        DetachScreenEvents();
    }

    private void DetachScreenEvents()
    {
        if (_isOpened && AssociatedObject != null) AssociatedObject.Screens.Changed -= OnScreensChanged;
        _isOpened = false;
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_isOpened) RequestPosition();
    }

    private void OnScreensChanged(object? sender, EventArgs e) => RequestPosition();

    private void OnPlacementRequested(object? sender, EventArgs e)
    {
        if (_isOpened) RequestPosition();
    }

    private void RequestPosition()
    {
        if (_positionScheduled) return;

        _positionScheduled = true;
        Dispatcher.UIThread.Post(() =>
        {
            _positionScheduled = false;
            PositionAtBottomRight();
        }, DispatcherPriority.Loaded);
    }

    private void PositionAtBottomRight()
    {
        var window = AssociatedObject;
        if (window == null) return;

        var screen = GetTargetScreen(window);
        if (screen == null) return;

        var scaling = screen.Scaling;
        var width = GetPixelSize(window.Bounds.Width, window.Width, DefaultWindowWidth, scaling);
        var height = GetPixelSize(window.Bounds.Height, window.Height, FallbackWindowHeight, scaling);
        var margin = (int)Math.Ceiling(ScreenMargin * scaling);
        var workingArea = screen.WorkingArea;
        window.Position = new PixelPoint(
            workingArea.Right - width - margin,
            workingArea.Bottom - height - margin);
    }

    private global::Avalonia.Platform.Screen? GetTargetScreen(Window window)
    {
        try
        {
            if (_viewModel?.NotificationHost is Window hostWindow)
            {
                var center = new PixelPoint(
                    hostWindow.Position.X +
                    (int)Math.Ceiling(hostWindow.Bounds.Width * hostWindow.RenderScaling / 2),
                    hostWindow.Position.Y +
                    (int)Math.Ceiling(hostWindow.Bounds.Height * hostWindow.RenderScaling / 2));
                return window.Screens.ScreenFromPoint(center) ?? window.Screens.Primary;
            }

            if (_viewModel?.NotificationHost != null)
                return window.Screens.ScreenFromTopLevel(_viewModel.NotificationHost) ?? window.Screens.Primary;
        }
        catch (ObjectDisposedException)
        {
        }

        return window.Screens.Primary;
    }

    private static int GetPixelSize(double renderedSize, double configuredSize, double fallbackSize, double scaling)
    {
        var size = IsValidSize(renderedSize)
            ? renderedSize
            : IsValidSize(configuredSize)
                ? configuredSize
                : fallbackSize;
        return Math.Max(1, (int)Math.Ceiling(size * scaling));
    }

    private static bool IsValidSize(double value) => double.IsFinite(value) && value > 0;
}
