using Dalamud.Game.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XIVChatBridge
{
    internal class ChatMessage
    {
        private DateTime timestamp = DateTime.Now;
        public DateTime Timestamp { get {  return timestamp; } }
        public XivChatType Type { get; }
        public string SenderName { get; }
        public string Text { get; }

        public ChatMessage(XivChatType type, string SenderName, string Text)
        {
            this.Type = type;
            this.SenderName = SenderName;
            this.Text = Text;
        }
    }

    internal class NewMessageRequest
    {
        public InputChannel Type { get; }
        public string Text { get; }


        public NewMessageRequest(InputChannel type, string text)
        {
            this.Type = type;
            this.Text = text;
        }
    }
}
