using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using BereftSouls.Quests.Steps;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using QuestBooks.Quests;

using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace BereftSouls.Quests;

/// <summary>
///     Picks the picture drawn on each quest in the book: the step's item, or
///     the boss's map head icon.
/// </summary>
internal static class QuestIcons
{
    private readonly record struct Icon(bool IsItem, int Type);

    // Boss quest key -> NPCs to try for a head icon, first match wins.
    private static readonly Dictionary<string, Func<int[]>> boss_npcs = new()
    {
        // Vanilla bosses (quests provided by QuestBooks).
        ["KingSlimeDefeated"]      = () => [NPCID.KingSlime],
        ["EyeOfCthulhuDefeated"]   = () => [NPCID.EyeofCthulhu],
        ["EvilBossDefeated"]       = () => WorldGen.crimson ? new int[] { NPCID.BrainofCthulhu } : new int[] { NPCID.EaterofWorldsHead },
        ["QueenBeeDefeated"]       = () => [NPCID.QueenBee],
        ["SkeletronDefeated"]      = () => [NPCID.SkeletronHead],
        ["DeerclopsDefeated"]      = () => [NPCID.Deerclops],
        ["WallOfFleshDefeated"]    = () => [NPCID.WallofFlesh],
        ["QueenSlimeDefeated"]     = () => [NPCID.QueenSlimeBoss],
        ["TheTwinsDefeated"]       = () => [NPCID.Retinazer, NPCID.Spazmatism],
        ["TheDestroyerDefeated"]   = () => [NPCID.TheDestroyer],
        ["SkeletronPrimeDefeated"] = () => [NPCID.SkeletronPrime],
        ["PlanteraDefeated"]       = () => [NPCID.Plantera],
        ["GolemDefeated"]          = () => [NPCID.GolemHead, NPCID.Golem],
        ["DukeFishronDefeated"]    = () => [NPCID.DukeFishron],
        ["EmpressOfLightDefeated"] = () => [NPCID.HallowBoss],
        ["LunaticCultistDefeated"] = () => [NPCID.CultistBoss],
        ["MoonLordDefeated"]       = () => [NPCID.MoonLordHead, NPCID.MoonLordCore],

        // Calamity and SotS bosses.
        ["DesertScourgeDefeated"]      = () => Modded("CalamityMod/DesertScourgeHead"),
        ["GlowmothDefeated"]           = () => Modded("SOTS/Glowmoth"),
        ["CrabulonDefeated"]           = () => Modded("CalamityMod/Crabulon"),
        ["HiveMindDefeated"]           = () => Modded("CalamityMod/HiveMind"),
        ["PerforatorsDefeated"]        = () => Modded("CalamityMod/PerforatorHive"),
        ["PutridPinkyDefeated"]        = () => Modded("SOTS/PutridPinkyPhase2", "SOTS/PutridPinky1"),
        ["PharaohsCurseDefeated"]      = () => Modded("SOTS/PharaohsCurse"),
        ["SlimeGodDefeated"]           = () => Modded("CalamityMod/SlimeGodCore"),
        ["ExcavatorDefeated"]          = () => Modded("SOTS/Excavator"),
        ["AdvisorDefeated"]            = () => Modded("SOTS/TheAdvisorHead"),
        ["CryogenDefeated"]            = () => Modded("CalamityMod/Cryogen"),
        ["AquaticScourgeDefeated"]     = () => Modded("CalamityMod/AquaticScourgeHead"),
        ["BrimstoneElementalDefeated"] = () => Modded("CalamityMod/BrimstoneElemental"),
        ["PolarisDefeated"]            = () => Modded("SOTS/NewPolaris"),
        ["CalamitasCloneDefeated"]     = () => Modded("CalamityMod/CalamitasClone"),
        ["LeviathanDefeated"]          = () => Modded("CalamityMod/Anahita", "CalamityMod/Leviathan"),
        ["AstrumAureusDefeated"]       = () => Modded("CalamityMod/AstrumAureus"),
        ["PlaguebringerDefeated"]      = () => Modded("CalamityMod/PlaguebringerGoliath"),
        ["RavagerDefeated"]            = () => Modded("CalamityMod/RavagerBody"),
        ["LuxDefeated"]                = () => Modded("SOTS/Lux"),
        ["AstrumDeusDefeated"]         = () => Modded("CalamityMod/AstrumDeusHead"),
        ["SubspaceSerpentDefeated"]    = () => Modded("SOTS/SubspaceSerpentHead"),
        ["ProfanedGuardiansDefeated"]  = () => Modded("CalamityMod/ProfanedGuardianCommander"),
        ["DragonfollyDefeated"]        = () => Modded("CalamityMod/Dragonfolly"),
        ["ProvidenceDefeated"]         = () => Modded("CalamityMod/Providence"),
        ["CeaselessVoidDefeated"]      = () => Modded("CalamityMod/CeaselessVoid"),
        ["StormWeaverDefeated"]        = () => Modded("CalamityMod/StormWeaverHead"),
        ["SignusDefeated"]             = () => Modded("CalamityMod/Signus"),
        ["PolterghastDefeated"]        = () => Modded("CalamityMod/Polterghast"),
        ["OldDukeDefeated"]            = () => Modded("CalamityMod/OldDuke"),
        ["DevourerOfGodsDefeated"]     = () => Modded("CalamityMod/DevourerofGodsHead"),
        ["YharonDefeated"]             = () => Modded("CalamityMod/Yharon"),
        ["ExoMechsDefeated"]           = () => Modded("CalamityMod/AresBody", "CalamityMod/Apollo", "CalamityMod/ThanatosHead", "CalamityMod/Draedon"),
        ["SupremeCalamitasDefeated"]   = () => Modded("CalamityMod/SupremeCalamitas"),
    };

