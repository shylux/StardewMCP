using StardewModdingAPI;
using StardewValley;
using System.Text;
using System.Text.Json.Nodes;

namespace StardewMCP.Tools;

public static class NpcTools
{
    public static void Register(ToolRegistry registry)
    {
        registry.Add(
            Tool("get_npc_location",
                "Get the current map location and tile position of a named NPC.",
                Props(Str("npc_name", "NPC name, e.g. Abigail, Penny, Harvey, Leah"))),
            GetNpcLocation
        );

        registry.Add(
            Tool("get_all_npc_locations",
                "Get the current location of every villager NPC in the game.",
                Props()),
            GetAllNpcLocations
        );

        registry.Add(
            Tool("get_upcoming_birthdays",
                "List NPCs whose birthdays fall within the next N in-game days.",
                Props(Int("days_ahead", "How many days ahead to look (default: 7)"))),
            GetUpcomingBirthdays
        );

        registry.Add(
            Tool("get_npc_info",
                "Get detailed info about an NPC: friendship level, hearts, birthday, relationship status, whether you've talked/gifted today, and today's schedule.",
                Props(Str("npc_name", "NPC name, e.g. Abigail, Harvey, Penny"))),
            GetNpcInfo
        );

        registry.Add(
            Tool("get_npc_gift_preferences",
                "Get the loved and liked gift items for an NPC.",
                Props(Str("npc_name", "NPC name, e.g. Abigail, Harvey, Penny"))),
            GetNpcGiftPreferences
        );

        registry.Add(
            Tool("get_all_friendships",
                "Get a summary of friendship levels with every NPC you've met.",
                Props()),
            GetAllFriendships
        );

        registry.Add(
            Tool("get_spouse_info",
                "Get detailed information about your spouse: friendship, location, daily status, and stardrop status. Returns an error if not married.",
                Props()),
            GetSpouseInfo
        );
    }

    // ── Handlers ────────────────────────────────────────────────────────────

    private static Task<string> GetNpcLocation(JsonObject args)
    {
        var name = args["npc_name"]?.GetValue<string>() ?? "";
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady)
                return "No game is loaded.";

            var npc = Game1.getCharacterFromName(name);
            if (npc is null)
                return $"NPC '{name}' not found. Check spelling — names are case-sensitive.";

