using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.BigCraftables;
using StardewValley.GameData.Objects;
using System.Text;
using System.Text.Json.Nodes;

namespace StardewMCP.Tools;

public static class PlayerTools
{
    public static void Register(ToolRegistry registry)
    {
        registry.Add(
            Tool("get_player_info",
                "Get the player's current location, tile position, health, energy, and money.",
                Props()),
            GetPlayerInfo,
            observeOnly: true
        );

        registry.Add(
            Tool("get_player_inventory",
                "List everything currently in the player's inventory with stack sizes.",
                Props()),
            GetPlayerInventory,
            observeOnly: true
        );

        registry.Add(
            Tool("teleport_player",
                "Warp the player to a named location. Use get_location_names to see valid locations.",
                Props(
                    Str("location", "Target location name, e.g. Farm, Town, Beach, Mountain, Mine"),
                    Int("x", "Tile X coordinate (optional, uses location default if omitted)"),
                    Int("y", "Tile Y coordinate (optional, uses location default if omitted)")
                )),
            TeleportPlayer
        );

        registry.Add(
            Tool("send_hud_message",
                "Display a message in the player's HUD (the small notification that appears in-game).",
                Props(Str("message", "The message text to display"))),
            SendHudMessage
        );

        registry.Add(
            Tool("add_item_to_inventory",
                "Add an item to the player's inventory by name. Searches regular items and craftables (e.g. Chest, Furnace).",
                Props(
                    Str("item_name", "Partial or full item name, e.g. 'Chest', 'Parsnip Seeds', 'Coal'"),
                    Int("quantity", "How many to add (default: 1)")
                )),
            AddItemToInventory
        );

        registry.Add(
            Tool("remove_item_from_inventory",
                "Remove an item from the player's inventory by name. Removes the specified quantity, or the entire stack if no quantity given.",
                Props(
                    Str("item_name", "Partial or full item name to remove, e.g. 'Chest', 'Coal'"),
                    Int("quantity", "How many to remove (default: entire stack)")
                )),
            RemoveItemFromInventory
        );

        registry.Add(
            Tool("player_emote",
                "Make the player perform an emote bubble. Available names: question, angry, exclamation, heart, sleep, sad, happy, x, pause, videogame, musicnote, blush. You can also pass a raw integer ID.",
                Props(Str("emote", "Emote name or raw integer ID, e.g. 'heart', 'sad', '32'"))),
            PlayerEmote
        );
    }

    // ── Handlers ────────────────────────────────────────────────────────────

    private static Task<string> GetPlayerInfo(JsonObject args)
    {
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady)
                return "No game is loaded.";

