using Dalamud.Game.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.Json;
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

    internal static class ChatMessageSerializer
    {
        internal static List<ChatMessage>? deserialize(FileInfo inputfile)
        {
            if (!inputfile.Exists) return null;

            string data = inputfile.OpenText().ReadToEnd();
            if (data.Length == 0) {  return null; }
            return JsonSerializer.Deserialize<List<ChatMessage>>(data);
        }

        internal static void serialize(List<ChatMessage> list, FileInfo outputfile)
        {
            var result = JsonSerializer.SerializeToUtf8Bytes(list, new JsonSerializerOptions { WriteIndented = false });
            using (var writer = outputfile.getWriter())
            {
                writer.Write(result, 0, result.Length);
            }
        }

        private static FileStream getWriter(this FileInfo file)
        {
            if (file.Exists)
            {
                file.Delete();
            }
            return file.Create();
        }
    }
}
