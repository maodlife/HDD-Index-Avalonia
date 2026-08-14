using System;
using System.IO;
using System.Text;

namespace HDD_Index.Services;

public interface IAtomicFileWriter
{
    void WriteAllText(string filePath, string content);
}

public interface IAtomicFileSystem
{
    void CreateDirectory(string directoryPath);

    void WriteAllTextAndFlush(string filePath, string content);

    bool FileExists(string filePath);

    void ReplaceFile(string sourceFilePath, string destinationFilePath);

    void MoveFile(string sourceFilePath, string destinationFilePath);

    void DeleteFile(string filePath);
}

public sealed class AtomicFileWriter : IAtomicFileWriter
{
    private readonly IAtomicFileSystem _fileSystem;

    public AtomicFileWriter()
        : this(new PhysicalAtomicFileSystem())
    {
    }

    public AtomicFileWriter(IAtomicFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public void WriteAllText(string filePath, string content)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("文件路径不能为空。", nameof(filePath));

        var fullPath = Path.GetFullPath(filePath);
        var directoryPath = Path.GetDirectoryName(fullPath)
                            ?? throw new InvalidOperationException(
                                $"无法确定文件所在目录：{fullPath}");
        _fileSystem.CreateDirectory(directoryPath);

        var tempFilePath = Path.Combine(
            directoryPath,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            _fileSystem.WriteAllTextAndFlush(tempFilePath, content);
            if (_fileSystem.FileExists(fullPath))
                _fileSystem.ReplaceFile(tempFilePath, fullPath);
            else
                _fileSystem.MoveFile(tempFilePath, fullPath);
        }
        catch
        {
            try
            {
                if (_fileSystem.FileExists(tempFilePath))
                    _fileSystem.DeleteFile(tempFilePath);
            }
            catch
            {
                // Preserve the publishing failure. A stale temporary file is safer
                // than hiding the error that prevented the target from being saved.
            }

            throw;
        }
    }
}

public sealed class PhysicalAtomicFileSystem : IAtomicFileSystem
{
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);

    public void CreateDirectory(string directoryPath)
    {
        Directory.CreateDirectory(directoryPath);
    }

    public void WriteAllTextAndFlush(string filePath, string content)
    {
        using var stream = new FileStream(
            filePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.WriteThrough);
        using (var writer = new StreamWriter(
                   stream,
                   Utf8WithoutBom,
                   bufferSize: 4096,
                   leaveOpen: true))
        {
            writer.Write(content);
            writer.Flush();
        }

        stream.Flush(flushToDisk: true);
    }

    public bool FileExists(string filePath)
    {
        return File.Exists(filePath);
    }

    public void ReplaceFile(string sourceFilePath, string destinationFilePath)
    {
        File.Replace(sourceFilePath, destinationFilePath, null);
    }

    public void MoveFile(string sourceFilePath, string destinationFilePath)
    {
        File.Move(sourceFilePath, destinationFilePath);
    }

    public void DeleteFile(string filePath)
    {
        File.Delete(filePath);
    }
}
