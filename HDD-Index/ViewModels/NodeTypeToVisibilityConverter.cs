using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace HDD_Index.ViewModels;

public class NodeTypeToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is RepoNodeVM repoNodeVM && repoNodeVM.IsDirectory)
        {
            return true;
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
