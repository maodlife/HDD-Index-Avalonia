using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace HDD_Index.ViewModels;

/// <summary>
/// 仅对目录类型的 FileNodeVM 显示。
/// </summary>
public class FileDirectoryNodeOnlyVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is FileNodeVM { IsDirectory: true };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
