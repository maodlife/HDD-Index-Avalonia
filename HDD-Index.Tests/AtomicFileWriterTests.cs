using HDD_Index.Services;

namespace HDD_Index.Tests;

public class AtomicFileWriterTests
{
    [Fact]
    public void WriteAllText_NewFileFlushesTemporaryFileThenMovesIt()
    {
        var fileSystem = new RecordingAtomicFileSystem();
        var writer = new AtomicFileWriter(fileSystem);
        var targetPath = Path.GetFullPath(Path.Combine("data", "repo.json"));

        writer.WriteAllText(targetPath, "new content");

        Assert.Equal("new content", fileSystem.Files[targetPath]);
        Assert.Single(fileSystem.FlushedPaths);
        Assert.Equal(
            Path.GetDirectoryName(targetPath),
            Path.GetDirectoryName(fileSystem.FlushedPaths.Single()));
        Assert.EndsWith(".tmp", fileSystem.FlushedPaths.Single());
        Assert.Equal((fileSystem.FlushedPaths.Single(), targetPath), fileSystem.Moves.Single());
        Assert.Empty(fileSystem.Replacements);
    }

    [Fact]
    public void WriteAllText_ExistingFileUsesAtomicReplacement()
    {
        var targetPath = Path.GetFullPath(Path.Combine("data", "repo.json"));
        var fileSystem = new RecordingAtomicFileSystem();
        fileSystem.Files[targetPath] = "old content";
        var writer = new AtomicFileWriter(fileSystem);

        writer.WriteAllText(targetPath, "new content");

        Assert.Equal("new content", fileSystem.Files[targetPath]);
        Assert.Equal((fileSystem.FlushedPaths.Single(), targetPath),
            fileSystem.Replacements.Single());
        Assert.Empty(fileSystem.Moves);
    }

    [Fact]
    public void WriteAllText_ReplacementFailureKeepsOriginalAndCleansTemporaryFile()
    {
        var targetPath = Path.GetFullPath(Path.Combine("data", "repo.json"));
        var fileSystem = new RecordingAtomicFileSystem
        {
            FailReplacement = true,
        };
        fileSystem.Files[targetPath] = "old content";
        var writer = new AtomicFileWriter(fileSystem);

        Assert.Throws<IOException>(() => writer.WriteAllText(targetPath, "new content"));

        Assert.Equal("old content", fileSystem.Files[targetPath]);
        var tempPath = fileSystem.FlushedPaths.Single();
        Assert.False(fileSystem.Files.ContainsKey(tempPath));
        Assert.Equal(tempPath, fileSystem.DeletedPaths.Single());
    }

    private sealed class RecordingAtomicFileSystem : IAtomicFileSystem
    {
        public Dictionary<string, string> Files { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public List<string> FlushedPaths { get; } = [];

        public List<(string Source, string Destination)> Replacements { get; } = [];

        public List<(string Source, string Destination)> Moves { get; } = [];

        public List<string> DeletedPaths { get; } = [];

        public bool FailReplacement { get; init; }

        public void CreateDirectory(string directoryPath)
        {
        }

        public void WriteAllTextAndFlush(string filePath, string content)
        {
            FlushedPaths.Add(filePath);
            Files.Add(filePath, content);
        }

        public bool FileExists(string filePath)
        {
            return Files.ContainsKey(filePath);
        }

        public void ReplaceFile(string sourceFilePath, string destinationFilePath)
        {
            Replacements.Add((sourceFilePath, destinationFilePath));
            if (FailReplacement)
                throw new IOException("Simulated replacement failure.");

            Files[destinationFilePath] = Files[sourceFilePath];
            Files.Remove(sourceFilePath);
        }

        public void MoveFile(string sourceFilePath, string destinationFilePath)
        {
            Moves.Add((sourceFilePath, destinationFilePath));
            Files.Add(destinationFilePath, Files[sourceFilePath]);
            Files.Remove(sourceFilePath);
        }

        public void DeleteFile(string filePath)
        {
            DeletedPaths.Add(filePath);
            Files.Remove(filePath);
        }
    }
}
