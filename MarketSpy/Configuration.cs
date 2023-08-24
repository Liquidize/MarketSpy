using System;
using System.Collections.Generic;
using System.IO;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace MarketSpy;

[Serializable]
public class Configuration : IPluginConfiguration
{
    // the below exist just to make saving less cumbersome
    [NonSerialized]
    private DalamudPluginInterface? PluginInterface;

    public string DatabasePath { get; set; } = "";

    public Dictionary<string, object> GraphConfig { get; set; } = new();

    public int Version { get; set; } = 0;


    public void Initialize(DalamudPluginInterface pluginInterface)
    {
        PluginInterface = pluginInterface;
        DatabasePath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.Parent.FullName, "market.db");
    }

    public T GetOrAddGraphOption<T>(string option, T defaultValue)
    {
        if (GraphConfig.ContainsKey(option))
        {
            Save();
            return (T)GraphConfig[option];
        }

        GraphConfig[option] = defaultValue;
        return defaultValue;
    }

    public void SetGraphOption<T>(string option, T value)
    {
        if (value != null)
        {
            GraphConfig[option] = value;
            Save();
        }
    }

    public void Save()
    {
        PluginInterface!.SavePluginConfig(this);
    }
}
