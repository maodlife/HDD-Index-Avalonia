using System;
using System.Collections.Generic;
using System.Linq;

namespace HDD_Index.Services;

public class DirtyJsonFileTracker
{
    private readonly Dictionary<string, string> _filePathsByDiskLabel = new();
    private readonly HashSet<string> _dirtyFilePaths = new(StringComparer.OrdinalIgnoreCase);
    private string _appConfigPath = string.Empty;
    private string _repoFilePath = string.Empty;

    public bool HasDirtyFiles => _dirtyFilePaths.Count > 0;

    public void SetAppConfigPath(string appConfigPath)
    {
        _appConfigPath = appConfigPath;
    }

    public void SetRepoFilePath(string repoFilePath)
    {
        _repoFilePath = repoFilePath;
    }

    public void SetFileNodePath(string diskLabel, string jsonFilePath)
    {
        if (string.IsNullOrWhiteSpace(diskLabel)
            || string.IsNullOrWhiteSpace(jsonFilePath))
        {
            return;
        }

        _filePathsByDiskLabel[diskLabel] = jsonFilePath;
    }

    public void MarkAppConfigDirty()
    {
        if (!string.IsNullOrWhiteSpace(_appConfigPath))
            _dirtyFilePaths.Add(_appConfigPath);
    }

    public void MarkRepoDirty()
    {
        if (!string.IsNullOrWhiteSpace(_repoFilePath))
            _dirtyFilePaths.Add(_repoFilePath);
    }

    public void MarkFileDirty(string diskLabel)
    {
        if (_filePathsByDiskLabel.TryGetValue(diskLabel, out var filePath))
            _dirtyFilePaths.Add(filePath);
    }

    public void MarkAllFileNodesDirty()
    {
        foreach (var filePath in _filePathsByDiskLabel.Values)
            _dirtyFilePaths.Add(filePath);
    }

    public IReadOnlyList<string> GetDirtyFilePaths()
    {
        return _dirtyFilePaths.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public void ClearDirtyFiles(IEnumerable<string> filePaths)
    {
        foreach (var filePath in filePaths)
            _dirtyFilePaths.Remove(filePath);
    }
}
