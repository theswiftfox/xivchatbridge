using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using XIVChatBridge.Windows;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using System;
using System.Threading.Tasks;
using System.Threading;
using System.Text;
using System.Text.Json;
using System.Net;
using System.Text.Json.Serialization;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using Lumina.Excel.GeneratedSheets;
using System.ComponentModel;
using System.Collections.Generic;

namespace XIVChatBridge;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Logger { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInteropProvider { get; private set; } = null!;

    private const string CommandName = "/xivchat";

    private const string messageStoreName = "storage.json";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("XIV Chat Bridge");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    internal GameFunctions Functions { get; }

    private ConcurrentQueue<ChatMessage> messages = new ConcurrentQueue<ChatMessage>(); // todo: ringbuffer in c#?
    private ConcurrentQueue<NewMessageRequest> newMessageRequests = new ConcurrentQueue<NewMessageRequest>();

    HttpListener listener = new HttpListener();
    private Task? httpServerTask { get; set; }

    private DirectoryInfo? frontendDir = null;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Functions = new GameFunctions();

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "A useful message to display in /xlhelp"
        });

        PluginInterface.UiBuilder.Draw += DrawUI;

        // This adds a button to the plugin installer entry of this plugin which allows
        // to toggle the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;

        ChatGui.ChatMessage += OnChatMessage;

        if (Configuration.PersistMessages)
        {
            var storagePath = getMessageStore();
            if (storagePath != null)
            {
                List<ChatMessage>? stored = null;
                try
                {
                    stored = ChatMessageSerializer.deserialize(storagePath);
                }
                catch (JsonException ex)
                {
                    Logger.Error("Failed to read message store: {0}", ex.Message);
                }

                if (stored != null && stored.Count > 0)
                {
                    messages = new ConcurrentQueue<ChatMessage>(stored);
                }
            }
            else
            {
                Logger.Warning("Unable to get storage path for message persistence");
            }
        }

        Load();
    }

    private void Load()
    {
        httpServerTask = handleIncomingConnections(Configuration.Port, Configuration.AllowNonLocalAccess)
            .ContinueWith(task =>
            {
                if (!task.IsCanceled && task.Exception != null)
                {
                    Logger.Error("exception on server: {0}", task.Exception);
                }
                else
                {
                    var _ignore = task.Exception;
                }
            });

        frontendDir = getWorkingDir()?.GetDirectories("Frontend").First();
        Logger.Debug("ChatBridge loaded!");
    }

    public void Reload()
    {
        stopListening();
        Load();
    }

    public void Dispose()
    {
        if (Configuration.PersistMessages)
        {
            var storagePath = getMessageStore();
            if (storagePath != null) {
                ChatMessageSerializer.serialize(messages.ToList(), storagePath);
            } else
            {
                Logger.Warning("Unable to get storage path for message persistence");
            }
        }
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);

        stopListening();
        listener?.Close();

        Functions.Dispose();
    }

    private void OnCommand(string command, string args)
    {
        // in response to the slash command, just toggle the display status of our main ui
        ToggleMainUI();
    }

    private void DrawUI()
    {
        WindowSystem.Draw();

        if (!newMessageRequests.IsEmpty)
        {
            NewMessageRequest? message;
            while (newMessageRequests.TryDequeue(out message))
            {
                if (message == null) continue;

                var localPlayer = ClientState.LocalPlayer;
                if (localPlayer == null)
                {
                    Logger.Error("Unable to get LocalPlayer..");
                    return;
                }

                Functions.SendMessage(message.Text, message.Type);
            }
        }
    }

    public void ToggleConfigUI() => ConfigWindow.Toggle();

    public void ToggleMainUI() => MainWindow.Toggle();

    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (!Enum.IsDefined(typeof(XivChatType), type) || isHandled || message == null) return;

        var text = message.TextValue;
        if (text == null) return;

        var chatMsg = new ChatMessage(type, sender.TextValue, message.TextValue);

        addMessage(chatMsg);
    }

    #region HttpServer
    private async Task handleIncomingConnections(int port, bool listenOnAll)
    {
        if (listener == null) return;
        if (listenOnAll)
        {
            listener.Prefixes.Add("http://+:" + port.ToString() + "/");
        }
        else
        {
            listener.Prefixes.Add("http://localhost:" + port.ToString() + "/");
            listener.Prefixes.Add("http://127.0.0.1:" + port.ToString() + "/");
        }

        listener.Start();
        Logger.Debug("Server started");

        while (listener.IsListening)
        {
            try
            {
                HttpListenerContext? ctx = await listener.GetContextAsync();
                if (ctx == null)
                {
                    Logger.Warning("got null ctx..");
                    continue;
                }

                HttpListenerRequest req = ctx.Request;
                HttpListenerResponse resp = ctx.Response;

                if (req == null || resp == null) continue;
                if (req.Url == null) continue;

                if (req.Url.AbsolutePath == "/messages")
                {
                    switch (req.HttpMethod)
                    {
                        case "GET":
                            {
                                await handleGet(req, resp);
                                break;
                            }
                        case "POST":
                            {
                                await handlePost(req, resp);
                                break;
                            }
                        case "OPTIONS":
                            {
                                handleOptionsMessage(req, resp);
                                break;
                            }
                        default:
                            {
                                await unknownMethod(resp);
                                break;
                            }

                    }
                }
                else
                {
                    switch (req.HttpMethod)
                    {
                        case "GET":
                            {
                                await handleGetFile(req, resp);
                                break;
                            }
                        case "OPTIONS":
                            {
                                handleOptionsChat(req, resp);
                                break;
                            }
                        default:
                            {
                                await unknownMethod(resp);
                                break;
                            }
                    }
                }
            }
            catch (HttpListenerException ex)
            {
                if (ex.ErrorCode != 995)
                {
                    Logger.Error("Http Listener exception: {0}", ex);
                }
            }
        }
    }

    internal void stopListening()
    {
        if (listener?.IsListening == true)
        {
            listener.Stop();
            Logger.Debug("Server closed.");
        }
    }

    private static async Task unknownMethod(HttpListenerResponse resp)
    {
        byte[] data = Encoding.UTF8.GetBytes("Unknown request method");
        resp.StatusCode = 400;
        resp.ContentType = "text/plain";
        resp.ContentEncoding = Encoding.UTF8;
        resp.ContentLength64 = data.LongLength;
        await resp.OutputStream.WriteAsync(data, 0, data.Length);
        resp.Close();
    }

    private async Task handleGet(HttpListenerRequest req, HttpListenerResponse resp)
    {
        var json = JsonSerializer.Serialize(messages, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } });

        byte[] data = Encoding.UTF8.GetBytes(json.ToString());

        resp.ContentType = "application/json";
        resp.ContentEncoding = Encoding.UTF8;
        resp.ContentLength64 = data.LongLength;

        await resp.OutputStream.WriteAsync(data, 0, data.Length);
        resp.Close();
    }

    private async Task handlePost(HttpListenerRequest req, HttpListenerResponse resp)
    {
        NewMessageRequest? message;
        try
        {
            message = JsonSerializer.Deserialize<NewMessageRequest>(req.InputStream, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } });
        }
        catch (JsonException e)
        {
            Logger.Error("Json deserialize failed: {0}", e.Message);

            byte[] data = Encoding.UTF8.GetBytes("Unable to parse message: " + e.Message);
            resp.StatusCode = 400;
            resp.ContentType = "text/plain";
            resp.ContentEncoding = Encoding.UTF8;
            resp.ContentLength64 = data.LongLength;
            await resp.OutputStream.WriteAsync(data, 0, data.Length);
            resp.Close();
            return;
        }

        if (message == null)
        {
            Logger.Warning("Messag deserialized as Null..");
            return;
        }

        newMessageRequests.Enqueue(message);

        resp.StatusCode = 201;
        resp.Close();
    }

    private void handleOptionsMessage(HttpListenerRequest req, HttpListenerResponse resp)
    {
        resp.StatusCode = 200;
        resp.Headers.Add("Allow: OPTIONS, GET, POST");
        resp.Close();
    }

    private async Task handleGetFile(HttpListenerRequest req, HttpListenerResponse resp)
    {
        var path = req.Url.AbsolutePath.ToString();
        Logger.Debug("request path: {0}", path);

        if (path == "/")
        {
            path = "index.html";
        }

        var file = frontendDir.GetFiles(path.TrimStart('/')).FirstOrDefault();
        if (file == null)
        {
            resp.StatusCode = 404;
            resp.Close();
            return;
        }

        resp.ContentLength64 = file.Length;

        if (path.EndsWith(".js"))
        {
            resp.ContentType = "text/javascript";
        }
        else if (path.EndsWith(".wasm"))
        {
            resp.ContentType = "application/wasm";
        }
        else if (path.EndsWith(".css"))
        {
            resp.ContentType = "text/css";
        }
        else if (path.EndsWith(".html"))
        {
            resp.ContentType = "text/html";
        }

        using (var reader = new BinaryReader(file.OpenRead()))
        {
            byte[] buffer = new byte[2048];
            int read;
            while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                await resp.OutputStream.WriteAsync(buffer, 0, read);
            }
        }

        resp.StatusCode = 200;
        resp.Close();
    }

    private void handleOptionsChat(HttpListenerRequest req, HttpListenerResponse resp)
    {
        resp.StatusCode = 200;
        resp.Headers.Add("Allow: OPTIONS, GET");
        resp.Close();
    }
    #endregion

    private void addMessage(ChatMessage msg)
    {
        if (messages.Count >= Configuration.MessageLimit) 
        {
            _ = messages.TryDequeue(out _); // just ignore for now.
        }
        messages.Enqueue(msg);
    }

    private DirectoryInfo? getWorkingDir()
    {
        return PluginInterface.AssemblyLocation?.Directory;
    }

    private FileInfo? getMessageStore()
    {
        var configDir = PluginInterface.ConfigDirectory;
        if (configDir == null) return null;

        var file = new FileInfo(Path.Combine(configDir.FullName, messageStoreName));
        return file;
    }
}
