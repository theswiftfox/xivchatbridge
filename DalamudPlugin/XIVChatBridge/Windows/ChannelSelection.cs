using System;
using System.Numerics;
using Dalamud.Game.Text;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace XIVChatBridge.Windows
{
    internal class ChannelSelection : Window, IDisposable
    {
        private Configuration configuration;

        public ChannelSelection(Plugin plugin) : base("XIVChatBridge: Channel Selection###CFGChannel")
        {
            Flags = ImGuiWindowFlags.NoCollapse;

            Size = new Vector2(220, 1100f);
            SizeCondition = ImGuiCond.FirstUseEver;

            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(220f, 300f),
                MaximumSize = new Vector2(220f, 1200f),
            };

            configuration = plugin.Configuration;
        }

        public void Dispose() { }

        public override void PreDraw()
        {
            Flags &= ~ImGuiWindowFlags.NoMove;
        }

        public override void Draw()
        {           
            foreach (var chatType in Enum.GetValues<XivChatType>())
            {
                var configValue = configuration.enabledChatTypes.Contains(chatType);
                if (ImGui.Checkbox(Enum.GetName<XivChatType>(chatType), ref configValue))
                {
                    configuration.enabledChatTypes.Add(chatType);
                    // can save immediately on change, if you don't want to provide a "Save and Close" button
                    configuration.Save();
                }
            }
        }
    }
}
