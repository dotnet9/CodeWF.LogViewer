using Avalonia.Controls;
using Avalonia.Controls.Presenters;

namespace CodeWF.LogViewer.Avalonia.Extensions;

public static class ScrollViewExtensions
{
    public static bool IsAtVerticalBottom(this ScrollViewer scrollView, double tolerance = 1.0)
    {
        if (scrollView == null) return false;
        var scp = scrollView.FindVisualDescendant<ScrollContentPresenter>();
        if (scp == null) return false;

        double verticalOffset = scp.Offset.Y;
        double viewportHeight = scp.Viewport.Height;
        double extentHeight = scp.Extent.Height;
        double totalScrollable = extentHeight - viewportHeight;

        return totalScrollable <= 0 || verticalOffset >= totalScrollable - tolerance;
    }
}