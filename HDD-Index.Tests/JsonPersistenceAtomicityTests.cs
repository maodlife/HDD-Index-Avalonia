using HDD_Index.Models;
using HDD_Index.Services;

namespace HDD_Index.Tests;

public class JsonPersistenceAtomicityTests
{
    [Fact]
    public void AppConfigService_DelegatesSaveToAtomicWriter()
    {
        var writer = new RecordingAtomicFileWriter();
        var service = new AppConfigService(writer);

        service.Save("C:\\Data\\config.json", new AppConfig
        {
            JsonFilePath = "D:\\Index",
            RepoFileName = "repo.json",
        });

        var write = Assert.Single(writer.Writes);
        Assert.Equal("C:\\Data\\config.json", write.FilePath);
        Assert.Contains("\"RepoFileName\": \"repo.json\"", write.Content);
    }

    [Fact]
    public void TreeDataStore_DelegatesRepositoryAndIndexSavesToAtomicWriter()
    {
        var writer = new RecordingAtomicFileWriter();
        var service = new TreeDataStore(writer);
        var appConfig = new AppConfig
        {
            JsonFilePath = "C:\\Data",
            RepoFileName = "repo.json",
        };

        service.SaveRepoRoot(appConfig, TestTreeFactory.Repo("Repo"));
        service.SaveFileData(new FileData
        {
            DiskLabel = "DiskA",
            JsonFilePath = "C:\\Data\\disk-a.json",
            FileNodeRoot = TestTreeFactory.File("DiskA"),
        });

        Assert.Equal(2, writer.Writes.Count);
        Assert.Equal("C:\\Data\\repo.json", writer.Writes[0].FilePath);
        Assert.Equal("C:\\Data\\disk-a.json", writer.Writes[1].FilePath);
    }

    private sealed class RecordingAtomicFileWriter : IAtomicFileWriter
    {
        public List<(string FilePath, string Content)> Writes { get; } = [];

        public void WriteAllText(string filePath, string content)
        {
            Writes.Add((filePath, content));
        }
    }
}
