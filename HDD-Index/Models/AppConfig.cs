using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace HDD_Index.Models;

public class AppConfig
{
    public string JsonFilePath { get; set; } = string.Empty;
    public string RepoFileName { get; set; } = string.Empty;
    public List<FileDataFileConfig> FileDataFiles { get; set; } = new();
    [JsonIgnore] public bool IsDirty { get; set; }
}

public class FileDataFileConfig
{
    public string JsonFilePath { get; set; } = string.Empty;
    public string LocalFolderPath { get; set; } = string.Empty;
}
