using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace HDD_Index.ViewModels;

/// <summary>
/// 仅对 FileNodeVM 显示，用于复用同一个 ContextMenu 时隐藏 RepoNode 菜单项。
/// </summary>
public class FileNodeOnlyVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is FileNodeVM;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
