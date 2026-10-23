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
        if (request.DisplayKind == MessageDisplayKind.Informational && _owner is MainWindow mainWindow)
        {
            mainWindow.ShowInformation(request.Message);
            return;
        }

        var dialog = new MessageDialog(request.Message, "请检查操作结果")
        {
            Title = "操作提示",
        };
        if (request.DisplayKind == MessageDisplayKind.Detailed)
        {
            dialog.Width = 720;
            dialog.Height = 480;
        }
        await dialog.ShowDialog(_owner);
    }

    public async Task<bool> ConfirmAsync(ConfirmationRequest request)
    {
        var dialog = new ConfirmMessageDialog(request.Message, request.Title, request.ConfirmLabel, request.IsDestructive)
        {
            Title = request.Title,
        };
        return await dialog.ShowDialog<bool>(_owner);
    }
}
