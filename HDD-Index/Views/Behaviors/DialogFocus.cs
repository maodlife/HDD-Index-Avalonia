using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace HDD_Index.Views.Behaviors;

/// <summary>Give every dialog a keyboard target without focusing a destructive action.</summary>
public static class DialogFocus
{
    public static void Attach(Window window)
    {
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
