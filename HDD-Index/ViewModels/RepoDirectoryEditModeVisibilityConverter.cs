using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace HDD_Index.ViewModels;

/// <summary>
/// 仅在编辑页对目录类型的 RepoNodeVM 显示。
/// </summary>
public class RepoDirectoryEditModeVisibilityConverter : IMultiValueConverter
{
    public object Convert(
        IList<object?> values,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        return values.Count >= 2
               && values[0] is RepoNodeVM { IsDirectory: true }
               && values[1] is int tabIndex
               && tabIndex == 1;
    }
}
