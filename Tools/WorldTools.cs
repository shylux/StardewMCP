using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;
using System.Text;
using System.Text.Json.Nodes;

namespace StardewMCP.Tools;

public static class WorldTools
{
    public static void Register(ToolRegistry registry)
    {
        registry.Add(
            Tool("find_item_in_chests",
                "Search all chests on the farm (and inside the farmhouse) for an item by name. Returns which chest contains it and where that chest is.",
                Props(Str("item_name", "Partial or full item name to search for, e.g. 'Watering Can', 'Coal', 'Parsnip'"))),
            FindItemInChests
        );

        registry.Add(
            Tool("get_location_names",
                "List all valid location names that can be used with teleport_player.",
                Props()),
            GetLocationNames
        );

        registry.Add(
            Tool("get_community_center_status",
                "Get the completion status of the Community Center (or Joja route): which rooms are done and which bundles are still incomplete.",
                Props()),
            GetCommunityCenterStatus
        );

        registry.Add(
            Tool("get_location_warps",
                "List all warp/exit points in a location: their tile position and where they lead.",
                Props(Str("location_name", "Location name, e.g. FarmHouse, Farm, Town"))),
            GetLocationWarps
        );
    }

    // ── Handlers ────────────────────────────────────────────────────────────

    private static Task<string> FindItemInChests(JsonObject args)
    {
        var search = args["item_name"]?.GetValue<string>() ?? "";
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady)
                return "No game is loaded.";

            var results = new List<string>();

            foreach (var location in Game1.locations)
            {
                foreach (var (tile, obj) in location.objects.Pairs)
                {
                    if (obj is not Chest chest)
                        continue;

                    foreach (var item in chest.Items)
                    {
                        if (item is null)
                            continue;

                        if (item.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                        {
                            var stackInfo = item.Stack > 1 ? $" x{item.Stack}" : "";
                            results.Add($"{item.Name}{stackInfo} — chest at {location.Name} ({tile.X}, {tile.Y})");
                        }
                    }
                }
            }

            if (results.Count == 0)
                return $"No item matching '{search}' found in any chest.";

            return string.Join("\n", results);
        });
    }

    private static Task<string> GetLocationNames(JsonObject args)
    {
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady)
                return "No game is loaded.";

            var names = Game1.locations
                .Select(l => l.Name)
                .Where(n => !string.IsNullOrEmpty(n))
                .OrderBy(n => n)
                .ToList();

            return string.Join("\n", names);
        });
    }

    private static Task<string> GetCommunityCenterStatus(JsonObject args)
    {
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady)
                return "No game is loaded.";

            // Joja route
            if (Utility.hasFinishedJojaRoute())
                return "Joja route completed. The Community Center was converted to a Joja warehouse.";

            var isJojaMember = Game1.player.mailReceived.Contains("JojaMember");

            var cc = Game1.locations.OfType<CommunityCenter>().FirstOrDefault();
            if (cc is null)
                return "Community Center location not found.";

            var sb = new StringBuilder();

            if (cc.areAllAreasComplete())
            {
                sb.AppendLine("Community Center: FULLY RESTORED ✓");
                return sb.ToString().TrimEnd();
            }

            if (isJojaMember)
                sb.AppendLine("Note: You are a Joja member.");

            sb.AppendLine("Community Center completion:\n");

            string[] areaNames = ["Crafts Room", "Pantry", "Fish Tank", "Boiler Room", "Vault", "Bulletin Board"];

            for (int areaIndex = 0; areaIndex < cc.areasComplete.Count && areaIndex < areaNames.Length; areaIndex++)
            {
                var areaName = areaNames[areaIndex];
                var areaComplete = cc.areasComplete[areaIndex];
                sb.AppendLine($"{(areaComplete ? "✓" : "✗")} {areaName}");

                if (areaComplete) continue;

                // Find incomplete bundles in this area
                foreach (var (bundleKey, bundleData) in Game1.netWorldState.Value.BundleData)
                {
                    // Key format: "AreaName/bundleIndex"
                    if (!bundleKey.StartsWith(areaName + "/")) continue;

                    var parts = bundleData.Split('/');
                    // parts[0] = bundle name, parts[2] = items (item_id quality amount repeating)
                    var bundleName = parts.Length > 0 ? parts[0] : bundleKey;

                    if (!int.TryParse(bundleKey.Split('/')[1], out var bundleIndex)) continue;

                    // Check completion via cc.bundles[bundleIndex]
                    if (!cc.bundles.ContainsKey(bundleIndex)) continue;
                    var bundleSlots = cc.bundles[bundleIndex];
                    bool bundleComplete = true;
                    foreach (var slot in bundleSlots) if (!slot) { bundleComplete = false; break; }
                    if (bundleComplete) continue;

                    sb.AppendLine($"  ✗ {bundleName}");

                    // List missing items
                    if (parts.Length > 2)
                    {
                        var itemTokens = parts[2].Split(' ');
                        int slotIndex = 0;
                        for (int i = 0; i + 2 < itemTokens.Length; i += 3, slotIndex++)
                        {
                            if (slotIndex < bundleSlots.Length && bundleSlots[slotIndex]) continue;
                            var itemId = itemTokens[i];
                            var amount = itemTokens[i + 2];
                            if (itemId == "-1") continue;
                            var itemName = ItemRegistry.GetDataOrErrorItem($"(O){itemId}").DisplayName;
                            sb.AppendLine($"    - {itemName}{(amount != "1" ? $" x{amount}" : "")}");
                        }
                    }
                }
            }

            return sb.ToString().TrimEnd();
        });
    }

    private static Task<string> GetLocationWarps(JsonObject args)
    {
        var locationName = args["location_name"]?.GetValue<string>() ?? "";
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady)
                return "No game is loaded.";

            var location = Game1.getLocationFromName(locationName);
            if (location is null)
                return $"Location '{locationName}' not found.";

            if (location.warps.Count == 0)
                return $"{locationName} has no warp points.";

            var lines = location.warps
                .Select(w => $"  ({w.X}, {w.Y}) → {w.TargetName} ({w.TargetX}, {w.TargetY})")
                .ToList();

            return $"Warps in {locationName}:\n" + string.Join("\n", lines);
        });
    }

    // ── Schema builders ─────────────────────────────────────────────────────

    private static JsonObject Tool(string name, string description, JsonObject inputSchema) =>
        new()
        {
            ["name"] = name,
            ["description"] = description,
            ["inputSchema"] = inputSchema
        };

    private static JsonObject Props(params (string Name, JsonObject Schema)[] props)
    {
        var properties = new JsonObject();
        foreach (var (n, s) in props)
            properties[n] = s;
        return new JsonObject { ["type"] = "object", ["properties"] = properties };
    }

    private static (string, JsonObject) Str(string name, string description) =>
        (name, new JsonObject { ["type"] = "string", ["description"] = description });
}
