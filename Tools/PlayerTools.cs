using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using StardewValley.Objects.Trinkets;
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
                "Warp the player to a named location. Use get_location_names to see valid locations. " +
                "When specifying explicit coordinates, use get_location_warps first and avoid those exit-trigger tiles — stepping on one immediately warps the player back out.",
                Props(
                    Str("location", "Target location name, e.g. Farm, Town, Beach, Mountain, Mine"),
                    Int("x", "Tile X coordinate (optional, uses a safe default if omitted)"),
                    Int("y", "Tile Y coordinate (optional, uses a safe default if omitted)")
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
                "Add an item to the player's inventory by name. Searches all item types registered in the game.",
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

        registry.Add(
            Tool("show_speech_bubble",
                "Show a speech bubble with text above a named NPC or monster.",
                Props(
                    Str("text", "The text to display in the bubble"),
                    Str("target", "The NPC or monster name, e.g. 'Abigail', 'Bat', 'Skeleton'"),
                    Int("duration", "How long to show the bubble in milliseconds (default: 3000)")
                )),
            ShowSpeechBubble
        );

        registry.Add(
            Tool("equip_item",
                "Equip an item directly to its slot. Supports hats, boots, rings, shirts, pants, and trinkets. " +
                "For rings, use slot 'left' or 'right' (default: left).",
                Props(
                    Str("item_name", "Item name to equip, e.g. 'Witch Hat', 'Infinity Boots', 'Iridium Band'"),
                    Str("slot", "Ring slot: 'left' or 'right' (only relevant for rings)")
                )),
            EquipItem
        );

        registry.Add(
            Tool("set_health",
                "Set the player's current health. Clamped to 1–max health. Use 99999 to fully heal.",
                Props(Int("health", "Health value to set"))),
            SetHealth
        );

        registry.Add(
            Tool("add_money",
                "Add or remove money from the player. Use a negative value to remove gold.",
                Props(Int("amount", "Amount of gold to add (negative to remove)"))),
            AddMoney
        );

        registry.Add(
            Tool("set_speed",
                "Set the player's extra movement speed. Base speed is 5; positive values make the player faster, negative values slower. Use 0 to reset.",
                Props(Int("speed", "Extra speed to add on top of base speed (e.g. 2 for noticeably faster, 5 for very fast)"))),
            SetSpeed
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
                var warp = location.warps.FirstOrDefault();
                if (warp is not null)
                {
                    // Step 2 tiles inward from whichever map edge the warp is closest to
                    var layer = location.map.Layers[0];
                    int mapW = layer.LayerWidth;
                    int mapH = layer.LayerHeight;
                    int wx = warp.X, wy = warp.Y;
                    int dLeft = wx, dRight = mapW - 1 - wx, dTop = wy, dBottom = mapH - 1 - wy;
                    int minDist = Math.Min(Math.Min(dLeft, dRight), Math.Min(dTop, dBottom));
                    int ax = wx, ay = wy;
                    if (minDist == dBottom) ay -= 2;
                    else if (minDist == dTop) ay += 2;
                    else if (minDist == dLeft) ax += 2;
                    else ax -= 2;
                    Game1.warpFarmer(locationName, ax, ay, false);
                }
                else
                {
                    Game1.warpFarmer(locationName, 10, 10, false);
                }

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

            foreach (var typeDef in ItemRegistry.ItemTypes)
            {
                foreach (var id in typeDef.GetAllIds())
                {
                    var qualifiedId = typeDef.Identifier + id;
                    var data = ItemRegistry.GetData(qualifiedId);
                    if (data is null) continue;
                    if (!data.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                        !data.InternalName.Contains(search, StringComparison.OrdinalIgnoreCase)) continue;
                    var item = ItemRegistry.Create(qualifiedId, quantity);
                    Game1.player.addItemByMenuIfNecessary(item);
                    return $"Added {data.DisplayName} to inventory.";
                }
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

    private static Task<string> ShowSpeechBubble(JsonObject args)
    {
        var text = args["text"]?.GetValue<string>() ?? "";
        var target = args["target"]?.GetValue<string>() ?? "";
        var duration = args["duration"]?.GetValue<int>() ?? 3000;

        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady)
                return "No game is loaded.";

            if (string.IsNullOrWhiteSpace(text))
                return "Text cannot be empty.";

            if (string.IsNullOrWhiteSpace(target))
                return "target is required (an NPC or monster name like 'Abigail' or 'Bat').";

            var npc = Game1.getCharacterFromName(target);
            npc ??= Game1.player.currentLocation.characters
                .FirstOrDefault(c => c.Name.Contains(target, StringComparison.OrdinalIgnoreCase));

            if (npc is null)
                return $"'{target}' not found.";

            npc.showTextAboveHead(text, null, NPC.textStyle_none, duration);
            return $"Speech bubble shown above {npc.Name}.";
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

    private static Task<string> EquipItem(JsonObject args)
    {
        var search = args["item_name"]?.GetValue<string>() ?? "";
        var slot = (args["slot"]?.GetValue<string>() ?? "left").ToLowerInvariant();

        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady)
                return "No game is loaded.";

            foreach (var typeDef in ItemRegistry.ItemTypes)
            {
                foreach (var id in typeDef.GetAllIds())
                {
                    var qualifiedId = typeDef.Identifier + id;
                    var data = ItemRegistry.GetData(qualifiedId);
                    if (data is null) continue;
                    if (!data.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                        !data.InternalName.Contains(search, StringComparison.OrdinalIgnoreCase)) continue;

                    var item = ItemRegistry.Create(qualifiedId);
                    var p = Game1.player;

                    if (item is Hat hat)
                    {
                        p.hat.Value = hat;
                        return $"Equipped hat: {data.DisplayName}.";
                    }
                    if (item is Boots boots)
                    {
                        p.boots.Value = boots;
                        return $"Equipped boots: {data.DisplayName}.";
                    }
                    if (item is Ring ring)
                    {
                        if (slot == "right")
                            p.rightRing.Value = ring;
                        else
                            p.leftRing.Value = ring;
                        return $"Equipped ring to {slot} slot: {data.DisplayName}.";
                    }
                    if (item is Clothing clothing)
                    {
                        if (clothing.clothesType.Value == Clothing.ClothesType.Shirt)
                        {
                            p.shirtItem.Value = clothing;
                            return $"Equipped shirt: {data.DisplayName}.";
                        }
                        p.pantsItem.Value = clothing;
                        return $"Equipped pants: {data.DisplayName}.";
                    }
                    if (item is Trinket trinket)
                    {
                        p.trinketItem.Value = trinket;
                        return $"Equipped trinket: {data.DisplayName}.";
                    }

                    return $"'{data.DisplayName}' ({typeDef.Identifier}) cannot be equipped.";
                }
            }

            return $"No item matching '{search}' found.";
        });
    }

    private static Task<string> SetHealth(JsonObject args)
    {
        var health = args["health"]?.GetValue<int>() ?? 0;
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady) return "No game is loaded.";
            var p = Game1.player;
            p.health = Math.Clamp(health, 1, p.maxHealth);
            return $"Health set to {p.health}/{p.maxHealth}.";
        });
    }

    private static Task<string> AddMoney(JsonObject args)
    {
        var amount = args["amount"]?.GetValue<int>() ?? 0;
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady) return "No game is loaded.";
            Game1.player.Money = Math.Max(0, Game1.player.Money + amount);
            return $"Money {(amount >= 0 ? "+" : "")}{amount}g. Total: {Game1.player.Money}g.";
        });
    }

    private static Task<string> SetSpeed(JsonObject args)
    {
        var speed = args["speed"]?.GetValue<int>() ?? 0;
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady) return "No game is loaded.";
            DebugCommands.TryHandle(new[] { "Speed", speed.ToString() });
            return $"Speed set to +{speed}.";
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
