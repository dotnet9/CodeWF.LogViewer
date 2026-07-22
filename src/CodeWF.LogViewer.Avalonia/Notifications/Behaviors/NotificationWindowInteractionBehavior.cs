using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Xaml.Interactivity;
using CodeWF.LogViewer.Avalonia.Notifications.ViewModels;
using System;

namespace CodeWF.LogViewer.Avalonia.Notifications.Behaviors;

/// <summary>
/// 将通知窗口生命周期、Hover 和快捷键转发给 ViewModel。
/// </summary>
public sealed class NotificationWindowInteractionBehavior : Behavior<Window>
{
    private NotificationWindowViewModel? _viewModel;

    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject == null) return;

        AssociatedObject.Opened += OnOpened;
        AssociatedObject.Closed += OnClosed;
        AssociatedObject.PointerEntered += OnPointerEntered;
        AssociatedObject.PointerExited += OnPointerExited;
        AssociatedObject.KeyDown += OnKeyDown;
        AssociatedObject.DataContextChanged += OnDataContextChanged;
        SubscribeViewModel(AssociatedObject.DataContext as NotificationWindowViewModel);
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject != null)
        {
            AssociatedObject.Opened -= OnOpened;
            AssociatedObject.Closed -= OnClosed;
            AssociatedObject.PointerEntered -= OnPointerEntered;
            AssociatedObject.PointerExited -= OnPointerExited;
            AssociatedObject.KeyDown -= OnKeyDown;
            AssociatedObject.DataContextChanged -= OnDataContextChanged;
        }

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

        if (_viewModel != null) _viewModel.CloseRequested -= OnCloseRequested;
        _viewModel = viewModel;
        if (_viewModel != null) _viewModel.CloseRequested += OnCloseRequested;
    }

    private void OnOpened(object? sender, EventArgs e) => _viewModel?.OnOpened();

    private void OnClosed(object? sender, EventArgs e) => _viewModel?.OnClosed();

    private void OnPointerEntered(object? sender, PointerEventArgs e) => _viewModel?.SetPointerInside(true);

    private void OnPointerExited(object? sender, PointerEventArgs e) => _viewModel?.SetPointerInside(false);

    private void OnCloseRequested(object? sender, EventArgs e) => AssociatedObject?.Close();

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel == null) return;

        switch (e.Key)
        {
            case Key.Left:
                _viewModel.SelectPrevious();
                e.Handled = true;
                break;
            case Key.Right:
                _viewModel.SelectNext();
                e.Handled = true;
                break;
            case Key.Enter:
            case Key.Escape:
                _viewModel.RequestClose();
                e.Handled = true;
                break;
            case Key.O when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                _viewModel.OpenLogFolder();
                e.Handled = true;
                break;
        }
    }
}
