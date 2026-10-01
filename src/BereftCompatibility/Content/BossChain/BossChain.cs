using System.Collections.Generic;
using System.Linq;

using CalamityMod;
using CalamityMod.NPCs.AquaticScourge;
using CalamityMod.NPCs.AstrumAureus;
using CalamityMod.NPCs.AstrumDeus;
using CalamityMod.NPCs.BrimstoneElemental;
using CalamityMod.NPCs.Bumblebirb;
using CalamityMod.NPCs.CalClone;
using CalamityMod.NPCs.CeaselessVoid;
using CalamityMod.NPCs.Crabulon;
using CalamityMod.NPCs.Cryogen;
using CalamityMod.NPCs.DesertScourge;
using CalamityMod.NPCs.DevourerofGods;
using CalamityMod.NPCs.ExoMechs;
using CalamityMod.NPCs.HiveMind;
using CalamityMod.NPCs.Leviathan;
using CalamityMod.NPCs.OldDuke;
using CalamityMod.NPCs.Perforator;
using CalamityMod.NPCs.PlaguebringerGoliath;
using CalamityMod.NPCs.Polterghast;
using CalamityMod.NPCs.ProfanedGuardians;
using CalamityMod.NPCs.Providence;
using CalamityMod.NPCs.Ravager;
using CalamityMod.NPCs.Signus;
using CalamityMod.NPCs.SlimeGod;
using CalamityMod.NPCs.StormWeaver;
using CalamityMod.NPCs.SupremeCalamitas;
using CalamityMod.NPCs.Yharon;

using Microsoft.Xna.Framework;

using SOTS;
using SOTS.NPCs.Boss;
using SOTS.NPCs.Boss.Advisor;
using SOTS.NPCs.Boss.Curse;
using SOTS.NPCs.Boss.Excavator;
using SOTS.NPCs.Boss.Glowmoth;
using SOTS.NPCs.Boss.Lux;
using SOTS.NPCs.Boss.Polaris.NewPolaris;

using Terraria;
using Terraria.ID;

using static Terraria.ModLoader.ModContent;

namespace BereftCompatibility.Content.BossChain;

