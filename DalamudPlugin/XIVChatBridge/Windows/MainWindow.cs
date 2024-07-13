using System;
using System.Numerics;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ImGuiNET;

namespace XIVChatBridge.Windows
{
    internal class MainWindow : Window, IDisposable
    {
        private Plugin Plugin;

        public MainWindow(Plugin plugin) : base("XIVChatBridge#MainWIndow", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse;

            Size = new Vector2(260, 170);

            Plugin = plugin;
        }

        public void Dispose() { }

        public override void Draw()
        {
            var url = $"http://localhost:{Plugin.Configuration.Port}";

            ImGui.Text($"Server is running on {url}/");

            ImGui.Spacing();

            var label = "Open UI";
            ImGuiHelper.Center(label);
            if (ImGui.Button(label))
            {
                System.Diagnostics.Process.Start("explorer", url);
            }

            ImGui.Dummy(new Vector2(0, 15));

            var label1 = "Show Settings";
            var label2 = "Select Channels";

            ImGuiHelper.Center(label1, label2);
            if (ImGui.Button(label1))
            {
                Plugin.ToggleConfigUI();
            }
            ImGui.SameLine();
            if (ImGui.Button(label2))
            {
                Plugin.ToggleChannelSelection();
            }

            ImGui.Dummy(new Vector2(0, 10));
            if (ImGui.Button("Reload Plugin"))
            {
                Plugin.Reload();
            }
        }
    }
}
