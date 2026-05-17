using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using System.Text;
using System.Text.Json.Nodes;
using xTile.Dimensions;
using static StardewMCP.Tools.ToolRegistry;

namespace StardewMCP.Tools;

public static class WorldTools
{
    public static void Register(ToolRegistry registry)
    {
        registry.Add(
            Tool("get_game_time",
                "Get the current in-game date and time (season, day, year, time of day).",
                Props()),
            GetGameTime,
            observeOnly: true
        );

        registry.Add(
            Tool("get_weather",
                "Get today's weather and tomorrow's forecast.",
                Props()),
            GetWeather,
            observeOnly: true
        );

        registry.Add(
            Tool("find_item",
                "Search everywhere for an item by name: chests, storage furniture (dressers, cabinets), items placed on tables, dropped items on the ground in all locations, the item recovery service, and the lost & found box.",
                Props(Str("item_name", "Partial or full item name to search for, e.g. 'Watering Can', 'Coal', 'Parsnip'"))),
            FindItem,
            observeOnly: true
        );

        registry.Add(
            Tool("list_items",
                "List every item in the world: chests, storage furniture, items on tables, dropped items, the item recovery service, and the lost & found box. Use find_item instead when you want to filter by name.",
                Props()),
            ListItems,
            observeOnly: true
        );

        registry.Add(
            Tool("list_registry_items",
                "List items from the game's item registry. Without a type, returns all registered types and their item counts. " +
                "With a type identifier (e.g. '(O)', '(W)', '(BC)', '(B)', '(R)', '(H)', '(F)', '(T)', '(TR)'), lists all items of that type.",
                Props(Str("type", "Item type identifier, e.g. '(W)' for weapons, '(O)' for objects. Omit to list all types."))),
            ListRegistryItems,
            observeOnly: true
        );

        registry.Add(
            Tool("get_location_names",
                "List all valid location names that can be used with teleport_player.",
                Props()),
            GetLocationNames,
            observeOnly: true
        );

        registry.Add(
            Tool("get_bundle_status",
                "Show every Community Center bundle with full item-by-item status: which items have been donated (✓) and which are still needed (✗), plus each bundle's reward. Organized by room.",
                Props()),
            GetBundleStatus,
            observeOnly: true
        );

        registry.Add(
            Tool("get_walkable_tiles",
                "Return a walkability grid for a given area. Shows which tiles the player can walk on. " +
                "Use this to find safe teleport destinations or understand the layout of an area.",
                Props(
                    Str("location_name", "Location to scan, e.g. Farm, Saloon, IslandFarmHouse"),
                    Int("x", "Center tile X"),
                    Int("y", "Center tile Y"),
                    Int("radius", "Tile radius to scan (default: 5, max: 20)")
                )),
            GetWalkableTiles,
            observeOnly: true
        );

        registry.Add(
            Tool("get_location_warps",
                "List all exit-trigger tiles in a location and where they lead. Avoid these tile positions when choosing teleport coordinates — stepping on them warps the player out of the location.",
                Props(Str("location_name", "Location name, e.g. FarmHouse, Farm, Town"))),
            GetLocationWarps,
            observeOnly: true
        );

        registry.Add(
            Tool("get_surroundings",
                "Scan the player's current location for nearby entities: NPCs, items/machines, crops, trees, bushes, buildings, and exits. Returns distance and direction from the player.",
                Props(Int("radius", "Tile radius to scan (default: 15)"))),
            GetSurroundings,
            observeOnly: true
        );

        registry.Add(
            Tool("set_weather",
                "Change the current weather immediately. Valid types: sunny, rain, thunderstorm, snow, windy.",
                Props(Str("weather", "Weather type: sunny, rain, thunderstorm, snow, or windy"))),
            SetWeather
        );

        registry.Add(
            Tool("set_time",
                "Set the current in-game time. Accepts formats like '6am', '2:30pm', '14:30', or a raw game time integer like 600 or 1430.",
                Props(Str("time", "Time to set, e.g. '8am', '2:30pm', '1800'"))),
            SetTime
        );

        registry.Add(
            Tool("set_date",
                "Set the current in-game date. All parameters are optional — only the ones provided will change.",
                Props(
                    Int("day", "Day of month (1–28)"),
                    Str("season", "Season: spring, summer, fall, or winter"),
                    Int("year", "Year number (1 or higher)")
                )),
            SetDate
        );

        registry.Add(
            Tool("advance_day",
                "Skip to the start of the next in-game day (equivalent to sleeping). Energy is restored and the calendar advances.",
                Props()),
            AdvanceDay
        );

        registry.Add(
            Tool("warp_to_mine_floor",
                "Warp directly to a specific mine floor. Floors 1–120 are the regular mines; floors 121+ are the Skull Cavern.",
                Props(Int("floor", "Mine floor number (e.g. 1, 40, 80, 120 for the regular mine; higher for Skull Cavern)"))),
            WarpToMineFloor
        );

        registry.Add(
            Tool("complete_community_center",
                "Mark all Community Center rooms as complete and set all required mail flags.",
                Props()),
            CompleteCommunityCenterCmd
        );

        registry.Add(
            Tool("grow_crops",
                "Advance all crops in the current location by N days of growth.",
                Props(Int("days", "Number of days to grow (default: 1)"))),
            GrowCrops
        );

        registry.Add(
            Tool("befriend_animals",
                "Set the friendship of all farm animals in the current location to a given value (max 1000).",
                Props(Int("friendship", "Friendship value 0–1000 (default: 1000 = max)"))),
            BefriendAnimals
        );

        registry.Add(
            Tool("pause_time",
                "Toggle in-game time pausing on or off. While paused the clock stops advancing.",
                Props()),
            PauseTime
        );

        registry.Add(
            Tool("clear_tile",
                "Remove whatever is on a specific tile in the player's current location: trees, bushes, grass, small rocks, twigs, weeds, and large stumps/logs. " +
                "Use get_surroundings or get_walkable_tiles to find tile coordinates.",
                Props(
                    Int("x", "Tile X coordinate"),
                    Int("y", "Tile Y coordinate")
                )),
            ClearTile
        );

        registry.Add(
            Tool("play_effect",
                "Play a visual effect at a tile position. " +
                "Effects: flash (white screen flash), glow (colored screen glow), rainbow (rainbow star burst), " +
                "sparkle (particle sprinkles), lightning (lightning bolt strike). " +
                "Position defaults to player's current tile if omitted.",
                Props(
                    Str("effect", "Effect name: flash, glow, rainbow, sparkle, lightning"),
                    Int("x", "Tile X (optional, defaults to player position)"),
                    Int("y", "Tile Y (optional, defaults to player position)"),
                    Str("color", "Color for glow effect: red, green, blue, yellow, purple, white (default: white)")
                )),
            PlayEffect
        );

        registry.Add(
            Tool("play_sound",
                "Play a one-shot sound effect by name. " +
                "Common sounds: coin, purchase, newArtifact, achievement, crystal, thunder, grunt, fishingRodCast, openChest, ship, drumkit0, stoneStep, hammer.",
                Props(
                    Str("sound", "Sound effect ID, e.g. 'coin', 'achievement', 'thunder'"),
                    Int("pitch", "Optional pitch in cents (0 = default, negative = lower, positive = higher)")
                )),
            PlaySound
        );

        registry.Add(
            Tool("play_music",
                "Change the background music to a named track, or stop it. " +
                "Common tracks: spring1, spring2, spring3, summer1, summer2, summer3, fall1, fall2, fall3, winter1, winter2, winter3, " +
                "rain, ocean, breezy, Stardew Valley Overture, wedding, movieTheater, submarine. " +
                "Use 'none' to stop the music.",
                Props(Str("track", "Track name, e.g. 'spring1', 'rain', 'none'"))),
            PlayMusic
        );

        registry.Add(
            Tool("set_mail_flag",
                "Add or remove a player mail flag to unlock game features. " +
                "NOTE: This tool can be buggy because there can be other things not tied to the flag that should also be changed. " +
                "Common flags: 'ccBoilerRoom' (unlocks minecarts), 'ccCraftsRoom' (quarry bridge), " +
                "'ccFishTank' (glittering boulder removed), 'ccPantry' (greenhouse), " +
                "'ccVault' (bus to desert), 'ccBulletin' (fireworks/town square), " +
                "'JojaMineCart' (Joja minecart upgrade), 'JojaVault' (Joja bus). " +
                "Use received=false to remove a flag.",
                Props(
                    Str("flag", "Mail flag name"),
                    Bool("received", "true to add the flag (default), false to remove it")
                )),
            SetMailFlag
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
            bool hasContent = false;

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
                hasContent = true;
                sb.AppendLine("NPCs:");
                foreach (var n in npcs) sb.AppendLine($"  {n}");
            }

            // ── Monsters ─────────────────────────────────────────────────────
            var monsters = location.characters
                .Where(c => c is not null && c.IsMonster)
                .Cast<StardewValley.Monsters.Monster>()
                .Select(m => (d: Dist(ox, oy, m.TilePoint.X, m.TilePoint.Y), text: $"{m.Name} ({Dir(ox, oy, m.TilePoint.X, m.TilePoint.Y)}, {Dist(ox, oy, m.TilePoint.X, m.TilePoint.Y):F1}t) HP:{m.Health}/{m.MaxHealth}"))
                .Where(x => x.d <= radius)
                .OrderBy(x => x.d)
                .Select(x => x.text)
                .ToList();

            if (monsters.Count > 0)
            {
                hasContent = true;
                sb.AppendLine("\nMonsters:");
                foreach (var m in monsters) sb.AppendLine($"  {m}");
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

            // ── Furniture ────────────────────────────────────────────────────
            var furniture = new List<(double d, string text)>();
            foreach (var f in location.furniture)
            {
                var d = Dist(ox, oy, f.TileLocation.X, f.TileLocation.Y);
                if (d > radius) continue;
                var kind = f switch
                {
                    TV => "[TV] ",
                    BedFurniture => "[Bed] ",
                    StorageFurniture => "[Storage] ",
                    _ => ""
                };
                var desc = kind + f.DisplayName;
                if (f.heldObject.Value is { } held)
                    desc += $" (holding {held.DisplayName})";
                furniture.Add((d, $"{desc} ({Dir(ox, oy, f.TileLocation.X, f.TileLocation.Y)}, {d:F1}t) at ({(int)f.TileLocation.X},{(int)f.TileLocation.Y})"));
            }

            if (furniture.Count > 0)
            {
                hasContent = true;
                sb.AppendLine("\nFurniture:");
                foreach (var (_, text) in furniture.OrderBy(x => x.d))
                    sb.AppendLine($"  {text}");
            }

            if (objects.Count > 0)
            {
                hasContent = true;
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
                hasContent = true;
                sb.AppendLine("\nTerrain:");
                foreach (var (_, text) in terrain.OrderBy(x => x.d))
                    sb.AppendLine($"  {text}");
            }

            // ── Buildings ────────────────────────────────────────────────────
            {
                var buildings = location.buildings
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
                    hasContent = true;
                    sb.AppendLine("\nBuildings:");
                    foreach (var (_, text) in buildings)
                        sb.AppendLine($"  {text}");
                }
            }

            // ── Tile Actions (ladders, elevators, mine carts, shops…) ────────
            var tileActions = new List<(double d, string text)>();
            for (int ty2 = oy - radius; ty2 <= oy + radius; ty2++)
            {
                for (int tx2 = ox - radius; tx2 <= ox + radius; tx2++)
                {
                    var action = location.doesTileHaveProperty(tx2, ty2, "Action", "Buildings");
                    if (action is null) continue;
                    if (action.StartsWith("Warp ")) continue; // already in Exits
                    var d = Dist(ox, oy, tx2, ty2);
                    if (d > radius) continue;
                    var parts = action.Split(' ', 2);
                    var label = parts[0];
                    var detail = parts.Length > 1 ? parts[1] : null;
                    // Resolve game string references (mirrors GameLocation.performAction Message logic)
                    if (detail != null)
                    {
                        string? resolved = null;
                        try { resolved = Game1.content.LoadStringReturnNullIfNotFound(detail.Trim()); } catch { }
                        if (resolved is null)
                            try { resolved = Game1.content.LoadStringReturnNullIfNotFound("Strings\\StringsFromMaps:" + detail.Trim().Replace("\"", "")); } catch { }
                        if (resolved != null)
                            detail = resolved;
                    }
                    var display = detail != null ? $"{label}: {detail}" : label;
                    tileActions.Add((d, $"{display} ({Dir(ox, oy, tx2, ty2)}, {d:F1}t) at ({tx2},{ty2})"));
                }
            }

            if (tileActions.Count > 0)
            {
                hasContent = true;
                sb.AppendLine("\nInteractive Tiles:");
                foreach (var (_, text) in tileActions.OrderBy(x => x.d))
                    sb.AppendLine($"  {text}");
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
                hasContent = true;
                sb.AppendLine("\nExits:");
                foreach (var e in exits) sb.AppendLine($"  {e}");
            }

            if (!hasContent)
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

            var result = $"Valley - Today: {today}\nValley - Tomorrow: {tomorrow}";

            var islandWeather = Game1.getLocationFromName("IslandSouth")?.GetWeather();
            if (islandWeather is not null)
            {
                string islandToday;
                if (islandWeather.isLightning.Value) islandToday = "Thunderstorm";
                else if (islandWeather.isRaining.Value) islandToday = "Rainy";
                else if (islandWeather.isSnowing.Value) islandToday = "Snowing";
                else if (islandWeather.isDebrisWeather.Value) islandToday = "Windy";
                else islandToday = "Sunny";

                var islandTomorrow = islandWeather.weatherForTomorrow.Value switch
                {
                    Game1.weather_sunny => "Sunny",
                    Game1.weather_rain => "Rainy",
                    Game1.weather_debris => "Windy",
                    Game1.weather_lightning => "Thunderstorm",
                    Game1.weather_snow => "Snowing",
                    _ => $"Unknown ({islandWeather.weatherForTomorrow.Value})"
                };

                result += $"\nGinger Island - Today: {islandToday}\nGinger Island - Tomorrow: {islandTomorrow}";
            }

            return result;
        });
    }

    private static Task<string> SetWeather(JsonObject args)
    {
        var weather = (args["weather"]?.GetValue<string>() ?? "").ToLowerInvariant();
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady)
                return "No game is loaded.";

            Game1.isRaining = false;
            Game1.isSnowing = false;
            Game1.isLightning = false;
            Game1.isDebrisWeather = false;

            switch (weather)
            {
                case "sunny":
                case "sun":
                    return "Weather changed to sunny.";
                case "rain":
                case "rainy":
                    Game1.isRaining = true;
                    return "Weather changed to rainy.";
                case "thunderstorm":
                case "storm":
                case "lightning":
                    Game1.isRaining = true;
                    Game1.isLightning = true;
                    return "Weather changed to thunderstorm.";
                case "snow":
                case "snowing":
                    Game1.isSnowing = true;
                    return "Weather changed to snowy.";
                case "windy":
                case "debris":
                    Game1.isDebrisWeather = true;
                    return "Weather changed to windy.";
                default:
                    return $"Unknown weather '{weather}'. Valid types: sunny, rain, thunderstorm, snow, windy.";
            }
        });
    }

    private static Task<string> SetDate(JsonObject args)
    {
        var day = args["day"]?.GetValue<int>();
        var season = args["season"]?.GetValue<string>()?.ToLowerInvariant();
        var year = args["year"]?.GetValue<int>();

        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady)
                return "No game is loaded.";

            if (day is null && season is null && year is null)
                return "Provide at least one of: day, season, year.";

            if (day is not null)
            {
                if (day < 1 || day > 28)
                    return "Day must be between 1 and 28.";
                DebugCommands.TryHandle(new[] { "Day", day.Value.ToString() });
            }

            if (season is not null)
            {
                if (season is not ("spring" or "summer" or "fall" or "winter"))
                    return $"Unknown season '{season}'. Use: spring, summer, fall, winter.";
                DebugCommands.TryHandle(new[] { "Season", season });
            }

            if (year is not null)
            {
                if (year < 1)
                    return "Year must be 1 or higher.";
                DebugCommands.TryHandle(new[] { "Year", year.Value.ToString() });
            }

            var d = Game1.Date;
            return $"Date set to {d.Season} {d.DayOfMonth}, Year {d.Year}.";
        });
    }

    private static Task<string> SetMailFlag(JsonObject args)
    {
        var flag = (args["flag"]?.GetValue<string>() ?? "").Trim();
        var received = args["received"]?.GetValue<bool>() ?? true;

        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady)
                return "No game is loaded.";

            if (string.IsNullOrWhiteSpace(flag))
                return "Flag name is required.";

            if (received)
            {
                if (!Game1.player.mailReceived.Contains(flag))
                    Game1.player.mailReceived.Add(flag);
                return $"Mail flag '{flag}' added.";
            }
            else
            {
                Game1.player.mailReceived.Remove(flag);
                return $"Mail flag '{flag}' removed.";
            }
        });
    }

    private static Task<string> SetTime(JsonObject args)
    {
        var input = (args["time"]?.GetValue<string>() ?? "").Trim();
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady)
                return "No game is loaded.";

            if (!TryParseGameTime(input, out int gameTime))
                return $"Could not parse time '{input}'. Use formats like '8am', '2:30pm', or '1430'.";

            DebugCommands.TryHandle(new[] { "Time", gameTime.ToString() });
            var h = gameTime / 100;
            var m = gameTime % 100;
            var ampm = h >= 12 ? "PM" : "AM";
            var h12 = h > 12 ? h - 12 : h == 0 ? 12 : h;
            return $"Time set to {h12}:{m:D2} {ampm}.";
        });
    }

    private static bool TryParseGameTime(string input, out int gameTime)
    {
        gameTime = 0;
        if (string.IsNullOrWhiteSpace(input)) return false;

        input = input.ToLowerInvariant().Replace(" ", "");

        bool isPm = input.EndsWith("pm");
        bool isAm = input.EndsWith("am");
        if (isPm || isAm)
            input = input[..^2];

        int hours, minutes = 0;
        if (input.Contains(':'))
        {
            var parts = input.Split(':');
            if (!int.TryParse(parts[0], out hours) || !int.TryParse(parts[1], out minutes))
                return false;
        }
        else if (!int.TryParse(input, out hours))
        {
            return false;
        }
        else if (hours >= 100)
        {
            // raw HHMM format e.g. 1430
            minutes = hours % 100;
            hours = hours / 100;
            gameTime = hours * 100 + minutes;
            return true;
        }

        if (isPm && hours != 12) hours += 12;
        if (isAm && hours == 12) hours = 0;

        gameTime = hours * 100 + minutes;
        return true;
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

    private static Task<string> ListItems(JsonObject args) => SearchItems("");

    private static Task<string> FindItem(JsonObject args)
    {
        var search = args["item_name"]?.GetValue<string>() ?? "";
        return SearchItems(search);
    }

    private static Task<string> SearchItems(string search)
    {
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady)
                return "No game is loaded.";

            var results = new List<string>();

            bool Matches(Item item) =>
                search.Length == 0 ||
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
                return search.Length == 0 ? "No items found anywhere." : $"No item matching '{search}' found.";

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

    private static Task<string> GetBundleStatus(JsonObject args)
    {
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady)
                return "No game is loaded.";

            if (Utility.hasFinishedJojaRoute())
                return "Joja route completed — Community Center was converted to a Joja warehouse.";

            var cc = Game1.locations.OfType<CommunityCenter>().FirstOrDefault();
            if (cc is null)
                return "Community Center location not found.";

            string[] areaNames = ["Crafts Room", "Pantry", "Fish Tank", "Boiler Room", "Vault", "Bulletin Board"];

            // Group bundle data by area name
            var bundlesByArea = new Dictionary<string, List<(int Index, string Data)>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, data) in Game1.netWorldState.Value.BundleData)
            {
                var slash = key.IndexOf('/');
                if (slash < 0) continue;
                var area = key[..slash];
                if (!int.TryParse(key[(slash + 1)..], out var idx)) continue;
                if (!bundlesByArea.ContainsKey(area))
                    bundlesByArea[area] = new();
                bundlesByArea[area].Add((idx, data));
            }

            var sb = new StringBuilder();
            sb.AppendLine(cc.areAllAreasComplete()
                ? "Community Center: FULLY RESTORED ✓\n"
                : "Community Center Bundle Status\n");

            for (int areaIdx = 0; areaIdx < areaNames.Length; areaIdx++)
            {
                var areaName = areaNames[areaIdx];
                var areaComplete = areaIdx < cc.areasComplete.Count && cc.areasComplete[areaIdx];
                var areaBundles = bundlesByArea.GetValueOrDefault(areaName) ?? new();

                int totalBundles = areaBundles.Count;
                int doneBundles = areaBundles.Count(b =>
                    cc.bundles.ContainsKey(b.Index) && cc.bundles[b.Index].All(v => v));

                var areaStatus = areaComplete ? "COMPLETE ✓" : $"{doneBundles}/{totalBundles} complete";
                sb.AppendLine($"{areaName} [{areaStatus}]");

                foreach (var (bundleIdx, bundleData) in areaBundles.OrderBy(b => b.Index))
                {
                    var parts = bundleData.Split('/');
                    var bundleName = parts.Length > 0 ? parts[0] : $"Bundle {bundleIdx}";
                    var rewardStr = parts.Length > 1 ? DescribeBundleReward(parts[1]) : "";

                    bool[] slots = cc.bundles.ContainsKey(bundleIdx)
                        ? cc.bundles[bundleIdx].ToArray()
                        : Array.Empty<bool>();
                    bool bundleComplete = slots.Length > 0 && slots.All(v => v);

                    var rewardLabel = string.IsNullOrEmpty(rewardStr) ? "" : $" — reward: {rewardStr}";
                    sb.AppendLine($"  {(bundleComplete ? "✓" : "✗")} {bundleName}{rewardLabel}");

                    // Show item slots only for incomplete bundles
                    if (!bundleComplete && parts.Length > 2)
                    {
                        var tokens = parts[2].Split(' ');
                        int slotIdx = 0;
                        for (int i = 0; i + 2 < tokens.Length; i += 3, slotIdx++)
                        {
                            var itemId = tokens[i];
                            if (itemId == "-1") continue;

                            int.TryParse(tokens[i + 1], out var quality);
                            int.TryParse(tokens[i + 2], out var amount);

                            var itemData = ItemRegistry.GetDataOrErrorItem($"(O){itemId}");
                            var itemName = itemData.DisplayName;
                            var qualityStr = quality switch { 1 => " (silver+)", 2 => " (gold+)", 4 => " (iridium)", _ => "" };
                            var amountStr = amount > 1 ? $" x{amount}" : "";
                            var donated = slotIdx < slots.Length && slots[slotIdx];
                            sb.AppendLine($"    {(donated ? "✓" : "✗")} {itemName}{amountStr}{qualityStr}");
                        }
                    }
                }

                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        });
    }

    private static string DescribeBundleReward(string rewardPart)
    {
        var tokens = rewardPart.Trim().Split(' ');
        if (tokens.Length == 0) return "";

        // Money reward: "Money 2000"
        if (tokens[0].Equals("Money", StringComparison.OrdinalIgnoreCase))
            return tokens.Length > 1 ? $"{tokens[1]}g" : "money";

        // Big Craftable: "BO itemId amount"
        if (tokens[0].Equals("BO", StringComparison.OrdinalIgnoreCase) && tokens.Length >= 3)
        {
            int.TryParse(tokens[2], out var amt);
            var name = ItemRegistry.GetDataOrErrorItem($"(BC){tokens[1]}").DisplayName;
            return amt > 1 ? $"{name} x{amt}" : name;
        }

        // Ring: "R itemId amount"
        if (tokens[0].Equals("R", StringComparison.OrdinalIgnoreCase) && tokens.Length >= 3)
        {
            int.TryParse(tokens[2], out var amt);
            var name = ItemRegistry.GetDataOrErrorItem($"(R){tokens[1]}").DisplayName;
            return amt > 1 ? $"{name} x{amt}" : name;
        }

        // Standard object: "itemId quality amount"
        if (tokens.Length >= 3 && int.TryParse(tokens[0], out var itemId) && itemId != -1)
        {
            int.TryParse(tokens[2], out var amt);
            var name = ItemRegistry.GetDataOrErrorItem($"(O){tokens[0]}").DisplayName;
            return amt > 1 ? $"{name} x{amt}" : name;
        }

        return "";
    }

    private static Task<string> ListRegistryItems(JsonObject args)
    {
        var type = args["type"]?.GetValue<string>()?.Trim();
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady)
                return "No game is loaded.";

            if (string.IsNullOrEmpty(type))
            {
                var lines = ItemRegistry.ItemTypes
                    .Select(t => $"{t.Identifier} — {t.GetAllIds().Count()} items")
                    .ToList();
                return "Registered item types:\n" + string.Join("\n", lines);
            }

            var typeDef = ItemRegistry.GetTypeDefinition(type);
            if (typeDef is null)
                return $"Unknown type '{type}'. Use list_registry_items without a type to see valid identifiers.";

            var items = typeDef.GetAllIds()
                .Select(id => ItemRegistry.GetData(typeDef.Identifier + id))
                .Where(d => d is not null)
                .Select(d => $"{d!.DisplayName} ({d.InternalName})")
                .OrderBy(s => s)
                .ToList();

            return items.Count == 0
                ? $"No items found for type '{type}'."
                : $"{type} items ({items.Count}):\n" + string.Join("\n", items);
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

            var sb2 = new StringBuilder($"Warps in {locationName}:\n");
            foreach (var w in location.warps)
            {
                sb2.AppendLine($"  ({w.X}, {w.Y}) → {w.TargetName} ({w.TargetX}, {w.TargetY})");
                var safe = WalkableTilesAround(location, w.X, w.Y, 1)
                    .Where(t => t != (w.X, w.Y))
                    .Select(t => $"({t.X},{t.Y})");
                sb2.AppendLine($"    Safe arrival tiles: {string.Join(", ", safe)}");
            }
            return sb2.ToString().TrimEnd();
        });
    }

    // ── Walkability ─────────────────────────────────────────────────────────

    private static bool IsTileWalkable(GameLocation location, int x, int y)
    {
        var layer = location.map.Layers[0];
        if (x < 0 || y < 0 || x >= layer.LayerWidth || y >= layer.LayerHeight) return false;
        return location.isTilePassable(new Location(x, y), Game1.viewport);
    }

    private static List<(int X, int Y)> WalkableTilesAround(GameLocation location, int cx, int cy, int radius)
    {
        var result = new List<(int, int)>();
        for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
                if (IsTileWalkable(location, cx + dx, cy + dy))
                    result.Add((cx + dx, cy + dy));
        return result;
    }

    private static Task<string> GetWalkableTiles(JsonObject args)
    {
        var locationName = args["location_name"]?.GetValue<string>() ?? "";
        var cx = args["x"]?.GetValue<int>() ?? 0;
        var cy = args["y"]?.GetValue<int>() ?? 0;
        var radius = Math.Clamp(args["radius"]?.GetValue<int>() ?? 5, 1, 20);

        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady)
                return "No game is loaded.";

            var location = Game1.getLocationFromName(locationName);
            if (location is null)
                return $"Location '{locationName}' not found.";

            // Build warp lookup within the scan area
            const string markers = "123456789abcdefghijklmnopqrstuvwxyz";
            var warpsInRange = location.warps
                .Where(w => Math.Abs(w.X - cx) <= radius && Math.Abs(w.Y - cy) <= radius)
                .ToList();
            var warpMarker = new Dictionary<(int, int), char>();
            for (int i = 0; i < warpsInRange.Count && i < markers.Length; i++)
                warpMarker[(warpsInRange[i].X, warpsInRange[i].Y)] = markers[i];

            var sb3 = new StringBuilder();
            sb3.AppendLine($"Walkability grid — {locationName} centered on ({cx},{cy}) radius {radius}:");
            sb3.AppendLine("  '.' = walkable, '#' = blocked, '+' = center, digit/letter = warp\n");

            for (int dy = -radius; dy <= radius; dy++)
            {
                sb3.Append("  ");
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int tx = cx + dx, ty = cy + dy;
                    if (dx == 0 && dy == 0)
                        sb3.Append('+');
                    else if (warpMarker.TryGetValue((tx, ty), out var m))
                        sb3.Append(m);
                    else
                        sb3.Append(IsTileWalkable(location, tx, ty) ? '.' : '#');
                }
                sb3.AppendLine();
            }

            var walkableCount = WalkableTilesAround(location, cx, cy, radius).Count;
            sb3.Append($"\n{walkableCount} walkable tile(s) in range.");

            if (warpMarker.Count > 0)
            {
                sb3.AppendLine("\n\nWarp legend:");
                for (int i = 0; i < warpsInRange.Count && i < markers.Length; i++)
                {
                    var w = warpsInRange[i];
                    sb3.AppendLine($"  {markers[i]}: ({w.X},{w.Y}) → {w.TargetName} ({w.TargetX},{w.TargetY})");
                }
            }

            return sb3.ToString();
        });
    }

    private static Task<string> ClearTile(JsonObject args)
    {
        var x = args["x"]?.GetValue<int>() ?? 0;
        var y = args["y"]?.GetValue<int>() ?? 0;

        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady) return "No game is loaded.";

            var location = Game1.player.currentLocation;
            var tile = new Vector2(x, y);
            var removed = new List<string>();

            // Terrain features: trees, bushes, grass, stumps, dirt
            if (location.terrainFeatures.ContainsKey(tile))
            {
                var tf = location.terrainFeatures[tile];
                removed.Add(tf.GetType().Name);
                location.terrainFeatures.Remove(tile);
            }

            // Natural debris only: rocks, twigs, weeds (not placed objects like chests/machines)
            if (location.objects.ContainsKey(tile))
            {
                var obj = location.objects[tile];
                if (obj.Name is "Stone" or "Twig" or "Weeds" or "Fiber")
                {
                    removed.Add(obj.Name);
                    location.objects.Remove(tile);
                }
            }

            // Resource clumps: large stumps, logs, boulders (multi-tile)
            var clumps = location.resourceClumps
                .Where(c => x >= c.Tile.X && x < c.Tile.X + c.width.Value &&
                            y >= c.Tile.Y && y < c.Tile.Y + c.height.Value)
                .ToList();
            foreach (var clump in clumps)
            {
                removed.Add($"resource clump ({clump.parentSheetIndex.Value})");
                location.resourceClumps.Remove(clump);
            }

            return removed.Count > 0
                ? $"Cleared at ({x}, {y}): {string.Join(", ", removed)}."
                : $"Nothing to clear at ({x}, {y}).";
        });
    }

    private static Task<string> PlayEffect(JsonObject args)
    {
        var effect = (args["effect"]?.GetValue<string>() ?? "").Trim().ToLowerInvariant();
        var hasX = args["x"] is not null;
        var hasY = args["y"] is not null;
        var x = args["x"]?.GetValue<int>() ?? 0;
        var y = args["y"]?.GetValue<int>() ?? 0;
        var colorName = (args["color"]?.GetValue<string>() ?? "white").ToLowerInvariant();

        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady) return "No game is loaded.";

            var location = Game1.player.currentLocation;
            var tx = hasX ? x : Game1.player.TilePoint.X;
            var ty = hasY ? y : Game1.player.TilePoint.Y;
            var pixelPos = new Vector2(tx * Game1.tileSize, ty * Game1.tileSize);

            switch (effect)
            {
                case "flash":
                    Game1.flashAlpha = 1f;
                    return "Screen flash triggered.";

                case "glow":
                    var color = colorName switch
                    {
                        "red"    => Color.Red,
                        "green"  => Color.Green,
                        "blue"   => Color.Blue,
                        "yellow" => Color.Yellow,
                        "purple" => Color.Purple,
                        _        => Color.White,
                    };
                    Game1.screenGlowOnce(color * 0.8f, hold: false);
                    return $"Screen glow triggered ({colorName}).";

                case "rainbow":
                    Utility.addRainbowStarExplosion(location, pixelPos, 20);
                    return $"Rainbow star explosion at ({tx}, {ty}).";

                case "sparkle":
                    Utility.addSprinklesToLocation(location, tx, ty, 3, 3, 2000, 100, Color.White);
                    return $"Sparkles at ({tx}, {ty}).";

                case "lightning":
                    Utility.drawLightningBolt(pixelPos, location);
                    return $"Lightning bolt at ({tx}, {ty}).";

                default:
                    return $"Unknown effect '{effect}'. Valid: flash, glow, rainbow, sparkle, lightning.";
            }
        });
    }

    private static Task<string> PlaySound(JsonObject args)
    {
        var sound = (args["sound"]?.GetValue<string>() ?? "").Trim();
        var hasPitch = args["pitch"] is not null;
        var pitch = args["pitch"]?.GetValue<int>() ?? 0;
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady) return "No game is loaded.";
            if (string.IsNullOrWhiteSpace(sound)) return "Sound ID is required.";
            if (hasPitch)
                Game1.playSound(sound, pitch);
            else
                Game1.playSound(sound);
            return $"Played sound: {sound}.";
        });
    }

    private static Task<string> PlayMusic(JsonObject args)
    {
        var track = (args["track"]?.GetValue<string>() ?? "").Trim();
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady) return "No game is loaded.";
            if (string.IsNullOrWhiteSpace(track)) return "Track name is required. Use 'none' to stop music.";
            Game1.changeMusicTrack(track);
            return track == "none" ? "Music stopped." : $"Now playing: {track}.";
        });
    }

    private static Task<string> AdvanceDay(JsonObject args)
    {
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady) return "No game is loaded.";
            Game1.player.isInBed.Value = true;
            Game1.player.sleptInTemporaryBed.Value = true;
            Game1.currentLocation.answerDialogueAction("Sleep_Yes", null);
            return "Advancing to next day...";
        });
    }

    private static Task<string> WarpToMineFloor(JsonObject args)
    {
        var floor = args["floor"]?.GetValue<int>() ?? 1;
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady) return "No game is loaded.";
            DebugCommands.TryHandle(new[] { "MineLevel", floor.ToString() });
            return $"Warping to mine floor {floor}.";
        });
    }

    private static Task<string> CompleteCommunityCenterCmd(JsonObject args)
    {
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady) return "No game is loaded.";
            DebugCommands.TryHandle(new[] { "CompleteCc" });
            return "Community Center marked as complete. All rooms restored.";
        });
    }

    private static Task<string> GrowCrops(JsonObject args)
    {
        var days = args["days"]?.GetValue<int>() ?? 1;
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady) return "No game is loaded.";
            DebugCommands.TryHandle(new[] { "GrowCrops", days.ToString() });
            return $"Grew all crops {days} day(s) in current location.";
        });
    }

    private static Task<string> BefriendAnimals(JsonObject args)
    {
        var friendship = Math.Clamp(args["friendship"]?.GetValue<int>() ?? 1000, 0, 1000);
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady) return "No game is loaded.";
            DebugCommands.TryHandle(new[] { "BefriendAnimals", friendship.ToString() });
            return $"Set animal friendship to {friendship} in current location.";
        });
    }

    private static Task<string> PauseTime(JsonObject args)
    {
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady) return "No game is loaded.";
            Game1.isTimePaused = !Game1.isTimePaused;
            return Game1.isTimePaused ? "Time paused." : "Time resumed.";
        });
    }

}
