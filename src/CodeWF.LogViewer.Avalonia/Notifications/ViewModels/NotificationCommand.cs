using System;
using System.Windows.Input;

namespace CodeWF.LogViewer.Avalonia.Notifications.ViewModels;

internal sealed class NotificationCommand(Action execute) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => execute();
}
