using System.Collections.Generic;

namespace HDD_Index.Models;

public class AppConfig
{
    public string JsonFilePath { get; set; } = string.Empty;
    public string RepoFileName { get; set; } = string.Empty;
    public List<FileDataFileConfig> FileDataFiles { get; set; } = new();
}

public class FileDataFileConfig
{
    public string JsonFilePath { get; set; } = string.Empty;
    public string LocalFolderPath { get; set; } = string.Empty;
}
