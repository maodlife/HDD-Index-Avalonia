using System.Threading.Tasks;
using Avalonia.Controls;
using HDD_Index.Application.ExternalInteractions;
using HDD_Index.Views;

namespace HDD_Index.Adapters;

public sealed class AvaloniaUserInteraction : IUserInteraction
{
    private readonly Window _owner;

    public AvaloniaUserInteraction(Window owner)
    {
        _owner = owner;
    }

    public async Task ShowMessageAsync(MessageRequest request)
    {
        var (width, height) = request.DisplayKind switch
        {
            MessageDisplayKind.Detailed => (720d, 480d),
            _ => (400d, 150d),
        };
        var dialog = new MessageDialog(request.Message)
        {
            Title = "提示",
            Width = width,
            Height = height,
        };
        await dialog.ShowDialog(_owner);
    }

    public async Task<bool> ConfirmAsync(ConfirmationRequest request)
    {
        var dialog = new ConfirmMessageDialog(request.Message)
        {
            Title = request.Title,
            Width = 520,
            Height = 260,
        };
        return await dialog.ShowDialog<bool>(_owner);
    }
}
