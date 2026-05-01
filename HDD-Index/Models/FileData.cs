namespace HDD_Index.Models;

public class FileData
{
    public string DiskLabel { get; set; }
    public string LocalFolderPath { get; set; } = string.Empty;
    public FileNode FileNodeRoot { get; set; }
}