using StardewModdingAPI;
using StardewValley;
using StardewValley.Monsters;
using System.Text.Json.Nodes;
using Microsoft.Xna.Framework;
using static StardewMCP.Tools.ToolRegistry;

namespace StardewMCP.Tools;

public static class MonsterTools
{
    private static readonly Dictionary<string, Func<Vector2, GameLocation, Monster>> Factories =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["slime"] = (pos, _) => new GreenSlime(pos, 1),
        ["green slime"] = (pos, _) => new GreenSlime(pos, 1),
        ["blue slime"] = (pos, _) => new GreenSlime(pos, 40),
        ["red slime"] = (pos, _) => new GreenSlime(pos, 80),
        ["purple slime"] = (pos, _) => new GreenSlime(pos, 121),
        ["big slime"] = (pos, _) => new BigSlime(pos, 1),
        ["bat"] = (pos, _) => new Bat(pos, 1),
        ["frost bat"] = (pos, _) => new Bat(pos, 40),
        ["lava bat"] = (pos, _) => new Bat(pos, 80),
        ["iridium bat"] = (pos, _) => new Bat(pos, 171),
        ["bug"] = (pos, _) => new Bug(pos, 0, "Bug"),
        ["skeleton"] = (pos, _) => new Skeleton(pos, false),
        ["mage skeleton"] = (pos, _) => new Skeleton(pos, true),
        ["ghost"] = (pos, _) => new Ghost(pos),
        ["carbon ghost"] = (pos, _) => new Ghost(pos, "Carbon Ghost"),
        ["putrid ghost"] = (pos, _) => new Ghost(pos, "Putrid Ghost"),
        ["duggy"] = (pos, _) => new Duggy(pos),
        ["dust sprite"] = (pos, _) => new DustSpirit(pos, false),
        ["fly"] = (pos, _) => new Fly(pos, false),
        ["mutant fly"] = (pos, _) => new Fly(pos, true),
        ["grub"] = (pos, _) => new Grub(pos, false),
        ["mutant grub"] = (pos, _) => new Grub(pos, true),
        ["mummy"] = (pos, _) => new Mummy(pos),
        ["metal head"] = (pos, _) => new MetalHead(pos, 1),
        ["serpent"] = (pos, _) => new Serpent(pos),
        ["shadow brute"] = (pos, _) => new ShadowBrute(pos),
        ["shadow shaman"] = (pos, _) => new ShadowShaman(pos),
        ["squid kid"] = (pos, _) => new SquidKid(pos),
        ["leaper"] = (pos, _) => new Leaper(pos),
        ["pepper rex"] = (pos, _) => new DinoMonster(pos),
        ["dinosaur"] = (pos, _) => new DinoMonster(pos),
        ["dino"] = (pos, _) => new DinoMonster(pos),
        ["angry roger"] = (pos, _) => new AngryRoger(pos),
        ["blue squid"] = (pos, _) => new BlueSquid(pos),
        ["dwarvish sentry"] = (pos, _) => new DwarvishSentry(pos),
        ["hot head"] = (pos, _) => new HotHead(pos),
        ["lava lurk"] = (pos, _) => new LavaLurk(pos),
        ["rock crab"] = (pos, _) => new RockCrab(pos),
        ["rock golem"] = (pos, _) => new RockGolem(pos),
        ["shadow girl"] = (pos, _) => new ShadowGirl(pos),
        ["shadow guy"] = (pos, _) => new ShadowGuy(pos),
        ["shooter"] = (pos, _) => new Shooter(pos),
        ["spiker"] = (pos, _) => new Spiker(pos, 0),
    };

    public static void Register(ToolRegistry registry)
    {
        registry.Add(
            Tool("kill_all_monsters",
                "Remove all monsters from the player's current location.",
                Props()),
            KillAllMonsters
        );

        registry.Add(
            Tool("spawn_monster",
                "Spawn one or more monsters at a tile position in the player's current location. " +
                "Available types: slime, green slime, blue slime, red slime, purple slime, big slime, " +
                "bat, frost bat, lava bat, iridium bat, bug, skeleton, mage skeleton, ghost, carbon ghost, " +
                "putrid ghost, duggy, dust sprite, fly, mutant fly, grub, mutant grub, mummy, metal head, " +
                "serpent, shadow brute, shadow girl, shadow guy, shadow shaman, squid kid, leaper, " +
                "pepper rex (dino), angry roger, blue squid, dwarvish sentry, hot head, lava lurk, " +
                "rock crab, rock golem, shooter, spiker.",
                Props(
                    Str("monster", "Monster type, e.g. 'slime', 'skeleton', 'bat'"),
                    Int("x", "Tile X to spawn at (default: 3 tiles east of player)"),
                    Int("y", "Tile Y to spawn at (default: player's tile Y)"),
                    Int("count", "How many to spawn (default: 1, max: 20)")
                )),
            SpawnMonster
        );
    }

    private static Task<string> KillAllMonsters(JsonObject args)
    {
        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady)
                return "No game is loaded.";

            var location = Game1.player.currentLocation;
            var monsters = location.characters.Where(c => c.IsMonster).ToList();
            foreach (var m in monsters)
                location.characters.Remove(m);

            return monsters.Count == 0
                ? "No monsters in this location."
                : $"Removed {monsters.Count} monster{(monsters.Count == 1 ? "" : "s")} from {location.Name}.";
        });
    }

    private static Task<string> SpawnMonster(JsonObject args)
    {
        var monsterName = (args["monster"]?.GetValue<string>() ?? "").Trim();
        var hasX = args["x"] is not null;
        var hasY = args["y"] is not null;
        var x = args["x"]?.GetValue<int>() ?? 0;
        var y = args["y"]?.GetValue<int>() ?? 0;
        var count = Math.Clamp(args["count"]?.GetValue<int>() ?? 1, 1, 20);

        return ModEntry.OnGameThread(() =>
        {
            if (!Context.IsWorldReady)
                return "No game is loaded.";

            if (!Factories.TryGetValue(monsterName, out var factory))
            {
                var match = Factories.Keys.FirstOrDefault(k =>
                    k.Contains(monsterName, StringComparison.OrdinalIgnoreCase));
                if (match is null)
                    return $"Unknown monster '{monsterName}'. Available: {string.Join(", ", Factories.Keys)}";
                factory = Factories[match];
                monsterName = match;
            }

            var location = Game1.player.currentLocation;
            var tileX = hasX ? x : Game1.player.TilePoint.X + 3;
            var tileY = hasY ? y : Game1.player.TilePoint.Y;

            for (int i = 0; i < count; i++)
            {
                // Spread monsters in a rough grid so they don't stack
                int offX = i % 4;
                int offY = i / 4;
                var pixelPos = new Vector2((tileX + offX) * Game1.tileSize, (tileY + offY) * Game1.tileSize);
                location.characters.Add(factory(pixelPos, location));
            }

            var at = $"({tileX}, {tileY}) in {location.Name}";
            return count == 1
                ? $"Spawned {monsterName} at {at}."
                : $"Spawned {count}x {monsterName} at {at}.";
        });
    }

}
