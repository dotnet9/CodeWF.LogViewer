using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using CodeWF.Log.Core;
using CodeWF.Log.Avalonia.Notifications.ViewModels;
using System;
using System.ComponentModel;
using Microsoft.Extensions.Logging;

namespace CodeWF.Log.Avalonia.Notifications.Behaviors;

/// <summary>
/// 根据当前日志级别同步徽标、图标和标题的样式类。
/// </summary>
public sealed class NotificationLevelVisualBehavior : Behavior<Border>
{
    private static readonly string[] LevelClassNames = ["debug", "info", "warn", "error", "fatal"];

    public static readonly StyledProperty<PathIcon?> IconTargetProperty =
        AvaloniaProperty.Register<NotificationLevelVisualBehavior, PathIcon?>(nameof(IconTarget));

    public static readonly StyledProperty<TextBlock?> TextTargetProperty =
        AvaloniaProperty.Register<NotificationLevelVisualBehavior, TextBlock?>(nameof(TextTarget));

    private NotificationWindowViewModel? _viewModel;

    public PathIcon? IconTarget
    {
        get => GetValue(IconTargetProperty);
        set => SetValue(IconTargetProperty, value);
    }

    public TextBlock? TextTarget
    {
        get => GetValue(TextTargetProperty);
        set => SetValue(TextTargetProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject == null) return;

        AssociatedObject.DataContextChanged += OnDataContextChanged;
        SubscribeViewModel(AssociatedObject.DataContext as NotificationWindowViewModel);
        ApplyLevelClasses();
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject != null) AssociatedObject.DataContextChanged -= OnDataContextChanged;
        SubscribeViewModel(null);
        base.OnDetaching();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconTargetProperty || change.Property == TextTargetProperty) ApplyLevelClasses();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        SubscribeViewModel(AssociatedObject?.DataContext as NotificationWindowViewModel);
    }

    private void SubscribeViewModel(NotificationWindowViewModel? viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel)) return;

        if (_viewModel != null) _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel = viewModel;
        if (_viewModel != null) _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        ApplyLevelClasses();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NotificationWindowViewModel.Level)) ApplyLevelClasses();
    }

    private void ApplyLevelClasses()
    {
        if (AssociatedObject == null || _viewModel == null) return;

        var levelClass = _viewModel.Level switch
        {
            LogLevel.Debug => "debug",
            LogLevel.Information => "info",
            LogLevel.Warning => "warn",
            LogLevel.Critical => "fatal",
            _ => "error"
        };

        foreach (var name in LevelClassNames)
        {
            AssociatedObject.Classes.Set(name, name == levelClass);
            IconTarget?.Classes.Set(name, name == levelClass);
            TextTarget?.Classes.Set(name, name == levelClass);
        }
    }
}
