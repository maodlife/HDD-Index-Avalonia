namespace HDD_Index.Models;

public class FileData
{
    public required string DiskLabel { get; set; }
    public string LocalFolderPath { get; set; } = string.Empty;
    public string JsonFilePath { get; set; } = string.Empty;
    public required FileNode FileNodeRoot { get; set; }
}