/// <summary>
///     The required boss order.  Every boss here drops a Sigil the next one
///     needs, and none can be fought for the first time before the bosses
///     listed in its <see cref="ChainLink.Needs"/>.
/// </summary>
/// <remarks>
///     Mirrors tools/quest_steps.json and the BereftSouls progression book.
///     Once a boss has been beaten its gates lift, so rematches (and Fargo's
///     Mutant summons) work normally.
/// </remarks>
internal static class BossChain
{
    public static readonly ChainLink[] Links =
    [
        // Pre-Hardmode.
        Link("KingSlime", "King Slime", new(70, 130, 255), () => NPC.downedSlimeKing,
             V(NPCID.KingSlime), [], Gate.Recipe, 1.0002f, 0),
        Link("Glowmoth", "Glowmoth", new(90, 200, 255), () => SOTSWorld.downedGlowmoth,
             M<Glowmoth>(), ["KingSlime"], Gate.Recipe, 1.8f, 0),
        Link("DesertScourge", "Desert Scourge", new(220, 180, 110), () => DownedBossSystem.downedDesertScourge,
             M<DesertScourgeHead>(), ["Glowmoth"], Gate.Recipe, 1.9f, 0),
        Link("EyeOfCthulhu", "Eye of Cthulhu", new(235, 60, 60), () => NPC.downedBoss1,
             V(NPCID.EyeofCthulhu), ["DesertScourge"], Gate.Recipe, 2.0002f, 0, dropCount: 2),
        Link("Crabulon", "Crabulon", new(80, 140, 255), () => DownedBossSystem.downedCrabulon,
             M<Crabulon>(), ["EyeOfCthulhu"], Gate.Recipe, 2.5002f, 0),
        new("WorldEvil", "World Evil", new(150, 80, 200), () => NPC.downedBoss2,
            () => [NPCID.EaterofWorldsHead, NPCID.BrainofCthulhu],
            () => [NPCID.EaterofWorldsHead, NPCID.EaterofWorldsBody, NPCID.EaterofWorldsTail, NPCID.BrainofCthulhu],
            ["Crabulon"], false, Gate.Recipe, 3.0002f, 0, 2, DropRule.LastEaterSegment),
        Link("HiveMind", "Hive Mind", new(120, 60, 160), () => DownedBossSystem.downedHiveMind,
             M<HiveMind>(), ["WorldEvil"], Gate.Recipe, 3.5002f, 0),
        Link("Perforator", "Perforator", new(200, 40, 60), () => DownedBossSystem.downedPerforator,
             M<PerforatorHive>(), ["WorldEvil"], Gate.Recipe, 3.5003f, 0),
        new("QueenBee", "Queen Bee", new(250, 190, 40), () => NPC.downedQueenBee,
            V(NPCID.QueenBee), V(NPCID.QueenBee), ["HiveMind", "Perforator"], true, Gate.Recipe, 4.0002f, 0),
        Link("PutridPinky", "Putrid Pinky", new(255, 120, 190), () => SOTSWorld.downedPinky,
             M<PutridPinky1>(), ["QueenBee"], Gate.Recipe, 4.25f, 0, loot: M<PutridPinkyPhase2>()),
        Link("PharaohsCurse", "Pharaoh's Curse", new(190, 150, 70), () => SOTSWorld.downedCurse,
             M<PharaohsCurse>(), ["PutridPinky"], Gate.Carry, 4.5f, 0),
        Link("Skeletron", "Skeletron", new(230, 225, 200), () => NPC.downedBoss3,
             V(NPCID.SkeletronHead), ["PharaohsCurse"], Gate.Carry, 5.0002f, 0, dropCount: 2),
        Link("Deerclops", "Deerclops", new(160, 200, 230), () => NPC.downedDeerclops,
             V(NPCID.Deerclops), ["Skeletron"], Gate.Recipe, 6.0002f, 0),
        Link("SlimeGod", "Slime God", new(170, 90, 220), () => DownedBossSystem.downedSlimeGod,
             M<SlimeGodCore>(), ["Deerclops"], Gate.Recipe, 6.5002f, 0),
        Link("Excavator", "Excavator", new(180, 120, 70), () => SOTSWorld.downedExcavator,
             M<Excavator>(), ["SlimeGod"], Gate.Carry, 6.8f, 0),
        Link("Advisor", "Advisor", new(120, 110, 255), () => SOTSWorld.downedAdvisor,
             M<TheAdvisorHead>(), ["Excavator"], Gate.Guarded, 6.9f, 0),
        // Summoned with the Fleshbound Effigy (see ChainItems).
        Link("WallOfFlesh", "Wall of Flesh", new(190, 60, 90), () => Main.hardMode,
             V(NPCID.WallofFlesh), ["EyeOfCthulhu", "WorldEvil", "Skeletron", "Advisor"], Gate.Recipe, 7.0002f, 0),

        // Early Hardmode.
        Link("QueenSlime", "Queen Slime", new(255, 130, 220), () => NPC.downedQueenSlime,
             V(NPCID.QueenSlimeBoss), ["WallOfFlesh"], Gate.Carry, 8.0002f, 1),
        Link("Cryogen", "Cryogen", new(130, 220, 255), () => DownedBossSystem.downedCryogen,
             M<Cryogen>(), ["QueenSlime"], Gate.Recipe, 8.5002f, 1),
        Link("AquaticScourge", "Aquatic Scourge", new(150, 180, 60), () => DownedBossSystem.downedAquaticScourge,
             M<AquaticScourgeHead>(), ["Cryogen"], Gate.Recipe, 8.6f, 1),
        Link("BrimstoneElemental", "Brimstone Elemental", new(255, 90, 60), () => DownedBossSystem.downedBrimstoneElemental,
             M<BrimstoneElemental>(), ["AquaticScourge"], Gate.Recipe, 8.7f, 1),
        Link("Destroyer", "Destroyer", new(230, 60, 40), () => NPC.downedMechBoss1,
             V(NPCID.TheDestroyer), ["BrimstoneElemental"], Gate.Recipe, 8.8f, 1),
        new("Twins", "Twins", new(90, 210, 90), () => NPC.downedMechBoss2,
            V(NPCID.Retinazer, NPCID.Spazmatism), V(NPCID.Retinazer, NPCID.Spazmatism),
            ["Destroyer"], false, Gate.Recipe, 9.0002f, 1, 1, DropRule.LastTwin),
        Link("SkeletronPrime", "Skeletron Prime", new(220, 220, 230), () => NPC.downedMechBoss3,
             V(NPCID.SkeletronPrime), ["Twins"], Gate.Recipe, 11.0002f, 1),
        Link("Polaris", "Polaris", new(90, 170, 255), () => SOTSWorld.downedAmalgamation,
             M<NewPolaris>(), ["SkeletronPrime"], Gate.Recipe, 11.01f, 1),
        Link("CalamitasClone", "Calamitas Clone", new(200, 30, 30), () => DownedBossSystem.downedCalamitasClone,
             M<CalamitasClone>(), ["Polaris"], Gate.Recipe, 11.5002f, 1),
        Link("Plantera", "Plantera", new(240, 90, 170), () => NPC.downedPlantBoss,
             V(NPCID.Plantera), ["Destroyer", "Twins", "SkeletronPrime", "CalamitasClone"], Gate.Carry, 12.0002f, 1),

        // Post-Plantera.
        new("Leviathan", "Leviathan", new(40, 140, 200), () => DownedBossSystem.downedLeviathan,
            () => [NPCType<Anahita>()], () => [NPCType<Anahita>(), NPCType<Leviathan>()],
            ["Plantera"], false, Gate.Recipe, 12.5002f, 2, 1, DropRule.LastLeviathan),
        Link("AstrumAureus", "Astrum Aureus", new(255, 150, 80), () => DownedBossSystem.downedAstrumAureus,
             M<AstrumAureus>(), ["Leviathan"], Gate.Recipe, 12.7502f, 2),
        Link("Golem", "Golem", new(220, 140, 40), () => NPC.downedGolemBoss,
             V(NPCID.Golem), ["AstrumAureus"], Gate.Carry, 13.0002f, 2),
        Link("Plaguebringer", "Plaguebringer", new(120, 200, 40), () => DownedBossSystem.downedPlaguebringer,
             M<PlaguebringerGoliath>(), ["Golem"], Gate.Recipe, 13.5002f, 2),
        Link("DukeFishron", "Duke Fishron", new(90, 200, 170), () => NPC.downedFishron,
             V(NPCID.DukeFishron), ["Plaguebringer"], Gate.Bait, 14.0002f, 2),
        Link("EmpressOfLight", "Empress of Light", new(255, 170, 255), () => NPC.downedEmpressOfLight,
             V(NPCID.HallowBoss), ["DukeFishron"], Gate.Carry, 15.0002f, 2),
        Link("Ravager", "Ravager", new(170, 80, 60), () => DownedBossSystem.downedRavager,
             M<RavagerBody>(), ["EmpressOfLight"], Gate.Recipe, 15.5f, 2),
        Link("Lux", "Lux", new(230, 120, 255), () => SOTSWorld.downedLux,
             M<Lux>(), ["Ravager"], Gate.Carry, 16.5f, 2),
        Link("LunaticCultist", "Lunatic Cultist", new(80, 120, 255), () => NPC.downedAncientCultist,
             V(NPCID.CultistBoss), ["Lux"], Gate.Carry, 17.0002f, 2),
        new("AstrumDeus", "Astrum Deus", new(90, 200, 240), () => DownedBossSystem.downedAstrumDeus,
            M<AstrumDeusHead>(), M<AstrumDeusHead>(),
            ["LunaticCultist"], false, Gate.Carry, 17.5002f, 2, 1, DropRule.LastAstrumDeus),
        Link("SubspaceSerpent", "Subspace Serpent", new(150, 40, 200), () => SOTSWorld.downedSubspace,
             M<SubspaceSerpentHead>(), ["AstrumDeus"], Gate.Recipe, 17.9f, 2),
        Link("MoonLord", "Moon Lord", new(110, 220, 200), () => NPC.downedMoonlord,
             V(NPCID.MoonLordCore), ["SubspaceSerpent"], Gate.Recipe, 18.0002f, 2),

        // Post-Moon Lord.
        Link("ProfanedGuardians", "Profaned Guardian", new(255, 200, 90), () => DownedBossSystem.downedGuardians,
             M<ProfanedGuardianCommander>(), ["MoonLord"], Gate.Recipe, 18.0061f, 3),
        Link("Dragonfolly", "Dragonfolly", new(255, 140, 60), () => DownedBossSystem.downedDragonfolly,
             M<Dragonfolly>(), ["ProfanedGuardians"], Gate.Recipe, 18.0071f, 3),
        Link("Providence", "Providence", new(255, 230, 140), () => DownedBossSystem.downedProvidence,
             M<Providence>(), ["Dragonfolly"], Gate.Carry, 18.0081f, 3),
        Link("StormWeaver", "Storm Weaver", new(140, 160, 255), () => DownedBossSystem.downedStormWeaver,
             M<StormWeaverHead>(), ["Providence"], Gate.Recipe, 18.0091f, 3),
        Link("CeaselessVoid", "Ceaseless Void", new(90, 80, 130), () => DownedBossSystem.downedCeaselessVoid,
             M<CeaselessVoid>(), ["Providence"], Gate.Recipe, 18.0092f, 3),
        Link("Signus", "Signus", new(180, 90, 220), () => DownedBossSystem.downedSignus,
             M<Signus>(), ["Providence"], Gate.Recipe, 18.0093f, 3),
        Link("Polterghast", "Polterghast", new(110, 240, 255), () => DownedBossSystem.downedPolterghast,
             M<Polterghast>(), ["StormWeaver", "CeaselessVoid", "Signus"], Gate.Recipe, 18.0094f, 3),
        Link("OldDuke", "Old Duke", new(160, 190, 60), () => DownedBossSystem.downedBoomerDuke,
             M<OldDuke>(), ["Polterghast"], Gate.Bait, 18.0095f, 3),
        Link("DevourerOfGods", "Devourer of Gods", new(200, 80, 255), () => DownedBossSystem.downedDoG,
             M<DevourerofGodsHead>(), ["OldDuke"], Gate.Recipe, 18.0096f, 3),

        // Endgame.
        Link("Yharon", "Yharon", new(255, 160, 40), () => DownedBossSystem.downedYharon,
             M<Yharon>(), ["DevourerOfGods"], Gate.Recipe, 18.0097f, 4),
        Link("SupremeCalamitas", "Supreme Calamitas", new(255, 40, 40), () => DownedBossSystem.downedCalamitas,
             M<SupremeCalamitas>(), ["Yharon"], Gate.Carry, 18.013f, 4),
        // Draedon brings the Exo Mechs; the Cooling Cell recipe also needs the
        // Supreme Calamitas Sigil.
        new("ExoMechs", "Exo Mechs", Color.White, () => DownedBossSystem.downedExoMechs,
            M<Draedon>(), () => [], ["SupremeCalamitas"], false, Gate.Recipe, 0f, 4, HasSigil: false),
    ];

