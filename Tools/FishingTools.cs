using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Locations;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static StardewMCP.Tools.ToolRegistry;

namespace StardewMCP.Tools;

public static class FishingTools
{
    public static void Register(ToolRegistry registry)
    {
        registry.Add(
            Tool("get_catchable_fish",
                "List all fish catchable right now based on the current season, time of day, weather, and player fishing level. " +
                "Pass a location name to check a specific spot, or omit it to see fish across all locations.",
                Props(
                    Str("location_name", "Location to check, e.g. 'Beach', 'Forest', 'Town', 'Mountain', 'IslandSouth'. Omit to check all locations.")
                )),
            GetCatchableFish,
            observeOnly: true
        );

        registry.Add(
            Tool("get_fish_schedule",
                "Show every location and season where a specific fish can be caught, plus its minimum fishing level and any extra conditions.",
                Props(
                    Str("fish_name", "Partial or full fish name, e.g. 'Catfish', 'Tuna', 'Legend'")
                )),
            GetFishSchedule,
            observeOnly: true
        );
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string FormatTime(int t)
    {
        var h = (t / 100) % 24; // wrap 2400→0, 2600→2, etc.
        var m = t % 100;
        var ampm = h >= 12 ? "PM" : "AM";
        var h12 = h > 12 ? h - 12 : h == 0 ? 12 : h;
        return $"{h12}:{m:D2} {ampm}";
    }

    // True for internal/temporary locations that shouldn't appear in results
    private static bool IsTempLocation(string name)
        => name == "fishingGame" || name.StartsWith("Temp", StringComparison.Ordinal);

    private static readonly string[] SeasonOrder = ["spring", "summer", "fall", "winter"];

    private static string DescribeSeason(Season? season, string? condition = null)
    {
        if (season.HasValue) return season.Value.ToString().ToLower();
        if (condition is null) return "all seasons";

        // Collect seasons from SEASON and LOCATION_SEASON GSQ clauses.
        // "SEASON spring fall"  →  group 1 = "spring fall"
        // "LOCATION_SEASON Here spring fall winter"  →  group 1 = "spring fall winter"
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in Regex.Matches(condition,
            @"(?:LOCATION_SEASON\s+\S+|SEASON)\s+((?:spring|summer|fall|winter)(?:\s+(?:spring|summer|fall|winter))*)",
            RegexOptions.IgnoreCase))
        {
            foreach (var s in m.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                found.Add(s.ToLower());
        }

        if (found.Count == 0 || found.Count == 4) return "all seasons";
        return string.Join("/", SeasonOrder.Where(found.Contains));
    }

    // Strip type prefix e.g. "(O)145" → "145" to look up in Data/Fish
    private static string UnqualifiedId(string itemId)
    {
        var paren = itemId.LastIndexOf(')');
        return paren >= 0 ? itemId[(paren + 1)..] : itemId;
    }

    // Returns true if this item is an actual fish (Object category -4), not seaweed/algae/jelly/etc.
    private static bool IsRealFish(string itemId, Dictionary<string, string> allFishData)
    {
        var rawId = UnqualifiedId(itemId);
        if (!allFishData.TryGetValue(rawId, out var raw)) return false;
        var parts = raw.Split('/');
        if (parts.Length > 1 && parts[1] == "trap") return false;
        // Category -4 = fish; seaweed/algae are not fish category
        return ItemRegistry.Create(itemId) is StardewValley.Object { Category: -4 };
    }

    // Check Data/Fish constraints (index 5 = time, index 7 = weather). Seasons are not in Data/Fish at runtime.
    private static bool PassesFishDataRequirements(
        SpawnFishData spawn,
        Dictionary<string, string> fishData,
        int time,
        bool isRaining)
    {
        if (spawn.IgnoreFishDataRequirements) return true;
        if (!fishData.TryGetValue(UnqualifiedId(spawn.ItemId ?? ""), out var raw)) return true;

        var parts = raw.Split('/');
        if (parts.Length <= 1 || parts[1] == "trap") return true;

        // Time window check (index 5: space-separated start/end pairs)
        if (parts.Length > 5)
        {
            var tokens = parts[5].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            bool inWindow = tokens.Length == 0;
            for (int i = 0; i + 1 < tokens.Length && !inWindow; i += 2)
            {
                if (int.TryParse(tokens[i], out var start) && int.TryParse(tokens[i + 1], out var end))
                    inWindow = time >= start && time < end;
            }
            if (!inWindow) return false;
        }

        // Weather check (index 7: "rainy", "sunny", anything else = any)
        if (parts.Length > 7)
        {
            var weather = parts[7].Trim().ToLowerInvariant();
            if (weather == "rainy" && !isRaining) return false;
            if (weather == "sunny" && isRaining) return false;
        }

        return true;
    }

    // Describe time windows from Data/Fish index 5
    private static string DescribeFishTimes(string[] parts)
    {
        if (parts.Length <= 5) return "all day";
        var tokens = parts[5].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 2) return "all day";
        var windows = new List<string>();
        for (int i = 0; i + 1 < tokens.Length; i += 2)
        {
            if (int.TryParse(tokens[i], out var s) && int.TryParse(tokens[i + 1], out var e))
                windows.Add($"{FormatTime(s)}–{FormatTime(e)}");
        }
        return windows.Count > 0 ? string.Join(", ", windows) : "all day";
    }

