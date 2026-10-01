using System.Collections.Generic;
using System.Linq;

using CalamityMod.Items.SummonItems;
using CalamityMod.NPCs.HiveMind;
using CalamityMod.NPCs.Perforator;
using CalamityMod.Projectiles.Boss;

using JetBrains.Annotations;

using Microsoft.Xna.Framework;

using SOTS.Items.Earth.Glowmoth;
using SOTS.NPCs.Constructs;

using Terraria;
using Terraria.Chat;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

using static Terraria.ModLoader.ModContent;

namespace BereftCompatibility.Content.BossChain;

/// <summary>Shared checks and messages for the boss chain's gates.</summary>
internal static class ChainRules
{
    /// <summary>How far from a waking boss a Sigil carrier can be.</summary>
    private const float carry_range = 5000f;

    private static readonly Color message_color = new(175, 75, 255);

    private static uint nextWarning;

    public static bool Carries(Player player, ChainLink link)
    {
        var sigils = BossChain.NeedsOf(link).Select(SigilLoader.Type);
        return link.NeedsAny ? sigils.Any(player.HasItem) : sigils.All(player.HasItem);
    }

    public static bool AnyoneCarries(Vector2 position, ChainLink link)
    {
        return Main.player.Any(
            p => p.active && !p.dead && p.Distance(position) < carry_range && Carries(p, link)
        );
    }

    /// <summary>The Sigils a boss needs, e.g. "a Twins Sigil and a Polaris Sigil".</summary>
    public static string SigilList(ChainLink link)
    {
        var names = BossChain.NeedsOf(link).Select(n => Language.GetTextValue("Mods.BereftCompatibility.BossChain.SigilName", n.BossName));
        var joiner = Language.GetTextValue(link.NeedsAny ? "Mods.BereftCompatibility.BossChain.Or" : "Mods.BereftCompatibility.BossChain.And");
        return string.Join(joiner, names);
    }

    public static string BossList(ChainLink link)
    {
        var joiner = Language.GetTextValue(link.NeedsAny ? "Mods.BereftCompatibility.BossChain.Or" : "Mods.BereftCompatibility.BossChain.And");
        return string.Join(joiner, BossChain.NeedsOf(link).Where(n => !n.Downed()).Select(n => n.BossName));
    }

    public static string Text(string key, params object[] args) =>
        Language.GetTextValue("Mods.BereftCompatibility.BossChain." + key, args);

    /// <summary>Tells everyone (from the server) or the local player.</summary>
    public static void Broadcast(string text)
    {
        if (Main.netMode == NetmodeID.Server)
        {
            ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(text), message_color);
        }
        else
        {
            Main.NewText(text, message_color);
        }
    }

    /// <summary>Tells the local player, at most every few seconds.</summary>
    public static void Warn(Player player, string text)
    {
        if (Main.netMode == NetmodeID.Server || player.whoAmI != Main.myPlayer || Main.GameUpdateCount < nextWarning)
        {
            return;
        }

        nextWarning = Main.GameUpdateCount + 180;
        Main.NewText(text, message_color);
    }

    /// <summary>
    ///     Why the local player can't start this boss yet, or null if they
    ///     can.  Used by summon items and other things a player interacts with.
    /// </summary>
    public static string? Blocked(ChainLink link, Player player)
    {
        if (link.Downed())
        {
            return null;
        }

        if (!BossChain.Unlocked(link))
        {
            return Text("Sealed", link.BossName, BossList(link));
        }

        return Carries(player, link) ? null : Text("NeedSigil", link.BossName, SigilList(link));
    }
}

