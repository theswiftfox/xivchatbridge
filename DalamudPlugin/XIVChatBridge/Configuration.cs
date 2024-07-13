using Dalamud.Configuration;
using Dalamud.Game.Text;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;

namespace XIVChatBridge;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public int Port { get; set; } = 9876;

    public bool AllowNonLocalAccess = false;

    public int MessageLimit { get; set; } = 5000;
    public bool PersistMessages = true;

    public HashSet<XivChatType> enabledChatTypes { get; set; } = [
        XivChatType.Say, 
        XivChatType.TellIncoming,
        XivChatType.TellOutgoing,
        XivChatType.FreeCompany, 
        XivChatType.Party, 
        XivChatType.CrossParty, 
        XivChatType.Alliance,
        XivChatType.Yell, 
        XivChatType.Shout,
        XivChatType.Ls1,
        XivChatType.Ls2,
        XivChatType.Ls3,
        XivChatType.Ls4,
        XivChatType.Ls5,
        XivChatType.Ls6,
        XivChatType.Ls7,
        XivChatType.Ls8,
        XivChatType.CrossLinkShell1,
        XivChatType.CrossLinkShell2,
        XivChatType.CrossLinkShell3,
        XivChatType.CrossLinkShell4,
        XivChatType.CrossLinkShell5,
        XivChatType.CrossLinkShell6,
        XivChatType.CrossLinkShell7,
        XivChatType.CrossLinkShell8,
    ];

    // the below exist just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
