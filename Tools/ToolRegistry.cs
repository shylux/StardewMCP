using System.Text.Json.Nodes;

namespace StardewMCP.Tools;

public class ToolRegistry
{
    private readonly List<(JsonNode Definition, Func<JsonObject, Task<string>> Handler)> _tools = new();

    public ToolRegistry()
    {
        NpcTools.Register(this);
        PlayerTools.Register(this);
        WorldTools.Register(this);
        FarmTools.Register(this);
    }

    public void Add(JsonNode definition, Func<JsonObject, Task<string>> handler)
        => _tools.Add((definition, handler));

    public JsonArray GetDefinitions()
    {
        var arr = new JsonArray();
        foreach (var (def, _) in _tools)
            arr.Add(JsonNode.Parse(def.ToJsonString())!);
        return arr;
    }

    public async Task<string> Call(string name, JsonObject args)
    {
        foreach (var (def, handler) in _tools)
        {
            if (def["name"]?.GetValue<string>() == name)
                return await handler(args);
        }
        return $"Unknown tool: '{name}'. Available: {string.Join(", ", _tools.Select(t => t.Definition["name"]?.GetValue<string>()))}";
    }
}