    // Describe weather from Data/Fish index 7
    private static string DescribeFishWeather(string[] parts)
    {
        if (parts.Length <= 7) return "any weather";
        return parts[7].Trim().ToLowerInvariant() switch
        {
            "rainy" => "rainy only",
            "sunny" => "sunny only",
            _ => "any weather"
        };
    }

    // Core logic: get all real catchable fish at a location given current conditions.
    private static List<string> GetFishAt(
        GameLocation location,
        LocationData locData,
        Season season,
        int time,
        int fishingLevel,
        bool isRaining,
        Dictionary<string, string> allFishData)
    {
        var result = new List<string>();
        var seen = new HashSet<string>();

        foreach (var entry in locData.Fish)
        {
            if (string.IsNullOrEmpty(entry.ItemId)) continue;
            if (entry.Season.HasValue && entry.Season.Value != season) continue;
            if (entry.MinFishingLevel > fishingLevel) continue;
            if (entry.RequireMagicBait) continue;
            if (!IsRealFish(entry.ItemId, allFishData)) continue;

            if (!string.IsNullOrEmpty(entry.Condition) &&
                !GameStateQuery.CheckConditions(entry.Condition, location, Game1.player))
                continue;

            if (!PassesFishDataRequirements(entry, allFishData, time, isRaining))
                continue;

            var name = ItemRegistry.GetData(entry.ItemId)?.DisplayName ?? entry.ItemId;
            if (seen.Add(name))
                result.Add(name);
        }

        return result;
    }

    // ── Handlers ─────────────────────────────────────────────────────────────

    private static Task<string> GetCatchableFish(JsonObject args)
    {
        var locationArg = args["location_name"]?.GetValue<string>().Trim();
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady) return "No game is loaded.";

            var season = Game1.season;
            var time = Game1.timeOfDay;
            var fishingLevel = Game1.player.FishingLevel;
            var isRaining = Game1.isRaining || Game1.isLightning;
            var allFishData = Game1.content.Load<Dictionary<string, string>>("Data/Fish");
            var conditions = $"{season}, {FormatTime(time)}, {(isRaining ? "rainy" : "sunny")}";

