using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Xaml.Interactivity;
using CodeWF.Log.Core;
using CodeWF.Log.Avalonia.Notifications.ViewModels;
using System;
using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CodeWF.Log.Avalonia.Notifications.Behaviors;

/// <summary>
/// 管理重要日志通知窗口的微抖与级别图标脉冲。
/// </summary>
public sealed class NotificationAttentionBehavior : Behavior<Border>
{
    private const long AttentionCooldownMilliseconds = 2000;
    private const long FrameIntervalMilliseconds = 16;
    private const long WarnDurationMilliseconds = 220;
    private const long ErrorDurationMilliseconds = 280;
    private const long FatalDurationMilliseconds = 380;

    private static readonly Frame[] ErrorShakeFrames =
    [
        new(0, 0), new(0.14, -5), new(0.28, 5), new(0.42, -4),
        new(0.56, 4), new(0.7, -2), new(0.84, 2), new(1, 0)
    ];

    private static readonly Frame[] FatalShakeFrames =
    [
        new(0, 0), new(0.11, -7), new(0.22, 7), new(0.33, -6), new(0.44, 6),
        new(0.56, -4), new(0.68, 4), new(0.8, -2), new(0.9, 2), new(1, 0)
    ];

    private static readonly Frame[] DefaultPulseFrames =
    [
        new(0, 1), new(0.45, 1.1), new(1, 1)
    ];

    private static readonly Frame[] FatalPulseFrames =
    [
        new(0, 1), new(0.18, 1.12), new(0.38, 1), new(0.62, 1.1), new(0.8, 1), new(1, 1)
    ];

    public static readonly StyledProperty<Control?> PulseTargetProperty =
        AvaloniaProperty.Register<NotificationAttentionBehavior, Control?>(nameof(PulseTarget));

