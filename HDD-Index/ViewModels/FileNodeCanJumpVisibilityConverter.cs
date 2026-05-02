using System;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;

namespace HDD_Index.ViewModels;

/// <summary>
/// 仅对声明了 RepoNode 路径的 FileNodeVM 显示跳转菜单项。
/// </summary>
public class FileNodeCanJumpVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is FileNodeVM fileNodeVM
               && fileNodeVM.FileNode.DeclareRepoNodeDatas
                   .Any(x => !string.IsNullOrWhiteSpace(x.RepoNodePath));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