    public static readonly Dictionary<string, ChainLink> ById = Links.ToDictionary(l => l.Id);

    public static IEnumerable<ChainLink> WithSigils => Links.Where(l => l.HasSigil);

    /// <summary>The links whose bosses need this one's Sigil.</summary>
    public static IEnumerable<ChainLink> Unlocks(ChainLink link) => Links.Where(l => l.Needs.Contains(link.Id));

    public static IEnumerable<ChainLink> NeedsOf(ChainLink link) => link.Needs.Select(id => ById[id]);

    /// <summary>Whether the bosses before this one are down.</summary>
    public static bool Unlocked(ChainLink link)
    {
        var needs = NeedsOf(link);
        return link.NeedsAny ? needs.Any(n => n.Downed()) : needs.All(n => n.Downed());
    }

    private static ChainLink Link(
        string      id,
        string      bossName,
        Color       tint,
        System.Func<bool> downed,
        System.Func<int[]> types,
        string[]    needs,
        Gate        gate,
        float       shop,
        int         tier,
        int         dropCount = 1,
        System.Func<int[]>? loot = null
    ) => new(id, bossName, tint, downed, types, loot ?? types, needs, false, gate, shop, tier, dropCount);

    private static System.Func<int[]> V(params int[] types) => () => types;

    private static System.Func<int[]> M<T>() where T : Terraria.ModLoader.ModNPC => () => [NPCType<T>()];
}