    private readonly DispatcherTimer _animationTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(FrameIntervalMilliseconds)
    };

    private readonly Stopwatch _animationStopwatch = new();
    private readonly TranslateTransform _windowTransform = new();
    private readonly ScaleTransform _pulseTransform = new(1, 1);

    private ITransform? _originalWindowTransform;
    private ITransform? _originalPulseTransform;
    private RelativePoint _originalPulseTransformOrigin;
    private Control? _configuredPulseTarget;
    private Frame[] _shakeFrames = ErrorShakeFrames;
    private Frame[] _pulseFrames = DefaultPulseFrames;
    private long _animationDurationMilliseconds;
    private long _lastAttentionTimestamp;
    private LogLevel _lastAttentionLevel;
    private bool _shouldShake;
    private NotificationWindowViewModel? _viewModel;

    public Control? PulseTarget
    {
        get => GetValue(PulseTargetProperty);
        set => SetValue(PulseTargetProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject == null) return;

        _originalWindowTransform = AssociatedObject.RenderTransform;
        AssociatedObject.RenderTransform = _windowTransform;
        ConfigurePulseTarget(PulseTarget);
        _animationTimer.Tick += OnAnimationTimerTick;
        AssociatedObject.DataContextChanged += OnAssociatedObjectDataContextChanged;
        SubscribeViewModel(AssociatedObject.DataContext as NotificationWindowViewModel);
    }

    protected override void OnDetaching()
    {
        _animationTimer.Tick -= OnAnimationTimerTick;
        if (AssociatedObject != null) AssociatedObject.DataContextChanged -= OnAssociatedObjectDataContextChanged;
        SubscribeViewModel(null);
        Stop();
        RestorePulseTarget();
        if (AssociatedObject != null) AssociatedObject.RenderTransform = _originalWindowTransform;
        _originalWindowTransform = null;
        base.OnDetaching();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == PulseTargetProperty)
        {
            ConfigurePulseTarget(PulseTarget);
        }
    }

    private void OnAssociatedObjectDataContextChanged(object? sender, EventArgs e)
    {
        SubscribeViewModel(AssociatedObject?.DataContext as NotificationWindowViewModel);
    }

    private void SubscribeViewModel(NotificationWindowViewModel? viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel)) return;

        if (_viewModel != null)
        {
            _viewModel.AttentionRequested -= OnAttentionRequested;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = viewModel;
        if (_viewModel == null) return;

        _viewModel.AttentionRequested += OnAttentionRequested;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NotificationWindowViewModel.AttentionMode) &&
            _viewModel?.AttentionMode == DesktopNotificationAttentionMode.None)
            Stop();
    }

    private void OnAttentionRequested(LogLevel level)
    {
        var viewModel = _viewModel;
        if (AssociatedObject == null || viewModel == null ||
            viewModel.AttentionMode == DesktopNotificationAttentionMode.None || level < LogLevel.Warning)
            return;

        var now = Stopwatch.GetTimestamp();
        var elapsedMilliseconds = _lastAttentionTimestamp == 0
            ? long.MaxValue
            : (now - _lastAttentionTimestamp) * 1000 / Stopwatch.Frequency;
        if (elapsedMilliseconds < AttentionCooldownMilliseconds && level <= _lastAttentionLevel) return;

        _lastAttentionTimestamp = now;
        _lastAttentionLevel = level;
        _shouldShake = viewModel.AttentionMode == DesktopNotificationAttentionMode.ShakeAndPulse &&
                       level >= LogLevel.Error &&
                       !viewModel.SuppressShake;

        var isFatal = level == LogLevel.Critical;
        _shakeFrames = isFatal ? FatalShakeFrames : ErrorShakeFrames;
        _pulseFrames = isFatal ? FatalPulseFrames : DefaultPulseFrames;
        _animationDurationMilliseconds = isFatal
            ? FatalDurationMilliseconds
            : level == LogLevel.Error
                ? ErrorDurationMilliseconds
                : WarnDurationMilliseconds;

        _animationTimer.Stop();
        _animationStopwatch.Restart();
        ApplyFrame(0);
        _animationTimer.Start();
    }

    private void Stop()
    {
        _animationTimer.Stop();
        _animationStopwatch.Reset();
        ResetTransforms();
    }

    private void OnAnimationTimerTick(object? sender, EventArgs e)
    {
        var elapsedMilliseconds = _animationStopwatch.ElapsedMilliseconds;
        if (elapsedMilliseconds >= _animationDurationMilliseconds)
        {
            Stop();
            return;
        }

        ApplyFrame((double)elapsedMilliseconds / _animationDurationMilliseconds);
    }

    private void ApplyFrame(double progress)
    {
        _windowTransform.X = _shouldShake ? Interpolate(_shakeFrames, progress) : 0;
        var scale = Interpolate(_pulseFrames, progress);
        _pulseTransform.ScaleX = scale;
        _pulseTransform.ScaleY = scale;
    }

    private void ConfigurePulseTarget(Control? target)
    {
        if (ReferenceEquals(_configuredPulseTarget, target)) return;

        RestorePulseTarget();
        _configuredPulseTarget = target;
        if (target == null) return;

        _originalPulseTransform = target.RenderTransform;
        _originalPulseTransformOrigin = target.RenderTransformOrigin;
        target.RenderTransformOrigin = RelativePoint.Center;
        target.RenderTransform = _pulseTransform;
    }

    private void RestorePulseTarget()
    {
        if (_configuredPulseTarget == null) return;

        _configuredPulseTarget.RenderTransform = _originalPulseTransform;
        _configuredPulseTarget.RenderTransformOrigin = _originalPulseTransformOrigin;
        _configuredPulseTarget = null;
        _originalPulseTransform = null;
    }

    private void ResetTransforms()
    {
        _windowTransform.X = 0;
        _pulseTransform.ScaleX = 1;
        _pulseTransform.ScaleY = 1;
    }

    private static double Interpolate(Frame[] frames, double progress)
    {
        for (var index = 1; index < frames.Length; index++)
        {
            var next = frames[index];
            if (progress > next.Cue) continue;

            var previous = frames[index - 1];
            var frameProgress = (progress - previous.Cue) / (next.Cue - previous.Cue);
            return previous.Value + (next.Value - previous.Value) * frameProgress;
        }

        return frames[^1].Value;
    }

    private readonly record struct Frame(double Cue, double Value);
}