            var p = Game1.player;
            var tile = p.TilePoint;
            var loc = p.currentLocation?.Name ?? "unknown";
            return $"Name: {p.Name}\n" +
                   $"Location: {loc} ({tile.X}, {tile.Y})\n" +
                   $"Health: {p.health}/{p.maxHealth}\n" +
                   $"Energy: {(int)p.stamina}/{p.maxStamina}\n" +
                   $"Money: {p.Money}g\n" +
                   $"Farm name: {p.farmName}";
        });
    }

    private static Task<string> GetPlayerInventory(JsonObject args)
    {
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady)
                return "No game is loaded.";

            var items = Game1.player.Items
                .Where(i => i is not null)
                .Select(i => i!.Stack > 1 ? $"{i.Name} x{i.Stack}" : i.Name)
                .ToList();

            return items.Count > 0
                ? string.Join("\n", items)
                : "Inventory is empty.";
        });
    }

    private static Task<string> TeleportPlayer(JsonObject args)
    {
        var locationName = args["location"]?.GetValue<string>() ?? "";
        var hasX = args["x"] is not null;
        var hasY = args["y"] is not null;
        var x = args["x"]?.GetValue<int>() ?? 0;
        var y = args["y"]?.GetValue<int>() ?? 0;

        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady)
                return "No game is loaded.";

            var location = Game1.getLocationFromName(locationName);
            if (location is null)
                return $"Location '{locationName}' not found.";

            if (hasX && hasY)
            {
                Game1.warpFarmer(locationName, x, y, false);
                return $"Warped to {locationName} at ({x}, {y}).";
            }
            else
            {
                // Use the location's default warp point
                var warp = location.warps.FirstOrDefault();
                if (warp is not null)
                    Game1.warpFarmer(locationName, warp.X, warp.Y, false);
                else
                    Game1.warpFarmer(locationName, 10, 10, false);

                return $"Warped to {locationName}.";
            }
        });
    }

    private static Task<string> SendHudMessage(JsonObject args)
    {
        var message = args["message"]?.GetValue<string>() ?? "";
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady)
                return "No game is loaded.";

            Game1.addHUDMessage(new HUDMessage(message, HUDMessage.newQuest_type));
            return $"Message sent: \"{message}\"";
        });
    }

    private static Task<string> AddItemToInventory(JsonObject args)
    {
        var search = args["item_name"]?.GetValue<string>() ?? "";
        var quantity = args["quantity"]?.GetValue<int>() ?? 1;

        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady)
                return "No game is loaded.";

            // Search big craftables first (Chest, Furnace, etc.)
            var bcMatch = Game1.bigCraftableData
                .FirstOrDefault(kvp => kvp.Value.Name.Contains(search, StringComparison.OrdinalIgnoreCase));

            if (bcMatch.Key != null)
            {
                var item = ItemRegistry.Create($"(BC){bcMatch.Key}", quantity);
                Game1.player.addItemByMenuIfNecessary(item);
                return $"Added {bcMatch.Value.Name} to inventory.";
            }

            // Search regular objects
            var objMatch = Game1.objectData
                .FirstOrDefault(kvp => kvp.Value.Name.Contains(search, StringComparison.OrdinalIgnoreCase));

            if (objMatch.Key != null)
            {
                var item = ItemRegistry.Create($"(O){objMatch.Key}", quantity);
                Game1.player.addItemByMenuIfNecessary(item);
                return $"Added {quantity}x {objMatch.Value.Name} to inventory.";
            }

            return $"No item matching '{search}' found.";
        });
    }

    private static Task<string> RemoveItemFromInventory(JsonObject args)
    {
        var search = args["item_name"]?.GetValue<string>() ?? "";
        var quantity = args["quantity"]?.GetValue<int>();

        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady)
                return "No game is loaded.";

            for (int i = 0; i < Game1.player.Items.Count; i++)
            {
                var item = Game1.player.Items[i];
                if (item is null) continue;
                if (!item.Name.Contains(search, StringComparison.OrdinalIgnoreCase)) continue;

                var toRemove = quantity ?? item.Stack;
                var name = item.Name;

                if (toRemove >= item.Stack)
                {
                    Game1.player.Items[i] = null;
                    return $"Removed {name} from inventory.";
                }
                else
                {
                    item.Stack -= toRemove;
                    return $"Removed {toRemove}x {name} from inventory ({item.Stack} remaining).";
                }
            }

            return $"No item matching '{search}' found in inventory.";
        });
    }

    private static readonly Dictionary<string, int> EmoteIds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["question"]    = 8,
        ["?"]           = 8,
        ["angry"]       = 12,
        ["exclamation"] = 16,
        ["!"]           = 16,
        ["heart"]       = 20,
        ["love"]        = 20,
        ["sleep"]       = 24,
        ["zzz"]         = 24,
        ["sad"]         = 28,
        ["happy"]       = 32,
        ["smile"]       = 32,
        ["x"]           = 36,
        ["no"]          = 36,
        ["pause"]       = 40,
        ["..."]         = 40,
        ["videogame"]   = 52,
        ["game"]        = 52,
        ["musicnote"]   = 56,
        ["music"]       = 56,
        ["blush"]       = 60,
    };

    private static Task<string> PlayerEmote(JsonObject args)
    {
        var input = args["emote"]?.GetValue<string>() ?? "";
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady)
                return "No game is loaded.";

            int id;
            if (!int.TryParse(input, out id) && !EmoteIds.TryGetValue(input, out id))
            {
                var names = string.Join(", ", EmoteIds.Keys.Where(k => k.Length > 1 && k != "..."));
                return $"Unknown emote '{input}'. Valid names: {names}";
            }

            Game1.player.doEmote(id);
            return $"Emote performed (ID {id}).";
        });
    }

    // ── Schema builders (duplicated from NpcTools for independence) ──────────

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