/// <summary>
///     Seals chain bosses that try to spawn before their time: natural spawns,
///     tumours, Fargo's copies and anything else that bypasses the recipes.
///     Also keeps world-bound bosses dormant until someone brings the Sigil.
/// </summary>
[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
internal sealed class ChainGuards : GlobalNPC
{
    private static Dictionary<int, ChainLink>? spawn_links;
    private static Dictionary<int, ChainLink>? guard_links;

    /// <summary>NPC types whose spawn starts a chain boss's fight.</summary>
    private static Dictionary<int, ChainLink> SpawnLinks => spawn_links ??= BossChain.Links
                                                                               .Where(l => l.Gate != Gate.Guarded)
                                                                               .SelectMany(l => l.SpawnTypes().Select(t => (t, l)))
                                                                               .ToDictionary(p => p.t, p => p.l);

    /// <summary>Dormant NPCs that can't be hurt without the next boss's Sigils.</summary>
    private static Dictionary<int, ChainLink> GuardLinks => guard_links ??= new Dictionary<int, ChainLink>
    {
        // Killing these is what summons the boss.
        [NPCType<HiveTumor>()]      = BossChain.ById["HiveMind"],
        [NPCType<PerforatorCyst>()] = BossChain.ById["Perforator"],
        // The Advisor wakes once its four tethered Constructs are destroyed.
        [NPCType<OtherworldlyConstructHead2>()] = BossChain.ById["Advisor"],
        // Attacking the ritual starts the Lunatic Cultist.
        [NPCID.CultistDevote] = BossChain.ById["LunaticCultist"],
        // Moon Lord comes once all four pillars fall.
        [NPCID.LunarTowerSolar]    = BossChain.ById["MoonLord"],
        [NPCID.LunarTowerVortex]   = BossChain.ById["MoonLord"],
        [NPCID.LunarTowerNebula]   = BossChain.ById["MoonLord"],
        [NPCID.LunarTowerStardust] = BossChain.ById["MoonLord"],
    };

    public override void Unload()
    {
        base.Unload();

        spawn_links = null;
        guard_links = null;
    }

    public override void OnSpawn(NPC npc, IEntitySource source)
    {
        base.OnSpawn(npc, source);

        if (Main.netMode == NetmodeID.MultiplayerClient || !SpawnLinks.TryGetValue(npc.type, out var link) || link.Downed())
        {
            return;
        }

        // A fight already underway (a worm splitting, the second twin).
        var types = link.SpawnTypes();
        if (Main.npc.Any(other => other.active && other.whoAmI != npc.whoAmI && types.Contains(other.type)))
        {
            return;
        }

        if (!BossChain.Unlocked(link))
        {
            Seal(npc, ChainRules.Text("Sealed", link.BossName, ChainRules.BossList(link)));
            return;
        }

        if (link.Id == "WallOfFlesh" && !FleshboundEffigy.Summoning)
        {
            Seal(npc, ChainRules.Text("EffigyOnly"));
            return;
        }

        // Acid Rain's own summon would skip the bait.
        if (link.Id == "OldDuke" && source is EntitySource_Parent { Entity: Projectile projectile } && projectile.type == ProjectileType<OverlyDramaticDukeSummoner>())
        {
            Seal(npc, ChainRules.Text("NeedBloodworm"));
            return;
        }

        if (link.Gate == Gate.Carry && !ChainRules.AnyoneCarries(npc.Center, link))
        {
            Seal(npc, ChainRules.Text("NeedSigil", link.BossName, ChainRules.SigilList(link)));
        }
    }

    // Terraria asks every NPC in the world before checking whether a swing or
    // projectile actually reaches it, so only warn about ones being struck.
    private const float warn_range = 320f;

    public override bool? CanBeHitByItem(NPC npc, Player player, Item item) =>
        Guarded(npc, player, player.Distance(npc.Center) < warn_range) ? false : null;

    public override bool? CanBeHitByProjectile(NPC npc, Projectile projectile)
    {
        if (!projectile.friendly || projectile.owner is < 0 or >= Main.maxPlayers)
        {
            return null;
        }

        return Guarded(npc, Main.player[projectile.owner], projectile.Hitbox.Intersects(npc.Hitbox)) ? false : null;
    }

    public override bool? CanChat(NPC npc)
    {
        // The Old Man's night-time "Curse" option summons Skeletron, so he
        // won't talk at night to anyone who can't face it yet.
        if (npc.type != NPCID.OldMan || Main.dayTime || ChainRules.Blocked(BossChain.ById["Skeletron"], Main.LocalPlayer) is not { } reason)
        {
            return null;
        }

        ChainRules.Warn(Main.LocalPlayer, reason);
        return false;
    }

    private static bool Guarded(NPC npc, Player player, bool warn)
    {
        if (!GuardLinks.TryGetValue(npc.type, out var link) || link.Downed() || ChainRules.Carries(player, link))
        {
            return false;
        }

        if (warn)
        {
            ChainRules.Warn(player, ChainRules.Text("Dormant", ChainRules.SigilList(link)));
        }

        return true;
    }

    private static void Seal(NPC npc, string reason)
    {
        npc.active = false;
        npc.netUpdate = true;
        ChainRules.Broadcast(reason);
    }
}

/// <summary>
///     Found (not crafted) summon items, and Fargo's copies of them, only work
///     once the bosses before are down and the user carries their Sigils.
/// </summary>
[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
internal sealed class ChainSummonItems : GlobalItem
{
    private static Dictionary<int, ChainLink>? item_links;

    private static Dictionary<int, ChainLink> ItemLinks => item_links ??= Build();

    public override void Unload()
    {
        base.Unload();

        item_links = null;
    }

    public override bool CanUseItem(Item item, Player player)
    {
        if (!ItemLinks.TryGetValue(item.type, out var link) || link.Downed())
        {
            return base.CanUseItem(item, player);
        }

        // Fargo's Fleshy Doll would skip the Fleshbound Effigy.
        var reason = link.Id == "WallOfFlesh" ? ChainRules.Text("EffigyOnly") : ChainRules.Blocked(link, player);
        if (reason is null)
        {
            return base.CanUseItem(item, player);
        }

        ChainRules.Warn(player, reason);
        return false;
    }

    private static Dictionary<int, ChainLink> Build()
    {
        var links = new Dictionary<int, ChainLink>
        {
            [ItemID.QueenSlimeCrystal]  = BossChain.ById["QueenSlime"],
            [ItemType<ProfanedCore>()]  = BossChain.ById["Providence"],
        };

        (string Mod, string Item, string Boss)[] copies =
        [
            ("Fargowiltas", "FleshyDoll", "WallOfFlesh"),
            ("Fargowiltas", "SuspiciousSkull", "Skeletron"),
            ("Fargowiltas", "JellyCrystal", "QueenSlime"),
            ("Fargowiltas", "PlanterasFruit", "Plantera"),
            ("Fargowiltas", "LihzahrdPowerCell2", "Golem"),
            ("Fargowiltas", "TruffleWorm2", "DukeFishron"),
            ("Fargowiltas", "PrismaticPrimrose", "EmpressOfLight"),
            ("Fargowiltas", "CultistSummon", "LunaticCultist"),
            ("Fargowiltas", "CelestialSigil2", "MoonLord"),
            ("FargowiltasCrossmod", "DefiledCore", "Providence"),
            ("FargowiltasCrossmod", "BloodyWorm", "OldDuke"),
            ("FargowiltasCrossmod", "PortableCodebreaker", "ExoMechs"),
        ];

        foreach (var (mod, item, boss) in copies)
        {
            if (TryFind<ModItem>(mod, item, out var modItem))
            {
                links[modItem.Type] = BossChain.ById[boss];
            }
        }

        return links;
    }
}

/// <summary>Tiles that summon a chain boss when broken.</summary>
[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
internal sealed class ChainTiles : GlobalTile
{
    private static Dictionary<int, ChainLink>? tile_links;

    private static Dictionary<int, ChainLink> TileLinks => tile_links ??= new Dictionary<int, ChainLink>
    {
        [TileType<SilkCocoonTile>()] = BossChain.ById["Glowmoth"],
        [TileID.Larva]               = BossChain.ById["QueenBee"],
        [TileID.PlanteraBulb]        = BossChain.ById["Plantera"],
    };

    public override void Unload()
    {
        base.Unload();

        tile_links = null;
    }

    public override bool CanKillTile(int i, int j, int type, ref bool blockDamaged) => !Guarded(i, j, type);

    public override bool CanExplode(int i, int j, int type) => !Guarded(i, j, type);

    private static bool Guarded(int i, int j, int type)
    {
        if (WorldGen.generatingWorld || !TileLinks.TryGetValue(type, out var link) || link.Downed())
        {
            return false;
        }

        var player = Main.player[Player.FindClosest(new Vector2(i, j) * 16f, 16, 16)];
        if (!player.active || ChainRules.Carries(player, link))
        {
            return false;
        }

        ChainRules.Warn(player, ChainRules.Text("Dormant", ChainRules.SigilList(link)));
        return true;
    }
}
