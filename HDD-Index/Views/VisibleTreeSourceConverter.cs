using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace HDD_Index.Views;

/// <summary>
/// TreeDataGrid sources contain measured row state. Only the visible workspace
/// may attach to a source, otherwise hidden grids corrupt its virtualized layout.
/// Selection and expansion remain on the original source while views switch.
/// </summary>
public sealed class VisibleTreeSourceConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        return values.Count == 2 && values[1] is true ? values[0] : null;
    }
}
