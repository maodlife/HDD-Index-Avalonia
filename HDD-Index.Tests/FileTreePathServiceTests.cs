using HDD_Index.Services;

namespace HDD_Index.Tests;

public class FileTreePathServiceTests
{
    [Theory]
    [InlineData("DiskA.json", "DiskA")]
    [InlineData(@"Nested\DiskA.json", "DiskA")]
    [InlineData("Nested/DiskA.json", "DiskA")]
    [InlineData(@"Nested\Indexes/DiskA.json", "DiskA")]
    [InlineData(@"C:\Index\DiskA.json", "DiskA")]
    [InlineData("/index/DiskA.json", "DiskA")]
    [InlineData(@"Nested\Disk.A.json", "Disk.A")]
    public void GetFileNameWithoutExtension_AcceptsPortableAndLegacyIndexPaths(
        string configuredPath,
        string expectedLabel)
    {
        var service = new FileTreePathService();

        Assert.Equal(expectedLabel, service.GetFileNameWithoutExtension(configuredPath));
    }

    [Fact]
    public void GetRelativePath_UsesForwardSlashesForStoredIndexPaths()
    {
        var dataDirectory = Path.Combine(Path.GetTempPath(), "Index");
        var indexPath = Path.Combine(dataDirectory, "Nested", "DiskA.json");
        var service = new FileTreePathService();

        Assert.Equal("Nested/DiskA.json", service.GetRelativePath(dataDirectory, indexPath));
    }

    [Theory]
    [InlineData(@"Nested\DiskA.json", "Nested/DiskA.json", true)]
    [InlineData(@"Nested\DiskA.json", "nested/diska.json", true)]
    [InlineData(@"Nested\DiskA.json", "Other/DiskA.json", false)]
    public void AreIndexPathsEqual_ComparesFullConfiguredPathsAcrossSeparators(
        string firstPath,
        string secondPath,
        bool expected)
    {
        var service = new FileTreePathService();

        Assert.Equal(expected, service.AreIndexPathsEqual(firstPath, secondPath));
    }

    [Theory]
    [InlineData("Disk/A")]
    [InlineData(@"Disk\A")]
    public void ContainsInvalidFileNameChars_RejectsBothConfiguredPathSeparators(string label)
    {
        var service = new FileTreePathService();

        Assert.True(service.ContainsInvalidFileNameChars(label));
        Assert.False(service.ContainsInvalidFileNameChars("Archive"));
    }
}
