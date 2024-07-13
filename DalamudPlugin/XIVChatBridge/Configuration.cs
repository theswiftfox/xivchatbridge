using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace XIVChatBridge;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public int Port { get; set; } = 9876;

    public bool AllowNonLocalAccess = false;

    public int MessageLimit { get; set; } = 5000;
    public bool PersistMessages = true;

    // the below exist just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
