using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using System;

namespace CodeWF.Log.Avalonia;

/// <summary>
/// Provides light, dark, and high-contrast resources for CodeWF log controls.
/// </summary>
public sealed partial class CodeWFLogTheme : Styles
{
    /// <summary>
    /// High-contrast dark theme variant for CodeWF log controls.
    /// </summary>
    public static ThemeVariant HighContrast { get; } = new("CodeWFLogHighContrast", ThemeVariant.Dark);

    public CodeWFLogTheme(IServiceProvider? serviceProvider = null) =>
        AvaloniaXamlLoader.Load(serviceProvider, this);
}
