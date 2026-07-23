using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using System;
using System.Linq;

namespace CodeWF.Log.Avalonia;

internal static class LogNotificationResources
{
    private static readonly Uri BaseUri =
        new("avares://CodeWF.Log.Avalonia/Notifications/Styles/");
    private static readonly Uri ResourceUri =
        new("avares://CodeWF.Log.Avalonia/Notifications/Styles/NotificationResources.axaml");
    private static readonly object SyncRoot = new();

    public static void EnsureRegistered()
    {
        var application = Application.Current;
        if (application == null)
        {
            return;
        }

        lock (SyncRoot)
        {
            var dictionaries = application.Resources.MergedDictionaries;
            if (dictionaries.OfType<ResourceInclude>().Any(include => Equals(include.Source, ResourceUri))) return;

            dictionaries.Insert(0, new ResourceInclude(BaseUri)
            {
                Source = ResourceUri
            });
        }
    }
}
