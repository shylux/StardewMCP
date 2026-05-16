using StardewMCP.Tools;
using StardewModdingAPI;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace StardewMCP;

public class McpServer
{
    private readonly IMonitor _monitor;
    private readonly HttpListener _listener = new();
    private readonly ToolRegistry _tools;
    private CancellationTokenSource? _cts;

    private readonly int _port;

    public McpServer(IMonitor monitor, ModConfig config)
    {
        _monitor = monitor;
        _port = config.Port;
        _tools = new ToolRegistry(config.OnlyAllowObserveTools);
    }

    public void Start()
    {
        _listener.Prefixes.Add($"http://localhost:{_port}/");
        _listener.Start();
        _cts = new CancellationTokenSource();
        Task.Run(() => AcceptLoop(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener.Stop();
    }

    private async Task AcceptLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var ctx = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequest(ctx), ct);
            }
            catch when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _monitor.Log($"MCP server error: {ex.Message}", LogLevel.Error);
            }
        }
    }

    private async Task HandleRequest(HttpListenerContext ctx)
    {
        try
        {
            ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");

            if (ctx.Request.HttpMethod == "OPTIONS")
            {
                ctx.Response.StatusCode = 204;
                ctx.Response.Close();
                return;
            }

            if (ctx.Request.HttpMethod != "POST")
            {
                ctx.Response.StatusCode = 405;
                ctx.Response.Close();
                return;
            }

            using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();
            var request = JsonNode.Parse(body);

            var method = request?["method"]?.GetValue<string>();
            var id = request?["id"];
            var @params = request?["params"] as JsonObject ?? new JsonObject();

            // Notifications (no id field) — acknowledge with no body.
            if (id is null)
            {
                ctx.Response.StatusCode = 202;
                ctx.Response.Close();
                return;
            }

            JsonNode? result;
            if (method == "initialize")
                result = HandleInitialize();
            else if (method == "tools/list")
                result = HandleToolsList();
            else if (method == "tools/call")
                result = await HandleToolCall(@params);
            else
                result = new JsonObject(); // unknown method — return empty result

            var response = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = JsonNode.Parse(id.ToJsonString()),
                ["result"] = result
            };

            await WriteJson(ctx.Response, response);
        }
        catch (Exception ex)
        {
            _monitor.Log($"MCP request error: {ex}", LogLevel.Error);
            try { ctx.Response.StatusCode = 500; ctx.Response.Close(); } catch { }
        }
    }

    private static JsonNode HandleInitialize() => new JsonObject
    {
        ["protocolVersion"] = "2024-11-05",
        ["capabilities"] = new JsonObject
        {
            ["tools"] = new JsonObject()
        },
        ["serverInfo"] = new JsonObject
        {
            ["name"] = "StardewMCP",
            ["version"] = "1.0.0"
        }
    };

    private JsonNode HandleToolsList() => new JsonObject
    {
        ["tools"] = _tools.GetDefinitions()
    };

    private async Task<JsonNode> HandleToolCall(JsonObject @params)
    {
        var name = @params["name"]?.GetValue<string>() ?? "";
        var args = @params["arguments"] as JsonObject ?? new JsonObject();

        try
        {
            var text = await _tools.Call(name, args);
            return new JsonObject
            {
                ["content"] = new JsonArray
                {
                    new JsonObject { ["type"] = "text", ["text"] = text }
                }
            };
        }
        catch (Exception ex)
        {
            return new JsonObject
            {
                ["isError"] = true,
                ["content"] = new JsonArray
                {
                    new JsonObject { ["type"] = "text", ["text"] = $"Tool error: {ex.Message}" }
                }
            };
        }
    }

    private static async Task WriteJson(HttpListenerResponse response, JsonNode node)
    {
        var bytes = Encoding.UTF8.GetBytes(node.ToJsonString());
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.Close();
    }
}
