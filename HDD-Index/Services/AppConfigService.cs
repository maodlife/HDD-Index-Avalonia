using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using HDD_Index.Models;

namespace HDD_Index.Services;

public class AppConfigService
{
    private readonly IAtomicFileWriter _fileWriter;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public AppConfigService()
        : this(new AtomicFileWriter())
    {
    }

    public AppConfigService(IAtomicFileWriter fileWriter)
    {
        _fileWriter = fileWriter ?? throw new ArgumentNullException(nameof(fileWriter));
    }

    public string GetDefaultConfigPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "HDD-Index/config.json");
    }

    public AppConfig Load(string configPath)
    {
        var content = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<AppConfig>(content);
        if (config == null)
            throw new InvalidOperationException($"无法读取配置文件: {configPath}");

        return config;
    }

    public void Save(string configPath, AppConfig appConfig)
    {
        var json = JsonSerializer.Serialize(appConfig, JsonOptions);
        _fileWriter.WriteAllText(configPath, json);
    }
}
