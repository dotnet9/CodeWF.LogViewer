using Avalonia.Media;

namespace CodeWF.Log.Avalonia;

internal sealed record LogViewPalette(
    IBrush TimestampForeground,
    IBrush ContentForeground,
    IBrush TraceForeground,
    IBrush InformationForeground,
    IBrush WarningForeground,
    IBrush ErrorForeground)
{
    internal static LogViewPalette Light { get; } = new(
        new SolidColorBrush(Color.Parse("#8C8C8C")),
        new SolidColorBrush(Color.Parse("#262626")),
        new SolidColorBrush(Color.Parse("#1890FF")),
        new SolidColorBrush(Color.Parse("#52C41A")),
        new SolidColorBrush(Color.Parse("#FAAD14")),
        new SolidColorBrush(Color.Parse("#FF4D4F")));
}
