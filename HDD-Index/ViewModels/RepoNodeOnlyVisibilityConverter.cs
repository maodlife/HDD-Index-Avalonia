using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace HDD_Index.ViewModels;

/// <summary>
/// 仅对 RepoNodeVM 显示（用于复用同一个 ContextMenu 时隐藏 FileNode 的菜单项）。
/// </summary>
public class RepoNodeOnlyVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is RepoNodeVM;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

