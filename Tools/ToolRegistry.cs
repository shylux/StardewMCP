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
    }

    public void Add(JsonNode definition, Func<JsonObject, Task<string>> handler, bool observeOnly = false)
        => _tools.Add((definition, handler, observeOnly));

    public JsonArray GetDefinitions()
    {
        var arr = new JsonArray();
        foreach (var (def, _, observeOnly) in _tools)
        {
            if (OnlyObserve && !observeOnly) continue;
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
}
