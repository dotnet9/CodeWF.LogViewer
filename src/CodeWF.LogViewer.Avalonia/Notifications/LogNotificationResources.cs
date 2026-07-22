using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using System;
using System.Runtime.CompilerServices;

namespace CodeWF.LogViewer.Avalonia;

internal static class LogNotificationResources
{
    private static readonly ConditionalWeakTable<Application, object> RegisteredApplications = new();
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
            if (RegisteredApplications.TryGetValue(application, out _))
            {
                return;
            }

            application.Resources.MergedDictionaries.Insert(0, new ResourceInclude(
                new Uri("avares://CodeWF.LogViewer.Avalonia/Notifications/Styles/"))
            {
                Source = new Uri(
                    "avares://CodeWF.LogViewer.Avalonia/Notifications/Styles/NotificationResources.axaml")
            });
            RegisteredApplications.Add(application, new object());
        }
    }
}
