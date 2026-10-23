using System.Globalization;
using HDD_Index.Views;

namespace HDD_Index.Tests;

public class VisibleTreeSourceConverterTests
{
    [Fact]
    public void OnlyTheVisibleTreeAttachesToSharedSource()
    {
        var source = new object();
        var converter = new VisibleTreeSourceConverter();

        Assert.Same(source, converter.Convert([source, true], typeof(object), null, CultureInfo.InvariantCulture));
        Assert.Null(converter.Convert([source, false], typeof(object), null, CultureInfo.InvariantCulture));
        Assert.Null(converter.Convert([source], typeof(object), null, CultureInfo.InvariantCulture));
    }
}
