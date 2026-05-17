using StardewModdingAPI;
using StardewValley;
using StardewValley.Companions;
using StardewValley.Objects;
using StardewValley.Objects.Trinkets;
using System.Text.Json.Nodes;
using static StardewMCP.Tools.ToolRegistry;

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
                "Equip an item to its slot, or clear a slot by omitting item_name. " +
                "Supports hats, boots, rings, shirts, pants, and trinkets. " +
                "slot values: 'left'/'right' for rings, or 'hat', 'boots', 'shirt', 'pants', 'trinket' to unequip that slot.",
                Props(
                    Str("item_name", "Item name to equip. Omit or leave empty to unequip the slot instead."),
                    Str("slot", "Ring slot ('left'/'right'), or slot to clear when item_name is empty: hat, boots, left ring, right ring, shirt, pants, trinket.")
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

        registry.Add(
            Tool("set_skill_level",
                "Set a player skill to a specific level (0–10). Skills: farming, fishing, foraging, mining, combat.",
                Props(
                    Str("skill", "Skill name: farming, fishing, foraging, mining, or combat"),
                    Int("level", "Level to set (0–10)")
                )),
            SetSkillLevel
        );

        registry.Add(
            Tool("add_recipe",
                "Unlock a crafting or cooking recipe by exact name (e.g. 'Chest', 'Fried Egg').",
                Props(
                    Str("name", "Exact recipe name as it appears in the game"),
                    Str("type", "Recipe type: 'crafting' (default) or 'cooking'")
                )),
            AddRecipe
        );

        registry.Add(
            Tool("add_profession",
                "Add a profession to the player by numeric ID. " +
                "Farming lvl5: Rancher=0, Tiller=1. Farming lvl10: Coopmaster=2, Shepherd=3, Artisan=4, Agriculturist=5. " +
                "Fishing lvl5: Fisher=6, Trapper=7. Fishing lvl10: Angler=8, Pirate=9, Mariner=10, Luremaster=11. " +
                "Foraging lvl5: Forester=12, Gatherer=13. Foraging lvl10: Lumberjack=14, Tapper=15, Botanist=16, Tracker=17. " +
                "Mining lvl5: Miner=18, Geologist=19. Mining lvl10: Blacksmith=20, Prospector=21, Excavator=22, Gemologist=23. " +
                "Combat lvl5: Fighter=24, Scout=25. Combat lvl10: Brute=26, Defender=27, Acrobat=28, Desperado=29.",
                Props(Int("id", "Profession ID (see description)"))),
            AddProfession
        );

        registry.Add(
            Tool("manage_quest",
                "Add, complete, or remove a quest by ID. Use action='clear' to clear the whole quest log.",
                Props(
                    Str("action", "One of: add, complete, remove, clear"),
                    Str("id", "Quest ID (required for add/complete/remove, omit for clear)")
                )),
            ManageQuest
        );

        registry.Add(
            Tool("add_walnut",
                "Add golden walnuts (used to unlock Ginger Island content).",
                Props(Int("amount", "Number of walnuts to add (default: 1)"))),
            AddWalnut
        );

        registry.Add(
            Tool("upgrade_house",
                "Set the farmhouse upgrade level. Level 0 = starter, 1 = kitchen, 2 = kids' room, 3 = cellar.",
                Props(Int("level", "Upgrade level 0–3"))),
            UpgradeHouse
        );

        registry.Add(
            Tool("toggle_invincible",
                "Toggle player invincibility on or off. While invincible the player cannot take damage.",
                Props()),
            ToggleInvincible
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


    private static void ReturnToInventory(Farmer p, Item? item)
    {
        if (item is not null)
            p.addItemToInventory(item);
    }

    private static Task<string> EquipItem(JsonObject args)
    {
        var search = args["item_name"]?.GetValue<string>() ?? "";
        var slot = (args["slot"]?.GetValue<string>() ?? "left").ToLowerInvariant();

        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady)
                return "No game is loaded.";

            var p = Game1.player;
            if (string.IsNullOrWhiteSpace(search))
            {
                switch (slot)
                {
                    case "hat": ReturnToInventory(p, p.hat.Value); p.hat.Value = null; return "Hat slot cleared.";
                    case "boots": ReturnToInventory(p, p.boots.Value); p.boots.Value = null; return "Boots slot cleared.";
                    case "left ring": case "left": ReturnToInventory(p, p.leftRing.Value); p.leftRing.Value = null; return "Left ring slot cleared.";
                    case "right ring": case "right": ReturnToInventory(p, p.rightRing.Value); p.rightRing.Value = null; return "Right ring slot cleared.";
                    case "shirt": ReturnToInventory(p, p.shirtItem.Value); p.shirtItem.Value = null; return "Shirt slot cleared.";
                    case "pants": ReturnToInventory(p, p.pantsItem.Value); p.pantsItem.Value = null; return "Pants slot cleared.";
                    case "trinket":
                        p.UnapplyAllTrinketEffects();
                        foreach (var t in p.trinketItems.ToList()) ReturnToInventory(p, t);
                        p.trinketItems.Clear();
                        foreach (var c in p.companions.ToList())
                            p.RemoveCompanion(c);
                        return "Trinket slot cleared.";
                    default: return $"Unknown slot '{slot}'. Valid: hat, boots, left ring, right ring, shirt, pants, trinket.";
                }
            }

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

                    if (item is Hat hat)
                    {
                        ReturnToInventory(p, p.hat.Value);
                        p.hat.Value = hat;
                        return $"Equipped hat: {data.DisplayName}.";
                    }
                    if (item is Boots boots)
                    {
                        ReturnToInventory(p, p.boots.Value);
                        p.boots.Value = boots;
                        return $"Equipped boots: {data.DisplayName}.";
                    }
                    if (item is Ring ring)
                    {
                        if (slot == "right")
                        {
                            ReturnToInventory(p, p.rightRing.Value);
                            p.rightRing.Value = ring;
                        }
                        else
                        {
                            ReturnToInventory(p, p.leftRing.Value);
                            p.leftRing.Value = ring;
                        }
                        return $"Equipped ring to {slot} slot: {data.DisplayName}.";
                    }
                    if (item is Clothing clothing)
                    {
                        if ((int)clothing.clothesType.Value == 0)
                        {
                            ReturnToInventory(p, p.shirtItem.Value);
                            p.shirtItem.Value = clothing;
                            return $"Equipped shirt: {data.DisplayName}.";
                        }
                        ReturnToInventory(p, p.pantsItem.Value);
                        p.pantsItem.Value = clothing;
                        return $"Equipped pants: {data.DisplayName}.";
                    }
                    if (item is Trinket trinket)
                    {
                        p.UnapplyAllTrinketEffects();
                        foreach (var t in p.trinketItems.ToList()) ReturnToInventory(p, t);
                        p.trinketItems.Clear();
                        p.trinketItems.Add(trinket);
                        p.ApplyAllTrinketEffects();
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

    private static readonly int[] SkillExpThresholds = [0, 100, 380, 770, 1300, 2150, 3300, 4800, 6900, 10000, 15000];

    private static Task<string> SetSkillLevel(JsonObject args)
    {
        var skill = (args["skill"]?.GetValue<string>() ?? "").Trim().ToLowerInvariant();
        var level = Math.Clamp(args["level"]?.GetValue<int>() ?? 10, 0, 10);

        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady) return "No game is loaded.";

            var p = Game1.player;
            int index;
            Action<int> setLevel;

            switch (skill)
            {
                case "farming":   index = 0; setLevel = v => p.farmingLevel.Value = v; break;
                case "fishing":   index = 1; setLevel = v => p.fishingLevel.Value = v; break;
                case "foraging":  index = 2; setLevel = v => p.foragingLevel.Value = v; break;
                case "mining":    index = 3; setLevel = v => p.miningLevel.Value = v; break;
                case "combat":    index = 4; setLevel = v => p.combatLevel.Value = v; break;
                default: return $"Unknown skill '{skill}'. Valid: farming, fishing, foraging, mining, combat.";
            }

            p.experiencePoints[index] = SkillExpThresholds[level];
            setLevel(level);
            return $"{char.ToUpper(skill[0]) + skill[1..]} set to level {level}.";
        });
    }

    private static Task<string> AddRecipe(JsonObject args)
    {
        var name = (args["name"]?.GetValue<string>() ?? "").Trim();
        var type = (args["type"]?.GetValue<string>() ?? "crafting").ToLowerInvariant();
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady) return "No game is loaded.";
            if (string.IsNullOrWhiteSpace(name)) return "Recipe name is required.";
            if (type == "cooking")
            {
                Game1.player.cookingRecipes[name] = 0;
                return $"Cooking recipe '{name}' unlocked.";
            }
            Game1.player.craftingRecipes[name] = 0;
            return $"Crafting recipe '{name}' unlocked.";
        });
    }

    private static Task<string> AddProfession(JsonObject args)
    {
        var id = args["id"]?.GetValue<int>() ?? 0;
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady) return "No game is loaded.";
            if (!Game1.player.professions.Contains(id))
                Game1.player.professions.Add(id);
            return $"Profession {id} added.";
        });
    }

    private static Task<string> ManageQuest(JsonObject args)
    {
        var action = (args["action"]?.GetValue<string>() ?? "").ToLowerInvariant();
        var id = (args["id"]?.GetValue<string>() ?? "").Trim();
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady) return "No game is loaded.";
            switch (action)
            {
                case "add":
                    if (string.IsNullOrWhiteSpace(id)) return "Quest ID required for action 'add'.";
                    Game1.player.addQuest(id);
                    return $"Quest '{id}' added.";
                case "complete":
                    if (string.IsNullOrWhiteSpace(id)) return "Quest ID required for action 'complete'.";
                    Game1.player.completeQuest(id);
                    return $"Quest '{id}' completed.";
                case "remove":
                    if (string.IsNullOrWhiteSpace(id)) return "Quest ID required for action 'remove'.";
                    Game1.player.removeQuest(id);
                    return $"Quest '{id}' removed.";
                case "clear":
                    Game1.player.questLog.Clear();
                    return "Quest log cleared.";
                default:
                    return $"Unknown action '{action}'. Valid: add, complete, remove, clear.";
            }
        });
    }

    private static Task<string> AddWalnut(JsonObject args)
    {
        var amount = args["amount"]?.GetValue<int>() ?? 1;
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady) return "No game is loaded.";
            DebugCommands.TryHandle(new[] { "Walnut", amount.ToString() });
            return $"Added {amount} golden walnut(s).";
        });
    }

    private static Task<string> UpgradeHouse(JsonObject args)
    {
        var level = Math.Clamp(args["level"]?.GetValue<int>() ?? 1, 0, 3);
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady) return "No game is loaded.";
            DebugCommands.TryHandle(new[] { "HouseUpgrade", level.ToString() });
            return $"Farmhouse set to upgrade level {level}.";
        });
    }

    private static Task<string> ToggleInvincible(JsonObject args)
    {
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady) return "No game is loaded.";
            var p = Game1.player;
            if (p.temporarilyInvincible)
            {
                p.temporaryInvincibilityTimer = 0;
                return "Invincibility disabled.";
            }
            p.temporarilyInvincible = true;
            p.temporaryInvincibilityTimer = -1_000_000_000; // large negative so the timer never expires naturally
            return "Invincibility enabled.";
        });
    }

}
