using System;
using System.IO;
using System.Text.Json;
using HDD_Index.Models;

namespace HDD_Index.Services;

public class AppConfigService
{
    public string GetDefaultConfigPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "HDD-Index/config.json");
    }

    public AppConfig LoadDefault()
    {
        return Load(GetDefaultConfigPath());
    }

    public AppConfig Load(string configPath)
    {
        var content = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<AppConfig>(content);
        if (config == null)
            throw new InvalidOperationException($"无法读取配置文件: {configPath}");

        return config;
    }
}
