using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XIVChatBridge.Windows
{
    internal static class ImGuiHelper
    {
        // source: https://github.com/ocornut/imgui/discussions/3862#discussioncomment-422097

        internal static void Center(params string[] labels)
        {
            ImGuiStylePtr style = ImGui.GetStyle();
            var labelsize = labels.Select(label => ImGui.CalcTextSize(label).X).Sum();
            var size = labelsize + (style.FramePadding.X * 2.0f);
            var avail = ImGui.GetContentRegionAvail().X;

            var off = (avail - size) * 0.5f;
            if (off > 0.0f)
            {
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + off);
            }
        }
    }
}
