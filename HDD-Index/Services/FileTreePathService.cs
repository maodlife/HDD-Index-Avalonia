using System.IO;
using HDD_Index.Application.FileTrees;

namespace HDD_Index.Services;

public sealed class FileTreePathService : IFileTreePathService
{
    public bool ContainsInvalidFileNameChars(string fileName)
    {
        return fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
               || fileName.Contains('\\');
    }

    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    public string Combine(string firstPath, string secondPath)
    {
        return Path.Combine(firstPath, secondPath);
    }

    public string GetRelativePath(string relativeTo, string path)
    {
        return Path.GetRelativePath(relativeTo, path)
            .Replace(Path.DirectorySeparatorChar, '/');
    }

    public string GetFileNameWithoutExtension(string path)
    {
        return ConfiguredIndexPath.GetFileNameWithoutExtension(path);
    }

    public bool AreIndexPathsEqual(string firstPath, string secondPath)
    {
        return ConfiguredIndexPath.AreEqual(firstPath, secondPath);
    }
}
