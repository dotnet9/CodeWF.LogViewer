using Avalonia;
using System;
using System.Collections.Generic;
using System.Text;
using Avalonia.VisualTree;

namespace CodeWF.LogViewer.Avalonia.Extensions;

public static class VisualTreeExtensions
{
    public static T FindVisualDescendant<T>(this Visual visual) where T : Visual
    {
        if (visual == null) return null;
        foreach (var child in visual.GetVisualChildren())
        {
            if (child is T target) return target;
            var descendant = child.FindVisualDescendant<T>();
            if (descendant != null) return descendant;
        }
        return null;
    }
}