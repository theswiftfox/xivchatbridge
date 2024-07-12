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
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(375, 330),
                MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
            };
            Plugin = plugin;
        }

        public void Dispose() { }

        public override void Draw()
        {
            ImGui.Text($"Server is running on http://localhost:{Plugin.Configuration.Port}/");

            if (ImGui.Button("Show Settings"))
            {
                Plugin.ToggleConfigUI();
            }

            ImGui.Spacing();

            if (ImGui.Button("Reload Plugin"))
            {
                Plugin.Reload();
            }
        }
    }
}
