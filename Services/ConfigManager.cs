using System;
using System.IO;
using Dalamud.Plugin.Services;
using Newtonsoft.Json;
using VenueMapper.Models;

namespace VenueMapper.Services;

public class ConfigManager
{
    private readonly IPluginLog log;
    private readonly string configDirectory;
    private readonly string cacheFilePath;

    private volatile VenueConfig? config;
    public VenueConfig? Config { get => config; private set => config = value; }
    public DateTime? LastUpdated { get; private set; }

    public ConfigManager(IPluginLog log, string configDirectory)
    {
        this.log = log;
        this.configDirectory = configDirectory;
        cacheFilePath = Path.Combine(configDirectory, "venues_cache.json");

        Directory.CreateDirectory(configDirectory);
    }

    public void Load(string bundledResourcePath)
    {
        if (File.Exists(cacheFilePath))
        {
            if (TryLoadFromFile(cacheFilePath))
            {
                LastUpdated = File.GetLastWriteTimeUtc(cacheFilePath);
                return;
            }
        }

        if (TryLoadFromFile(bundledResourcePath))
        {
            LastUpdated = null;
        }
    }

    private bool TryLoadFromFile(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            var parsed = JsonConvert.DeserializeObject<VenueConfig>(json);
            if (parsed == null)
                return false;

            Config = parsed;
            return true;
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Failed to load venue config from {path}");
            return false;
        }
    }

    private readonly object cacheLock = new();

    public void SaveToCache(string json)
    {
        lock (cacheLock)
        {
            try
            {
                File.WriteAllText(cacheFilePath, json);
                var parsed = JsonConvert.DeserializeObject<VenueConfig>(json);
                if (parsed != null)
                {
                    Config = parsed;
                    LastUpdated = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                log.Error(ex, "Failed to save venue config cache");
            }
        }
    }

    public string CacheFilePath => cacheFilePath;
    public string ConfigDirectory => configDirectory;
}