            if (!string.IsNullOrEmpty(locationArg))
            {
                // Single location
                var location = Game1.getLocationFromName(locationArg);
                if (location is null)
                    return $"Location '{locationArg}' not found.";

                var locData = location.GetData();
                if (locData?.Fish == null || locData.Fish.Count == 0)
                    return $"No fish data for {location.Name}. Try Beach, Forest, Town, Mountain, etc.";

                var catchable = GetFishAt(location, locData, season, time, fishingLevel, isRaining, allFishData);
                if (catchable.Count == 0)
                    return $"No fish catchable at {location.Name} right now ({conditions}, fishing lvl {fishingLevel}).";

                var sb = new StringBuilder();
                sb.AppendLine($"Catchable at {location.Name} — {conditions}, fishing lvl {fishingLevel}:\n");
                foreach (var name in catchable)
                    sb.AppendLine($"  {name}");
                return sb.ToString().TrimEnd();
            }
            else
            {
                // All locations
                var allLocationData = Game1.content.Load<Dictionary<string, LocationData>>("Data/Locations");
                var sb = new StringBuilder();
                sb.AppendLine($"Catchable fish everywhere — {conditions}, fishing lvl {fishingLevel}:\n");
                bool anyFound = false;

                foreach (var (locName, locData) in allLocationData.OrderBy(kv => kv.Key))
                {
                    if (locData.Fish == null || locData.Fish.Count == 0) continue;
                    var location = Game1.getLocationFromName(locName);
                    if (location is null) continue;

                    var catchable = GetFishAt(location, locData, season, time, fishingLevel, isRaining, allFishData);
                    if (catchable.Count == 0) continue;

                    anyFound = true;
                    sb.AppendLine($"{locName}: {string.Join(", ", catchable)}");
                }

                if (!anyFound)
                    return $"No fish catchable anywhere right now ({conditions}).";

                return sb.ToString().TrimEnd();
            }
        });
    }

    // Fish hardcoded in MineShaft.getFish() — not in Data/Locations.
    // Only catchable on the specific water/lava floors within each mine area.
    private static readonly (string ItemId, string Location)[] MineShaftFish =
    [
        ("(O)158", "UndergroundMine (floor 20)"),  // Stonefish
        ("(O)161", "UndergroundMine (floor 60)"),  // Ice Pip
        ("(O)162", "UndergroundMine (floor 100)"), // Lava Eel
    ];

    private static Task<string> GetFishSchedule(JsonObject args)
    {
        var fishName = (args["fish_name"]?.GetValue<string>() ?? "").Trim();
        if (string.IsNullOrEmpty(fishName))
            return Task.FromResult("Provide a fish name to look up.");

        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady) return "No game is loaded.";

            var allLocationData = Game1.content.Load<Dictionary<string, LocationData>>("Data/Locations");
            var allFishData = Game1.content.Load<Dictionary<string, string>>("Data/Fish");
            var matches = new List<string>();
            var seen = new HashSet<string>();

            foreach (var (locName, locData) in allLocationData.OrderBy(kv => kv.Key))
            {
                if (IsTempLocation(locName)) continue;
                if (locData.Fish == null) continue;

                foreach (var entry in locData.Fish)
                {
                    if (string.IsNullOrEmpty(entry.ItemId)) continue;
                    if (!IsRealFish(entry.ItemId, allFishData)) continue;

                    var itemData = ItemRegistry.GetData(entry.ItemId);
                    var displayName = itemData?.DisplayName ?? entry.ItemId;

                    if (!displayName.Contains(fishName, StringComparison.OrdinalIgnoreCase) &&
                        !entry.ItemId.Contains(fishName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var season = DescribeSeason(entry.Season, entry.Condition);
                    var levelStr = entry.MinFishingLevel > 0 ? $", lvl {entry.MinFishingLevel}+" : "";

                    string timeStr = "all day", weatherStr = "any weather";
                    if (allFishData.TryGetValue(UnqualifiedId(entry.ItemId), out var rawFish))
                    {
                        var parts = rawFish.Split('/');
                        if (parts.Length > 1 && parts[1] != "trap")
                        {
                            timeStr = DescribeFishTimes(parts);
                            weatherStr = DescribeFishWeather(parts);
                        }
                    }

                    var line = $"  {displayName} — {locName}: {season} | {timeStr} | {weatherStr}{levelStr}";
                    if (seen.Add(line))
                        matches.Add(line);
                }
            }

            // Supplement with fish hardcoded in MineShaft.getFish() (not in Data/Locations).
            foreach (var (itemId, location) in MineShaftFish)
            {
                var itemData = ItemRegistry.GetData(itemId);
                var displayName = itemData?.DisplayName ?? itemId;
                if (!displayName.Contains(fishName, StringComparison.OrdinalIgnoreCase) &&
                    !itemId.Contains(fishName, StringComparison.OrdinalIgnoreCase))
                    continue;

                string timeStr = "all day", weatherStr = "any weather";
                if (allFishData.TryGetValue(UnqualifiedId(itemId), out var rawFish))
                {
                    var parts = rawFish.Split('/');
                    if (parts.Length > 1 && parts[1] != "trap")
                    {
                        timeStr = DescribeFishTimes(parts);
                        weatherStr = DescribeFishWeather(parts);
                    }
                }

                var line = $"  {displayName} — {location}: all seasons | {timeStr} | {weatherStr}";
                if (seen.Add(line))
                    matches.Add(line);
            }

            if (matches.Count == 0)
                return $"No fish matching '{fishName}' found in any location.";

            var sb = new StringBuilder();
            sb.AppendLine($"Schedule for '{fishName}':\n");
            foreach (var line in matches)
                sb.AppendLine(line);

            return sb.ToString().TrimEnd();
        });
    }
}
