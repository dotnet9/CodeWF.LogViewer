using Avalonia.Media;
using Avalonia.Styling;
using CodeWF.Log.Avalonia;
using Xunit;

namespace CodeWF.Log.Avalonia.Tests;

public sealed class CodeWFLogThemeTests
{
    [Theory]
    [MemberData(nameof(ThemeVariants))]
    public void Theme_ProvidesEveryLogViewBrush(ThemeVariant variant)
    {
        var theme = new CodeWFLogTheme();
        var keys = new[]
        {
            LogViewResourceKeys.TimestampForeground,
            LogViewResourceKeys.ContentForeground,
            LogViewResourceKeys.TraceForeground,
            LogViewResourceKeys.InformationForeground,
            LogViewResourceKeys.WarningForeground,
            LogViewResourceKeys.ErrorForeground
        };

        foreach (var key in keys)
        {
            Assert.True(theme.TryGetResource(key, variant, out var resource));
            Assert.IsAssignableFrom<IBrush>(resource);
        }
    }

    public static TheoryData<ThemeVariant> ThemeVariants => new()
    {
        ThemeVariant.Light,
        ThemeVariant.Dark,
        CodeWFLogTheme.HighContrast
    };
}
