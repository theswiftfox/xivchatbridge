using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace XIVChatBridge { 

    internal class GameFunctions : IDisposable
    {
        private static class Signatures
        {
            internal const string ProcessChat = "48 89 5C 24 ?? 57 48 83 EC 20 48 8B FA 48 8B D9 45 84 C9";

            internal const string Input = "E8 ?? ?? ?? ?? 4D 8B 47 18 84 C0";

            internal const string InputAfk = "E8 ?? ?? ?? ?? 41 83 7F ?? ?? 4C 8D 2D";

            internal const string Channel = "E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 85 D2 BB";

            internal const string ChannelCommand = "E8 ?? ?? ?? ?? 0F B7 44 37";
        }

        private delegate void EasierProcessChatBoxDelegate(nint uiModule, nint message, nint unused, byte a4);

        private delegate IntPtr ChannelChangeCommandDelegate(IntPtr a1, int inputChannel, uint linkshellIdx, IntPtr tellTarget, char canChangeChannel);

        private delegate byte ChatChannelChangeDelegate(IntPtr a1, uint channel);

        private delegate byte InputDelegate(nint a1);

        private delegate byte InputAfkDelegate();

        [Signature(Signatures.ProcessChat)]
        private readonly EasierProcessChatBoxDelegate? easierProcessChatBox;

        [Signature(Signatures.ChannelCommand)]
        private readonly ChannelChangeCommandDelegate? channelChangeCommand;

        [Signature(Signatures.Channel, DetourName = "changeChatChannelDetour")]
        private readonly Hook<ChatChannelChangeDelegate>? chatChannelChangeHook;

        [Signature(Signatures.Input, DetourName = "inputDetour")]
        private readonly Hook<InputDelegate>? isInputHook;

        [Signature(Signatures.InputAfk, DetourName = "inputAfkDetour")]
        private readonly Hook<InputAfkDelegate>? isInputAfkHook;

        private IntPtr chatManager = IntPtr.Zero;

        private IntPtr emptyXivString;

        private InputChannel currentChannel;

        [Flags]
        private enum InputState
        {
            None = 0,
            Normal = 1,
            Afk = 2
        }
        private InputState HadInput { get; set; }

        internal unsafe GameFunctions()
        {
            Plugin.GameInteropProvider.InitializeFromAttributes(this);
            chatChannelChangeHook?.Enable();
            isInputHook?.Enable();
            isInputAfkHook?.Enable();
            emptyXivString = (IntPtr)Utf8String.CreateEmpty(null);
        }

        internal void SendMessage(string message, InputChannel channel)
        {
            if (currentChannel != channel && !message.StartsWith('/'))
            {
                message = channel.CommandPrefix() + " " + message;
            }
            ProcessChatBox(message);
        }

        private unsafe void ProcessChatBox(string message)
        {
            if (easierProcessChatBox == null)
            {
                return;
            }
            HadInput = InputState.Normal | InputState.Afk;
            UIModule* uiModule = UIModule.Instance();
            using ChatPayload payload = new ChatPayload(message);
            nint mem1 = Marshal.AllocHGlobal(400);
            Marshal.StructureToPtr(payload, mem1, fDeleteOld: false);
            easierProcessChatBox((nint)uiModule, mem1, IntPtr.Zero, 0);
            Marshal.FreeHGlobal(mem1);
        }

        internal void ChangeChatChannel(InputChannel channel)
        {
            if (chatManager != IntPtr.Zero && channelChangeCommand != null && emptyXivString != IntPtr.Zero)
            {
                channelChangeCommand(chatManager, (int)channel, channel.LinkshellIndex(), emptyXivString, '\u0001');
            }
        }

        private byte changeChatChannelDetour(IntPtr a1, uint channel)
        {
            chatManager = a1;
            currentChannel = (InputChannel)channel;
            return chatChannelChangeHook.Original(a1, channel);
        }

        private byte inputDetour(nint a1)
        {
            if (HadInput == InputState.None)
            {
                return isInputHook!.Original(a1);
            }

            HadInput &= ~InputState.Normal;
            return 1;
        }

        private byte inputAfkDetour()
        {
            if (HadInput == InputState.None)
            {
                return isInputAfkHook!.Original();
            }

            HadInput &= ~InputState.Afk;
            return 1;
        }

        public unsafe void Dispose()
        {
            chatChannelChangeHook?.Dispose();
            isInputHook?.Dispose();
            isInputAfkHook?.Dispose();
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    internal readonly struct ChatPayload : IDisposable
    {
        [FieldOffset(0)]
        private readonly nint textPtr;

        [FieldOffset(16)]
        private readonly ulong textLen;

        [FieldOffset(8)]
        private readonly ulong unk1;

        [FieldOffset(24)]
        private readonly ulong unk2;

        internal ChatPayload(string text)
        {
            byte[] stringBytes = Encoding.UTF8.GetBytes(text);
            textPtr = Marshal.AllocHGlobal(stringBytes.Length + 30);
            Marshal.Copy(stringBytes, 0, textPtr, stringBytes.Length);
            Marshal.WriteByte(textPtr + stringBytes.Length, 0);
            textLen = (ulong)(stringBytes.Length + 1);
            unk1 = 64uL;
            unk2 = 0uL;
        }

        public void Dispose()
        {
            Marshal.FreeHGlobal(textPtr);
        }
    }

    internal enum InputChannel : uint
    {
        Tell = 0u,
        Say = 1u,
        Party = 2u,
        Alliance = 3u,
        Yell = 4u,
        Shout = 5u,
        FreeCompany = 6u,
        PvpTeam = 7u,
        NoviceNetwork = 8u,
        CrossLinkshell1 = 9u,
        CrossLinkshell2 = 10u,
        CrossLinkshell3 = 11u,
        CrossLinkshell4 = 12u,
        CrossLinkshell5 = 13u,
        CrossLinkshell6 = 14u,
        CrossLinkshell7 = 15u,
        CrossLinkshell8 = 16u,
        Linkshell1 = 19u,
        Linkshell2 = 20u,
        Linkshell3 = 21u,
        Linkshell4 = 22u,
        Linkshell5 = 23u,
        Linkshell6 = 24u,
        Linkshell7 = 25u,
        Linkshell8 = 26u
    }

    internal static class InputChannelExtensions
    {
        internal static uint LinkshellIndex(this InputChannel channel)
        {
            return channel switch
            {
                InputChannel.Linkshell1 => 0u,
                InputChannel.Linkshell2 => 1u,
                InputChannel.Linkshell3 => 2u,
                InputChannel.Linkshell4 => 3u,
                InputChannel.Linkshell5 => 4u,
                InputChannel.Linkshell6 => 5u,
                InputChannel.Linkshell7 => 6u,
                InputChannel.Linkshell8 => 7u,
                InputChannel.CrossLinkshell1 => 0u,
                InputChannel.CrossLinkshell2 => 1u,
                InputChannel.CrossLinkshell3 => 2u,
                InputChannel.CrossLinkshell4 => 3u,
                InputChannel.CrossLinkshell5 => 4u,
                InputChannel.CrossLinkshell6 => 5u,
                InputChannel.CrossLinkshell7 => 6u,
                InputChannel.CrossLinkshell8 => 7u,
                _ => 0u,
            };
        }

        internal static string CommandPrefix(this InputChannel channel)
        {
            return "/" + channel switch
            {
                //InputChannel.Tell = 0u,
                InputChannel.Say => "say",
                InputChannel.Party => "party",
                InputChannel.Alliance => "alliance",
                InputChannel.Yell => "yell",
                InputChannel.Shout => "shout",
                InputChannel.FreeCompany => "fc",
                InputChannel.PvpTeam => "pvp",
                InputChannel.NoviceNetwork => "nn",
                InputChannel.Linkshell1 => "ls1",
                InputChannel.Linkshell2 => "ls2",
                InputChannel.Linkshell3 => "ls3",
                InputChannel.Linkshell4 => "ls4",
                InputChannel.Linkshell5 => "ls5",
                InputChannel.Linkshell6 => "ls6",
                InputChannel.Linkshell7 => "ls7",
                InputChannel.Linkshell8 => "ls8",
                InputChannel.CrossLinkshell1 => "cwls1",
                InputChannel.CrossLinkshell2 => "cwls2",
                InputChannel.CrossLinkshell3 => "cwls3",
                InputChannel.CrossLinkshell4 => "cwls4",
                InputChannel.CrossLinkshell5 => "cwls5",
                InputChannel.CrossLinkshell6 => "cwls6",
                InputChannel.CrossLinkshell7 => "cwls7",
                InputChannel.CrossLinkshell8 => "cwls8",
                _ => "say",
            };
        }

        internal static InputChannel InptuChannelFromChatType(XivChatType type)
        {
            InputChannel channel = InputChannel.Say;
            switch (type)
            {
                case XivChatType.None: { break; }
                case XivChatType.Debug: { break; }
                case XivChatType.Urgent: { break; }
                case XivChatType.Notice: { break; }
                case XivChatType.Say: { break; }
                case XivChatType.Shout: { break; }
                case XivChatType.TellOutgoing: { break; }
                case XivChatType.TellIncoming: { break; }
                case XivChatType.Party: { break; }
                case XivChatType.Alliance: { break; }
                case XivChatType.Ls1: { break; }
                case XivChatType.Ls2: { break; }
                case XivChatType.Ls3: { break; }
                case XivChatType.Ls4: { break; }
                case XivChatType.Ls5: { break; }
                case XivChatType.Ls6: { break; }
                case XivChatType.Ls7: { break; }
                case XivChatType.Ls8: { break; }
                case XivChatType.FreeCompany: { break; }
                case XivChatType.NoviceNetwork: { break; }
                case XivChatType.CustomEmote: { break; }
                case XivChatType.StandardEmote: { break; }
                case XivChatType.Yell: { break; }
                case XivChatType.CrossParty: { break; }
                case XivChatType.PvPTeam: { break; }
                case XivChatType.CrossLinkShell1: { break; }
                case XivChatType.Echo: { break; }
                case XivChatType.SystemError: { break; }
                case XivChatType.SystemMessage: { break; }
                case XivChatType.GatheringSystemMessage: { break; }
                case XivChatType.ErrorMessage: { break; }
                case XivChatType.NPCDialogue: { break; }
                case XivChatType.NPCDialogueAnnouncements: { break; }
                case XivChatType.RetainerSale: { break; }
                case XivChatType.CrossLinkShell2: { break; }
                case XivChatType.CrossLinkShell3: { break; }
                case XivChatType.CrossLinkShell4: { break; }
                case XivChatType.CrossLinkShell5: { break; }
                case XivChatType.CrossLinkShell6: { break; }
                case XivChatType.CrossLinkShell7: { break; }
                case XivChatType.CrossLinkShell8: { break; }
            }
            return channel;
        }
    }
}

 
