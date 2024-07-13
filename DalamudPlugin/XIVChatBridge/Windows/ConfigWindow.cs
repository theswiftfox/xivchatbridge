using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace XIVChatBridge.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Configuration configuration;

    public ConfigWindow(Plugin plugin) : base("XIVChatBridge Config###CFG")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse;

        Size = new Vector2(300, 230);
        SizeCondition = ImGuiCond.Always;

        configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        Flags &= ~ImGuiWindowFlags.NoMove;
    }

    public override void Draw()
    {
        var label = "Server Settings";
        ImGuiHelper.Center(label);
        ImGui.Text(label);
        ImGui.Spacing();
        // can't ref a property, so use a local copy
        {
            var configValue = configuration.Port;
            if (ImGui.InputInt("Port", ref configValue, 0))
            {
                configuration.Port = configValue;
                // can save immediately on change, if you don't want to provide a "Save and Close" button
                configuration.Save();
            }
        }

        {
            var configValue = configuration.AllowNonLocalAccess;
            if (ImGui.Checkbox("Allow non-local access", ref configValue))
            {
                configuration.AllowNonLocalAccess = configValue;
                // can save immediately on change, if you don't want to provide a "Save and Close" button
                configuration.Save();
            }
        }

        ImGui.Dummy(new Vector2(0, 15));

        label = "Message Persistence";
        ImGuiHelper.Center(label);
        ImGui.Text(label);
        {
            var configValue = configuration.PersistMessages;
            if (ImGui.Checkbox("Enable", ref configValue))
            {
                configuration.PersistMessages = configValue;
                // can save immediately on change, if you don't want to provide a "Save and Close" button
                configuration.Save();
            }
        }

        {
            ImGui.Text("Maxium number of chat messages to be stored");
            var configValue = configuration.MessageLimit;
            if (ImGui.InputInt("", ref configValue, 0))
            {
                configuration.MessageLimit = configValue;
                // can save immediately on change, if you don't want to provide a "Save and Close" button
                configuration.Save();
            }
        }
    }
}
