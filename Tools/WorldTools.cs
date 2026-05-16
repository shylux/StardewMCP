using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using System.Text;
using System.Text.Json.Nodes;

namespace StardewMCP.Tools;

public static class WorldTools
{
    public static void Register(ToolRegistry registry)
    {
        registry.Add(
            Tool("get_game_time",
                "Get the current in-game date and time (season, day, year, time of day).",
                Props()),
            GetGameTime
        );

        registry.Add(
            Tool("get_weather",
                "Get today's weather and tomorrow's forecast.",
                Props()),
            GetWeather
        );

        registry.Add(
            Tool("find_item",
                "Search everywhere for an item by name: chests, storage furniture (dressers, cabinets), items placed on tables, dropped items on the ground in all locations, the item recovery service, and the lost & found box.",
                Props(Str("item_name", "Partial or full item name to search for, e.g. 'Watering Can', 'Coal', 'Parsnip'"))),
            FindItem
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

        registry.Add(
            Tool("get_surroundings",
                "Scan the player's current location for nearby entities: NPCs, items/machines, crops, trees, bushes, buildings, and exits. Returns distance and direction from the player.",
                Props(Int("radius", "Tile radius to scan (default: 15)"))),
            GetSurroundings
        );
    }

    // ── Handlers ────────────────────────────────────────────────────────────

    private static Task<string> GetSurroundings(JsonObject args)
    {
        var radius = args["radius"]?.GetValue<int>() ?? 15;
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady)
                return "No game is loaded.";

            var player = Game1.player;
            var location = player.currentLocation;
            var ox = player.TilePoint.X;
            var oy = player.TilePoint.Y;

            static double Dist(float ox, float oy, float tx, float ty)
                => Math.Sqrt((tx - ox) * (tx - ox) + (ty - oy) * (ty - oy));

            static string Dir(float ox, float oy, float tx, float ty)
            {
                var dx = tx - ox;
                var dy = ty - oy;
                if (dx == 0 && dy == 0) return "here";
                var angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                return angle switch
                {
                    <= -157.5 or > 157.5 => "W",
                    <= -112.5 => "NW",
                    <= -67.5 => "N",
                    <= -22.5 => "NE",
                    <= 22.5 => "E",
                    <= 67.5 => "SE",
                    <= 112.5 => "S",
                    _ => "SW"
                };
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Surroundings within {radius} tiles of player ({ox}, {oy}) in {location.Name}:\n");

            // ── NPCs ────────────────────────────────────────────────────────
            var npcs = location.characters
                .Where(c => c is not null && !c.IsMonster)
                .Select(c => (d: Dist(ox, oy, c.TilePoint.X, c.TilePoint.Y), text: $"{c.Name} ({Dir(ox, oy, c.TilePoint.X, c.TilePoint.Y)}, {Dist(ox, oy, c.TilePoint.X, c.TilePoint.Y):F1}t)"))
                .Where(x => x.d <= radius)
                .OrderBy(x => x.d)
                .Select(x => x.text)
                .ToList();
            
            if (npcs.Count > 0)
            {
                sb.AppendLine("NPCs:");
                foreach (var n in npcs) sb.AppendLine($"  {n}");
            }

            // ── Objects / Machines ───────────────────────────────────────────
            var objects = new List<(double d, string text)>();
            foreach (var (tile, obj) in location.objects.Pairs)
            {
                var d = Dist(ox, oy, tile.X, tile.Y);
                if (d > radius) continue;
                string desc;
                if (obj.bigCraftable.Value)
                {
                    if (obj.heldObject.Value is not null)
                        desc = obj.readyForHarvest.Value
                            ? $"{obj.DisplayName}: {obj.heldObject.Value.DisplayName} (ready)"
                            : $"{obj.DisplayName}: processing {obj.heldObject.Value.DisplayName}";
                    else
                        desc = $"{obj.DisplayName} (idle)";
                }
                else
                {
                    desc = obj.DisplayName;
                }
                objects.Add((d, $"{desc} ({Dir(ox, oy, tile.X, tile.Y)}, {d:F1}t) at ({tile.X},{tile.Y})"));
            }
            // Dropped items (debris)
            foreach (var debris in location.debris)
            {
                if (debris.item is null) continue;
                if (debris.Chunks.Count == 0) continue;
                var px = debris.Chunks[0].position.X / Game1.tileSize;
                var py = debris.Chunks[0].position.Y / Game1.tileSize;
                var d = Dist(ox, oy, px, py);
                if (d > radius) continue;
                var stackInfo = debris.item.Stack > 1 ? $" x{debris.item.Stack}" : "";
                objects.Add((d, $"Dropped: {debris.item.DisplayName}{stackInfo} ({Dir(ox, oy, px, py)}, {d:F1}t)"));
            }

            if (objects.Count > 0)
            {
                sb.AppendLine("\nObjects/Machines:");
                foreach (var (_, text) in objects.OrderBy(x => x.d))
                    sb.AppendLine($"  {text}");
            }

            // ── Terrain Features ─────────────────────────────────────────────
            var terrain = new List<(double d, string text)>();
            foreach (var (tile, tf) in location.terrainFeatures.Pairs)
            {
                var d = Dist(ox, oy, tile.X, tile.Y);
                if (d > radius) continue;
                string? desc = null;
                if (tf is HoeDirt dirt)
                {
                    if (dirt.crop is not null)
                    {
                        var cropData = dirt.crop.GetData();
                        var name = cropData is not null
                            ? ItemRegistry.GetDataOrErrorItem("(O)" + cropData.HarvestItemId).DisplayName
                            : "Unknown Crop";
                        var state = dirt.crop.fullyGrown.Value || dirt.crop.currentPhase.Value >= dirt.crop.phaseDays.Count - 1
                            ? "ready"
                            : $"{dirt.crop.phaseDays.Count - 1 - dirt.crop.currentPhase.Value} phases left";
                        var watered = dirt.state.Value == HoeDirt.watered ? "watered" : "dry";
                        desc = $"Crop: {name} ({state}, {watered})";
                    }
                    else
                    {
                        desc = "Tilled soil (empty)";
                    }
                }
                else if (tf is Bush bush)
                {
                    var bushType = bush.size.Value switch
                    {
                        Bush.smallBush => "Small bush",
                        Bush.mediumBush => "Medium bush",
                        Bush.largeBush => "Large bush",
                        Bush.walnutBush => "Walnut bush",
                        _ => "Bush"
                    };
                    desc = bush.tileSheetOffset.Value == 1 ? $"{bushType} (has berries)" : bushType;
                }
                else if (tf is Tree tree)
                {
                    var stage = tree.growthStage.Value switch
                    {
                        0 => "seed",
                        1 => "sprout",
                        2 => "sapling",
                        3 => "bush",
                        4 => "small tree",
                        _ => "full tree"
                    };
                    var tapped = tree.tapped.Value ? " (tapped)" : "";
                    var hasSeed = tree.hasSeed.Value ? " (has seed)" : "";
                    desc = $"Tree ({stage}{tapped}{hasSeed})";
                }
                else if (tf is FruitTree fruitTree)
                {
                    var fruitData = ItemRegistry.GetData("(O)" + fruitTree.treeId.Value);
                    var fruitName = fruitData?.DisplayName ?? fruitTree.treeId.Value;
                    var fruitCount = fruitTree.fruit.Count;
                    desc = fruitCount > 0
                        ? $"Fruit Tree: {fruitName} ({fruitCount} fruit ready)"
                        : $"Fruit Tree: {fruitName} (no fruit)";
                }
                if (desc is not null)
                    terrain.Add((d, $"{desc} ({Dir(ox, oy, tile.X, tile.Y)}, {d:F1}t) at ({tile.X},{tile.Y})"));
            }

            // Large terrain objects (resource clumps)
            foreach (var clump in location.resourceClumps)
            {
                var tx = clump.Tile.X + clump.width.Value / 2;
                var ty = clump.Tile.Y + clump.height.Value / 2;
                var d = Dist(ox, oy, (int)tx, (int)ty);
                if (d > radius) continue;
                var name = clump.parentSheetIndex.Value switch
                {
                    600 => "Large Stump",
                    602 => "Large Log",
                    622 => "Large Rock (Meteorite)",
                    672 => "Large Rock",
                    _ => $"Resource Clump ({clump.parentSheetIndex.Value})"
                };
                terrain.Add((d, $"{name} ({Dir(ox, oy, (int)tx, (int)ty)}, {d:F1}t) at ({clump.Tile.X},{clump.Tile.Y})"));
            }

            if (terrain.Count > 0)
            {
                sb.AppendLine("\nTerrain:");
                foreach (var (_, text) in terrain.OrderBy(x => x.d))
                    sb.AppendLine($"  {text}");
            }

            // ── Buildings ────────────────────────────────────────────────────
            if (location is Farm farm)
            {
                var buildings = farm.buildings
                    .Select(b => {
                        var tx = b.tileX.Value + b.tilesWide.Value / 2;
                        var ty = b.tileY.Value + b.tilesHigh.Value / 2;
                        var d = Dist(ox, oy, tx, ty);
                        return (d, text: $"{b.buildingType.Value} ({Dir(ox, oy, tx, ty)}, {d:F1}t) at ({b.tileX.Value},{b.tileY.Value})");
                    })
                    .Where(x => x.d <= radius)
                    .OrderBy(x => x.d)
                    .ToList();

                if (buildings.Count > 0)
                {
                    sb.AppendLine("\nBuildings:");
                    foreach (var (_, text) in buildings)
                        sb.AppendLine($"  {text}");
                }
            }

            // ── Exits / Warps ────────────────────────────────────────────────
            var exits = location.warps
                .Select(w => (d: Dist(ox, oy, w.X, w.Y), text: $"Exit → {w.TargetName} ({Dir(ox, oy, w.X, w.Y)}, {Dist(ox, oy, w.X, w.Y):F1}t) at ({w.X},{w.Y})"))
                .Where(x => x.d <= radius)
                .OrderBy(x => x.d)
                .Select(x => x.text)
                .ToList();

            if (exits.Count > 0)
            {
                sb.AppendLine("\nExits:");
                foreach (var e in exits) sb.AppendLine($"  {e}");
            }

            if (sb.ToString().Trim().Split('\n').Length <= 1)
                sb.AppendLine("Nothing notable nearby.");

            return sb.ToString().TrimEnd();
        });
    }

    private static Task<string> GetWeather(JsonObject args)
    {
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady)
                return "No game is loaded.";

            string today;
            if (Game1.isGreenRain) today = "Green Rain";
            else if (Game1.isLightning) today = "Thunderstorm";
            else if (Game1.isRaining) today = "Rainy";
            else if (Game1.isSnowing) today = "Snowing";
            else if (Game1.isDebrisWeather) today = "Windy";
            else today = "Sunny";

            var tomorrow = Game1.weatherForTomorrow switch
            {
                Game1.weather_sunny => "Sunny",
                Game1.weather_rain => "Rainy",
                Game1.weather_debris => "Windy",
                Game1.weather_lightning => "Thunderstorm",
                Game1.weather_festival => "Festival day",
                Game1.weather_snow => "Snowing",
                Game1.weather_wedding => "Wedding day",
                Game1.weather_green_rain => "Green Rain",
                _ => $"Unknown ({Game1.weatherForTomorrow})"
            };

            return $"Today: {today}\nTomorrow: {tomorrow}";
        });
    }

    private static Task<string> GetGameTime(JsonObject args)
    {
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady)
                return "No game is loaded.";

            var date = Game1.Date;
            var rawTime = Game1.timeOfDay;
            var hour = rawTime / 100;
            var minute = rawTime % 100;
            var ampm = hour >= 12 ? "PM" : "AM";
            var hour12 = hour > 12 ? hour - 12 : hour == 0 ? 12 : hour;

            return $"Season: {date.Season}\n" +
                   $"Day: {date.DayOfMonth}\n" +
                   $"Year: {date.Year}\n" +
                   $"Time: {hour12}:{minute:D2} {ampm}";
        });
    }

    private static Task<string> FindItem(JsonObject args)
    {
        var search = args["item_name"]?.GetValue<string>() ?? "";
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady)
                return "No game is loaded.";

            var results = new List<string>();

            bool Matches(Item item) =>
                item.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                item.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase);

            void Add(Item item, string where)
            {
                var stack = item.Stack > 1 ? $" x{item.Stack}" : "";
                results.Add($"{item.DisplayName}{stack} — {where}");
            }

            foreach (var location in Game1.locations)
            {
                // Chests
                foreach (var (tile, obj) in location.objects.Pairs)
                {
                    if (obj is not Chest chest) continue;
                    foreach (var item in chest.Items)
                        if (item is not null && Matches(item))
                            Add(item, $"chest at {location.Name} ({(int)tile.X}, {(int)tile.Y})");
                }

                // Furniture: storage (dressers/cabinets) and items placed on tables
                foreach (var furniture in location.furniture)
                {
                    var fpos = $"{location.Name} ({(int)furniture.TileLocation.X}, {(int)furniture.TileLocation.Y})";
                    if (furniture is StorageFurniture storage)
                    {
                        foreach (var item in storage.heldItems)
                            if (item is not null && Matches(item))
                                Add(item, $"inside {furniture.DisplayName} at {fpos}");
                    }
                    else if (furniture.heldObject.Value is { } held && Matches(held))
                    {
                        Add(held, $"on {furniture.DisplayName} at {fpos}");
                    }
                }

                // Dropped items (debris) on the ground
                foreach (var debris in location.debris)
                {
                    if (debris.item is null || !Matches(debris.item)) continue;
                    if (debris.Chunks.Count == 0) continue;
                    var tx = (int)(debris.Chunks[0].position.X / Game1.tileSize);
                    var ty = (int)(debris.Chunks[0].position.Y / Game1.tileSize);
                    Add(debris.item, $"dropped on ground in {location.Name} ({tx}, {ty})");
                }
            }

            // Item recovery service (items lost when passing out in dangerous areas)
            foreach (var item in Game1.player.itemsLostLastDeath)
                if (item is not null && Matches(item))
                    Add(item, "item recovery service (Marlon, Adventurer's Guild)");

            // Lost & found box (Mayor Lewis's house — chest backed by a global inventory)
            var manorHouse = Game1.getLocationFromName("ManorHouse");
            if (manorHouse is not null)
            {
                foreach (var (_, obj) in manorHouse.objects.Pairs)
                {
                    if (obj is not Chest lostChest || lostChest.GlobalInventoryId is null) continue;
                    foreach (var item in Game1.player.team.GetOrCreateGlobalInventory(lostChest.GlobalInventoryId))
                        if (item is not null && Matches(item))
                            Add(item, "lost and found box (Mayor Lewis's house)");
                }
            }

            if (results.Count == 0)
                return $"No item matching '{search}' found.";

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

    private static (string, JsonObject) Int(string name, string description) =>
        (name, new JsonObject { ["type"] = "integer", ["description"] = description });
}