    private static readonly Dictionary<string, Icon?> cache = [];

    public static void Clear() => cache.Clear();

    public static bool TryGet(Quest quest, [NotNullWhen(true)] out Texture2D? texture, out Rectangle frame)
    {
        texture = null;
        frame   = default;

        if (!cache.TryGetValue(quest.Key, out var icon))
        {
            cache[quest.Key] = icon = Find(quest);
        }

        if (icon is not { } found)
        {
            return false;
        }

        if (found.IsItem)
        {
            Main.instance.LoadItem(found.Type);
            texture = TextureAssets.Item[found.Type].Value;
            frame   = Main.itemAnimations[found.Type]?.GetFrame(texture) ?? texture.Frame();
        }
        else
        {
            texture = TextureAssets.NpcHeadBoss[found.Type].Value;
            frame   = texture.Frame();
        }

        return true;
    }

    private static Icon? Find(Quest quest)
    {
        if (quest is StepQuest { IconItem: > 0 } step)
        {
            return new Icon(true, step.IconItem);
        }

        if (!boss_npcs.TryGetValue(quest.Key, out var npcs))
        {
            return null;
        }

        foreach (var npc in npcs())
        {
            var head = npc > 0 && npc < NPCID.Sets.BossHeadTextures.Length ? NPCID.Sets.BossHeadTextures[npc] : -1;
            if (head >= 0 && head < TextureAssets.NpcHeadBoss.Length)
            {
                return new Icon(false, head);
            }
        }

        return null;
    }

    private static int[] Modded(params string[] names)
    {
        var types = new List<int>();
        foreach (var name in names)
        {
            if (ModContent.TryFind<ModNPC>(name, out var npc))
            {
                types.Add(npc.Type);
            }
        }

        return [.. types];
    }
}

/// <summary>Resets icons per world, since the evil boss depends on the world's evil.</summary>
[JetBrains.Annotations.UsedImplicitly(JetBrains.Annotations.ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
internal sealed class QuestIconCache : ModSystem
{
    public override void OnWorldLoad() => QuestIcons.Clear();

    public override void Unload() => QuestIcons.Clear();
}
