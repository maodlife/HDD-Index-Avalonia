using Avalonia.Platform;

namespace HDD_Index.Tests;

public class AppIconTests
{
    [Fact]
    public void ApplicationIcon_EmbedsAllDesktopSizes()
    {
        var assets = new StandardAssetLoader();
        assets.SetDefaultAssembly(typeof(App).Assembly);
        using var stream = assets.Open(new Uri("avares://HDD-Index/Assets/hdd-index.ico"));
        using var reader = new BinaryReader(stream);

        Assert.Equal(0, reader.ReadUInt16());
        Assert.Equal(1, reader.ReadUInt16());
        var count = reader.ReadUInt16();
        var sizes = new List<int>();
        for (var i = 0; i < count; i++)
        {
            var width = reader.ReadByte();
            var height = reader.ReadByte();
            Assert.Equal(width, height);
            sizes.Add(width == 0 ? 256 : width);
            Assert.Equal(14, reader.ReadBytes(14).Length);
        }

        Assert.Equal(new[] { 16, 24, 32, 48, 64, 128, 256 }, sizes);
    }

    [Fact]
    public void ToolbarIcon_IsAnEmbeddedPng()
    {
        var assets = new StandardAssetLoader();
        assets.SetDefaultAssembly(typeof(App).Assembly);
        using var stream = assets.Open(new Uri("avares://HDD-Index/Assets/hdd-index.png"));
        using var reader = new BinaryReader(stream);

        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, reader.ReadBytes(8));
    }
}
