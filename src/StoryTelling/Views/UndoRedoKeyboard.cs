using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using StoryTelling.ViewModels;

namespace StoryTelling.Views;

internal static class UndoRedoKeyboard
{
    public static void Attach(Window window) =>
        window.AddHandler(InputElement.KeyDownEvent, (_, e) => Handle(window, e), RoutingStrategies.Tunnel);

    private static void Handle(Window window, KeyEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control) || window.DataContext is not IUndoRedoHost host)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Z when e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                if (host.RedoCommand.CanExecute(null))
                {
                    host.RedoCommand.Execute(null);
                    e.Handled = true;
                }

                break;
            case Key.Z:
                if (host.UndoCommand.CanExecute(null))
                {
                    host.UndoCommand.Execute(null);
                    e.Handled = true;
                }

                break;
            case Key.Y:
                if (host.RedoCommand.CanExecute(null))
                {
                    host.RedoCommand.Execute(null);
                    e.Handled = true;
                }

                break;
        }
    }
}
