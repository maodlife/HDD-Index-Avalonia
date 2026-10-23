using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace HDD_Index.Views.Behaviors;

/// <summary>Give every dialog a keyboard target without focusing a destructive action.</summary>
public static class DialogFocus
{
    public static void Attach(Window window)
    {
        // Read-only multiline fields can consume Escape before the default
        // IsCancel handling. Route it to the same safe command as the button.
        window.AddHandler(InputElement.KeyDownEvent, (_, args) =>
        {
            if (args.Key != Key.Escape)
                return;

            var cancel = window.GetVisualDescendants().OfType<Button>()
                .FirstOrDefault(button => button.IsCancel && button.IsEffectivelyVisible && button.IsEffectivelyEnabled);
            if (cancel?.Command is { } command && command.CanExecute(cancel.CommandParameter))
            {
                command.Execute(cancel.CommandParameter);
                args.Handled = true;
            }
        }, RoutingStrategies.Tunnel);

        window.Opened += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            var input = window.GetVisualDescendants().OfType<TextBox>()
                .FirstOrDefault(control => control.IsVisible && control.IsEnabled && !control.IsReadOnly);
            if (input != null)
            {
                input.Focus();
                input.SelectAll();
                return;
            }

            var buttons = window.GetVisualDescendants().OfType<Button>()
                .Where(button => button.IsVisible && button.IsEnabled).ToArray();
            (buttons.FirstOrDefault(button => button.IsCancel) ?? buttons.FirstOrDefault())?.Focus();
        }, DispatcherPriority.Loaded);
    }
}
