using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Locations;
using StardewValley.TerrainFeatures;
using System.Text;
using System.Text.Json.Nodes;

namespace StardewMCP.Tools;

public static class FarmTools
{
    public static void Register(ToolRegistry registry)
    {
        registry.Add(
            Tool("get_farm_info",
                "Get a full overview of the farm: planted crops and their growth state, buildings, animals, and machines currently processing items.",
                Props()),
            GetFarmInfo,
            observeOnly: true
        );
    }

    private static Task<string> GetFarmInfo(JsonObject args)
    {
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady)
                return "No game is loaded.";

            var farm = Game1.getFarm();
            var sb = new StringBuilder();

            // ── Crops ────────────────────────────────────────────────────────
            var crops = new List<string>();
            var readyToHarvest = new List<string>();

            foreach (var (_, tf) in farm.terrainFeatures.Pairs)
            {
                if (tf is not HoeDirt { crop: not null } dirt)
                    continue;

                var crop = dirt.crop;
                var cropData = crop.GetData();
                if (cropData is null) continue;

                var harvestName = ItemRegistry.GetDataOrErrorItem("(O)" + cropData.HarvestItemId).DisplayName;

                if (crop.fullyGrown.Value || crop.currentPhase.Value >= crop.phaseDays.Count - 1)
                {
                    readyToHarvest.Add(harvestName);
                }
                else
                {
                    int daysGrown = crop.dayOfCurrentPhase.Value;
                    for (int i = 0; i < crop.currentPhase.Value; i++)
                        daysGrown += crop.phaseDays[i];

                    int totalDays = 0;
                    for (int i = 0; i < crop.phaseDays.Count - 1; i++)
                        totalDays += crop.phaseDays[i];

                    int daysLeft = Math.Max(0, totalDays - daysGrown);
                    var watered = dirt.state.Value == HoeDirt.watered ? "watered" : "dry";
                    crops.Add($"{harvestName} ({daysLeft} day{(daysLeft == 1 ? "" : "s")} left, {watered})");
                }
            }

            sb.AppendLine("## Crops");
            if (crops.Count == 0 && readyToHarvest.Count == 0)
            {
                sb.AppendLine("No crops planted.");
            }
            else
            {
                if (readyToHarvest.Count > 0)
                {
                    var grouped = readyToHarvest.GroupBy(n => n)
                        .Select(g => g.Count() > 1 ? $"{g.Key} x{g.Count()}" : g.Key);
                    sb.AppendLine($"Ready to harvest: {string.Join(", ", grouped)}");
                }
                if (crops.Count > 0)
                {
                    var grouped = crops.GroupBy(n => n)
                        .Select(g => g.Count() > 1 ? $"{g.Key} x{g.Count()}" : g.Key);
                    sb.AppendLine("Growing:");
                    foreach (var c in grouped)
                        sb.AppendLine($"  {c}");
                }
            }

            // ── Buildings ────────────────────────────────────────────────────
            sb.AppendLine("\n## Buildings");
            if (farm.buildings.Count == 0)
            {
                sb.AppendLine("No buildings.");
            }
            else
            {
                foreach (var building in farm.buildings)
                {
                    var type = building.buildingType.Value;
                    var info = new StringBuilder(type);

                    if (building.indoors.Value is AnimalHouse house)
                        info.Append($" ({house.animals.Count()}/{house.animalLimit.Value} animals)");

                    if (type == "Greenhouse")
                    {
                        var unlocked = Game1.player.mailReceived.Contains("ccPantry")
                                    || Utility.hasFinishedJojaRoute();
                        info.Append(unlocked ? " (unlocked)" : " (locked — complete Pantry bundles)");
                    }

                    sb.AppendLine($"  {info}");
                }
            }

            // ── Animals ──────────────────────────────────────────────────────
            sb.AppendLine("\n## Animals");
            var allAnimals = new List<FarmAnimal>();

            allAnimals.AddRange(farm.animals.Values);
            foreach (var building in farm.buildings)
            {
                if (building.indoors.Value is AnimalHouse house)
                    allAnimals.AddRange(house.animals.Values);
            }

            if (allAnimals.Count == 0)
            {
                sb.AppendLine("No animals.");
            }
            else
            {
                foreach (var animal in allAnimals)
                {
                    var friendship = animal.friendshipTowardFarmer.Value;
                    var hearts = friendship / 200; // 200 per heart, max 5 hearts (1000)
                    var mood = animal.happiness.Value switch
                    {
                        >= 200 => "happy",
                        >= 100 => "content",
                        _ => "unhappy"
                    };
                    sb.AppendLine($"  {animal.displayName} ({animal.type.Value}) — {hearts}♥ {mood}");
                }
            }

            // ── Machines ─────────────────────────────────────────────────────
            sb.AppendLine("\n## Machines");
            var machines = new List<string>();

            foreach (var (_, obj) in farm.objects.Pairs)
            {
                if (!obj.bigCraftable.Value) continue;

                if (obj.heldObject.Value is not null)
                {
                    var held = obj.heldObject.Value.DisplayName;
                    if (obj.readyForHarvest.Value)
                        machines.Add($"{obj.DisplayName}: {held} (ready)");
                    else
                        machines.Add($"{obj.DisplayName}: processing {held} ({obj.MinutesUntilReady} min left)");
                }
                else
                {
                    machines.Add($"{obj.DisplayName}: idle");
                }
            }

            if (machines.Count == 0)
                sb.AppendLine("No machines on farm.");
            else
                foreach (var m in machines)
                    sb.AppendLine($"  {m}");

            return sb.ToString().TrimEnd();
        });
    }

    // ── Schema builders ──────────────────────────────────────────────────────

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
}
