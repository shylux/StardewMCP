using System.Text.Json.Nodes;

namespace StardewMCP.Tools;

public class ToolRegistry
{
    private readonly List<(JsonNode Definition, Func<JsonObject, Task<string>> Handler, bool ObserveOnly)> _tools = new();

    public bool OnlyObserve { get; }

    public ToolRegistry(bool onlyObserve = false)
    {
        OnlyObserve = onlyObserve;
        NpcTools.Register(this);
        PlayerTools.Register(this);
        WorldTools.Register(this);
        FarmTools.Register(this);
        MonsterTools.Register(this);
        FishingTools.Register(this);
    }

    public void Add(JsonNode definition, Func<JsonObject, Task<string>> handler, bool observeOnly = false)
        => _tools.Add((definition, handler, observeOnly));

    public JsonArray GetDefinitions()
    {
        var arr = new JsonArray();
        foreach (var (def, _, observeOnly) in _tools)
        {
            if (OnlyObserve && !observeOnly) continue;
            // JsonArray takes ownership of nodes, so deep-clone each definition to keep the original intact
            arr.Add(JsonNode.Parse(def.ToJsonString())!);
        }
        return arr;
    }

    public async Task<string> Call(string name, JsonObject args)
    {
        foreach (var (def, handler, observeOnly) in _tools)
        {
            if (def["name"]?.GetValue<string>() != name) continue;
            if (OnlyObserve && !observeOnly)
                return $"Tool '{name}' is not available in observe-only mode.";
            return await handler(args);
        }
        return $"Unknown tool: '{name}'. Available: {string.Join(", ", _tools.Select(t => t.Definition["name"]?.GetValue<string>()))}";
    }

    // ── Shared schema builders used by all tool files ────────────────────────

    internal static JsonObject Tool(string name, string description, JsonObject inputSchema) =>
        new() { ["name"] = name, ["description"] = description, ["inputSchema"] = inputSchema };

    internal static JsonObject Props(params (string Name, JsonObject Schema)[] props)
    {
        var properties = new JsonObject();
        foreach (var (n, s) in props)
            properties[n] = s;
        return new JsonObject { ["type"] = "object", ["properties"] = properties };
    }

    internal static (string, JsonObject) Str(string name, string description) =>
        (name, new JsonObject { ["type"] = "string", ["description"] = description });

    internal static (string, JsonObject) Int(string name, string description) =>
        (name, new JsonObject { ["type"] = "integer", ["description"] = description });

    internal static (string, JsonObject) Bool(string name, string description) =>
        (name, new JsonObject { ["type"] = "boolean", ["description"] = description });
}