            var tile = npc.TilePoint;
            var loc = npc.currentLocation?.Name ?? "unknown";
            return $"{npc.Name} is currently in {loc} at tile ({tile.X}, {tile.Y}).";
        });
    }

    private static Task<string> GetAllNpcLocations(JsonObject args)
    {
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady)
                return "No game is loaded.";

            var sb = new StringBuilder();
            foreach (var location in Game1.locations)
            {
                foreach (var character in location.characters)
                {
                    if (character is NPC npc && !npc.IsMonster)
                    {
                        var tile = npc.TilePoint;
                        sb.AppendLine($"{npc.Name}: {location.Name} ({tile.X}, {tile.Y})");
                    }
                }
            }

            return sb.Length > 0
                ? sb.ToString().TrimEnd()
                : "No NPCs found.";
        });
    }

    private static Task<string> GetUpcomingBirthdays(JsonObject args)
    {
        var daysAhead = args["days_ahead"]?.GetValue<int>() ?? 7;
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady)
                return "No game is loaded.";

            var today = Game1.Date;
            var currentSeasonIdx = SeasonIndex(today.Season.ToString());
            var currentDayOfYear = currentSeasonIdx * 28 + today.DayOfMonth;

            var results = new List<(int DaysUntil, string Text)>();

            foreach (var location in Game1.locations)
            {
                foreach (var character in location.characters)
                {
                    if (character is not NPC npc || npc.Birthday_Season is null)
                        continue;

                    var birthdaySeasonIdx = SeasonIndex(npc.Birthday_Season);
                    if (birthdaySeasonIdx < 0)
                        continue;

                    var birthdayDayOfYear = birthdaySeasonIdx * 28 + npc.Birthday_Day;
                    var diff = birthdayDayOfYear - currentDayOfYear;
                    if (diff < 0) diff += 112; // wrap to next year

                    if (diff <= daysAhead)
                    {
                        var suffix = diff == 0 ? "TODAY!" : diff == 1 ? "tomorrow" : $"in {diff} days";
                        results.Add((diff, $"{npc.Name}: {npc.Birthday_Season} {npc.Birthday_Day} ({suffix})"));
                    }
                }
            }

            if (results.Count == 0)
                return $"No birthdays in the next {daysAhead} day(s).";

            results.Sort((a, b) => a.DaysUntil.CompareTo(b.DaysUntil));
            return string.Join("\n", results.Select(r => r.Text));
        });
    }

    private static Task<string> GetNpcInfo(JsonObject args)
    {
        var name = args["npc_name"]?.GetValue<string>() ?? "";
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady)
                return "No game is loaded.";

            var npc = Game1.getCharacterFromName(name);
            if (npc is null)
                return $"NPC '{name}' not found.";

            var sb = new StringBuilder();
            sb.AppendLine($"Name: {npc.Name}");
            sb.AppendLine($"Birthday: {npc.Birthday_Season} {npc.Birthday_Day}");

            if (Game1.player.friendshipData.TryGetValue(npc.Name, out var friendship))
            {
                var hearts = friendship.Points / 250;
                var maxHearts = npc.datable.Value ? 14 : 10;
                var status = friendship.IsMarried() ? "Married"
                    : friendship.IsRoommate() ? "Roommate"
                    : friendship.IsDating() ? "Dating"
                    : "Friend";

                sb.AppendLine($"Friendship: {friendship.Points} pts ({hearts}/{maxHearts / 2} hearts) — {status}");
                sb.AppendLine($"Talked today: {(friendship.TalkedToToday ? "yes" : "no")}");
                sb.AppendLine($"Gifts this week: {friendship.GiftsThisWeek}/2{(friendship.GiftsToday != 0 ? " (gifted today)" : "")}");
            }
            else
            {
                sb.AppendLine("Friendship: not yet met");
            }

            // Today's schedule
            if (npc.Schedule is { Count: > 0 })
            {
                sb.AppendLine("Schedule today:");
                foreach (var (time, entry) in npc.Schedule.OrderBy(e => e.Key))
                {
                    var h = time / 100;
                    var m = time % 100;
                    var ampm = h >= 12 ? "PM" : "AM";
                    var h12 = h > 12 ? h - 12 : h == 0 ? 12 : h;
                    sb.AppendLine($"  {h12}:{m:D2} {ampm} → {entry.targetLocationName} ({entry.targetTile.X}, {entry.targetTile.Y})");
                }
            }
            else
            {
                sb.AppendLine("Schedule: none (may be following a special routine)");
            }

            return sb.ToString().TrimEnd();
        });
    }

    private static Task<string> GetNpcGiftPreferences(JsonObject args)
    {
        var name = args["npc_name"]?.GetValue<string>() ?? "";
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady)
                return "No game is loaded.";

            var npc = Game1.getCharacterFromName(name);
            if (npc is null)
                return $"NPC '{name}' not found.";

            if (!Game1.NPCGiftTastes.TryGetValue(npc.Name, out var tasteData))
                return $"No gift taste data found for {npc.Name}.";

            // Format: loved_items/loved_categories/liked_items/liked_categories/...
            var parts = tasteData.Split('/');

            var sb = new StringBuilder();
            sb.AppendLine($"Gift preferences for {npc.Name}:");
            sb.AppendLine($"\nLoved:");
            AppendGiftList(sb, parts.Length > 0 ? parts[0] : "", parts.Length > 1 ? parts[1] : "");
            sb.AppendLine($"\nLiked:");
            AppendGiftList(sb, parts.Length > 2 ? parts[2] : "", parts.Length > 3 ? parts[3] : "");
            sb.AppendLine($"\nDisliked:");
            AppendGiftList(sb, parts.Length > 4 ? parts[4] : "", parts.Length > 5 ? parts[5] : "");
            sb.AppendLine($"\nHated:");
            AppendGiftList(sb, parts.Length > 6 ? parts[6] : "", parts.Length > 7 ? parts[7] : "");

            return sb.ToString().TrimEnd();
        });
    }

    private static void AppendGiftList(StringBuilder sb, string itemIds, string categoryIds)
    {
        var items = itemIds.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(id => ItemRegistry.GetDataOrErrorItem($"(O){id}").DisplayName)
            .ToList();

        var categories = categoryIds.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(id => int.TryParse(id, out var n) ? CategoryName(n) : id)
            .Where(s => s is not null)
            .ToList();

        if (items.Count == 0 && categories.Count == 0)
        {
            sb.AppendLine("  (none)");
            return;
        }
        foreach (var item in items)
            sb.AppendLine($"  {item}");
        foreach (var cat in categories)
            sb.AppendLine($"  [all {cat}]");
    }

    private static Task<string> GetAllFriendships(JsonObject args)
    {
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady)
                return "No game is loaded.";

            if (Game1.player.friendshipData.Count() == 0)
                return "No friendships yet.";

            var sb = new StringBuilder();
            var keys = Game1.player.friendshipData.Keys
                .OrderByDescending(k => Game1.player.friendshipData[k].Points)
                .ToList();

            foreach (var key in keys)
            {
                var friendship = Game1.player.friendshipData[key];
                var hearts = friendship.Points / 250;
                var status = friendship.IsMarried() ? " 💍"
                    : friendship.IsRoommate() ? " 🏠"
                    : friendship.IsDating() ? " 💕"
                    : "";
                var talked = friendship.TalkedToToday ? " ✓talked" : "";
                var gifted = friendship.GiftsToday != 0 ? " ✓gifted" : "";
                sb.AppendLine($"{key}: {hearts}♥{status}{talked}{gifted}");
            }

            return sb.ToString().TrimEnd();
        });
    }

    private static Task<string> GetSpouseInfo(JsonObject args)
    {
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady)
                return "No game is loaded.";

            if (string.IsNullOrEmpty(Game1.player.spouse))
                return "You are not married.";

            var spouseName = Game1.player.spouse;
            var npc = Game1.getCharacterFromName(spouseName);
            var sb = new StringBuilder();

            sb.AppendLine($"Spouse: {spouseName}");

            if (npc is not null)
            {
                var tile = npc.TilePoint;
                sb.AppendLine($"Current location: {npc.currentLocation?.Name ?? "unknown"} ({tile.X}, {tile.Y})");
            }

            if (Game1.player.friendshipData.TryGetValue(spouseName, out var friendship))
            {
                var hearts = friendship.Points / 250;
                sb.AppendLine($"Friendship: {friendship.Points} pts ({hearts}/14 hearts)");
                sb.AppendLine($"Talked today: {(friendship.TalkedToToday ? "yes" : "no")}");
                sb.AppendLine($"Roommate marriage: {(friendship.IsRoommate() ? "yes" : "no")}");

                // Stardrop is given at 12.5 hearts (3125 pts), tracked via mail
                var starDropMailKey = $"CF_Spouse";
                var hasStardrop = Game1.player.mailReceived.Contains(starDropMailKey);
                sb.AppendLine($"Stardrop given: {(hasStardrop ? "yes" : friendship.Points >= 3125 ? "yes (≥12.5 hearts)" : "no (need 12.5 hearts)")}");
            }

            // Divorce pending
            if (Game1.player.divorceTonight.Value)
                sb.AppendLine("⚠ Divorce pending tonight.");

            return sb.ToString().TrimEnd();
        });
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static string? CategoryName(int id) => id switch
    {
        -2  => "Gems",
        -4  => "Fish",
        -5  => "Eggs",
        -6  => "Milk",
        -7  => "Cooked Food",
        -8  => "Crafted Items",
        -12 => "Minerals",
        -14 => "Meat",
        -15 => "Metal Bars",
        -16 => "Building Resources",
        -19 => "Fertilizer",
        -20 => "Trash",
        -21 => "Bait",
        -22 => "Tackle",
        -24 => "Furniture",
        -26 => "Artisan Goods",
        -27 => "Syrups",
        -28 => "Monster Loot",
        -74 => "Seeds",
        -75 => "Bars",
        -79 => "Fruit",
        -80 => "Flowers",
        -81 => "Forage",
        -96 => "Rings",
        -98 => "Boots",
        -99 => "Weapons",
        _   => $"Category {id}"
    };

    private static int SeasonIndex(string? season) => season?.ToLower() switch
    {
        "spring" => 0,
        "summer" => 1,
        "fall"   => 2,
        "winter" => 3,
        _        => -1
    };

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

    private static (string, JsonObject) Int(string name, string description) =>
        (name, new JsonObject { ["type"] = "integer", ["description"] = description });
}
