// See https://aka.ms/new-console-template for more information

using System.Text.Json;
using ConvertOldJsonFiles;

Console.WriteLine("Start Convert!");

string oldRepoPath =
    "/Users/maodlife/Documents/HDD-Index/JsonFiles/RepoTreeData.txt";
string[] hddFileNames =
{
    "/Users/maodlife/Documents/HDD-Index/JsonFiles/HDD1BDRip.txt",
    "/Users/maodlife/Documents/HDD-Index/JsonFiles/HDD3BDRip.txt",
    "/Users/maodlife/Documents/HDD-Index/JsonFiles/HDD4BDRip.txt",
};
string configFolderPath = "/Users/maodlife/Documents/HDD-Index/config";

if (!Directory.Exists(configFolderPath))
{
    Directory.CreateDirectory(configFolderPath);
}

var oldRepoJsonStr = File.ReadAllText(oldRepoPath);
var oldRepoRoot = JsonSerializer.Deserialize<OldRepoNode>(oldRepoJsonStr);
// Console.WriteLine(oldRepoRoot.name);
var newRepoRoot = Utils.ConvertRepoNode(oldRepoRoot);
var newRepoJsonStr = JsonSerializer.Serialize(newRepoRoot,
    new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    });
var newRepoPath = Path.Combine(configFolderPath, Path.GetFileName(oldRepoPath));
if (!File.Exists(newRepoPath))
{
    File.Create(newRepoPath).Close();
}
await File.WriteAllTextAsync(newRepoPath, newRepoJsonStr);

foreach (var hddFileName in hddFileNames)
{
    var oldFileRoot =
        JsonSerializer.Deserialize<OldFileNode>(File.ReadAllText(hddFileName));
    var newFileRoot = Utils.ConvertFileNode(oldFileRoot);
    var newFileJsonStr = JsonSerializer.Serialize(newFileRoot,
        new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    var newFilePath = Path.Combine(configFolderPath, Path.GetFileName(hddFileName));
    if (!File.Exists(newFilePath))
    {
        File.Create(newFilePath).Close();
    }
    await File.WriteAllTextAsync(newFilePath, newFileJsonStr);
}