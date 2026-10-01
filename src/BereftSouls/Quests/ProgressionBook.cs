using System.Collections.Generic;
using System.Linq;

using CalamityMod;

using JetBrains.Annotations;

using Microsoft.Xna.Framework;

using QuestBooks;
using QuestBooks.QuestLog;
using QuestBooks.QuestLog.DefaultChapters;
using QuestBooks.QuestLog.DefaultElements;
using QuestBooks.QuestLog.DefaultQuestBooks;

using Terraria;
using Terraria.ModLoader;

namespace BereftSouls.Quests;

/// <summary>
///     Registers the boss progression book, combining vanilla, Calamity and
///     SotS bosses.
/// </summary>
/// <remarks>
///     Each chapter is one tier and unlocks once the previous tier's gate boss
///     is down.  Required bosses sit on the middle row; optional bosses branch
///     off above and below and feed nothing later on.  Positions are a rough
///     grid meant to be refined in the QuestBooks designer, after which this
///     can be swapped for an exported log.
/// </remarks>
[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
internal sealed class ProgressionBook : ModSystem
{
    public const string QUEST_LOG_KEY = "BereftSouls";

    private const float column_spacing = 160f;
    private const float row_spacing    = 130f;

    private const string texture_path = "QuestBooks/Assets/Textures/Quests/";

    private enum NodeKind
    {
        /// <summary>On the critical path to the final bosses.</summary>
        Required,

        /// <summary>Side content; nothing later depends on it.</summary>
        Optional,

        /// <summary>Unlocks the next chapter, or ends the game.</summary>
        Gate,
    }

    /// <param name="Key">Quest key (the quest class name).</param>
    /// <param name="Column">Grid column, left to right in progression order.</param>
    /// <param name="Row">Grid row; 0 is the main line.</param>
    /// <param name="After">
    ///     Keys of quests in the same chapter that must be completed to unlock
    ///     this one.
    /// </param>
    private readonly record struct Node(string Key, int Column, int Row, NodeKind Kind, params string[] After);

    // Vanilla boss quests come from QuestBooks itself.
    private static readonly Node[] pre_hardmode =
    [
        new("KingSlimeDefeated",     0, -1, NodeKind.Optional),
        new("EyeOfCthulhuDefeated",  0, 0,  NodeKind.Required),
        new("DesertScourgeDefeated", 1, -1, NodeKind.Optional, "EyeOfCthulhuDefeated"),
        new("GlowmothDefeated",      1, 1,  NodeKind.Optional, "EyeOfCthulhuDefeated"),
        new("CrabulonDefeated",      2, -1, NodeKind.Optional, "DesertScourgeDefeated"),
        new("EvilBossDefeated",      2, 0,  NodeKind.Required, "EyeOfCthulhuDefeated"),
        // Whichever of these comes first unlocks Aerialite.
        new("HiveMindDefeated",      3, -2, NodeKind.Optional, "EvilBossDefeated"),
        new("PerforatorsDefeated",   3, -1, NodeKind.Optional, "EvilBossDefeated"),
        new("PutridPinkyDefeated",   3, 1,  NodeKind.Optional, "EvilBossDefeated"),
        new("QueenBeeDefeated",      3, 2,  NodeKind.Optional, "EvilBossDefeated"),
        new("PharaohsCurseDefeated", 4, 2,  NodeKind.Optional, "QueenBeeDefeated"),
        new("SkeletronDefeated",     4, 0,  NodeKind.Required, "EvilBossDefeated"),
        new("SlimeGodDefeated",      5, -1, NodeKind.Optional, "SkeletronDefeated"),
        new("ExcavatorDefeated",     5, 1,  NodeKind.Optional, "SkeletronDefeated"),
        new("DeerclopsDefeated",     5, 2,  NodeKind.Optional, "SkeletronDefeated"),
        new("AdvisorDefeated",       6, 1,  NodeKind.Optional, "ExcavatorDefeated"),
        new("WallOfFleshDefeated",   7, 0,  NodeKind.Gate,     "SkeletronDefeated"),
    ];

    private static readonly Node[] early_hardmode =
    [
        new("WallOfFleshDefeated",        0, 0,  NodeKind.Gate),
        new("QueenSlimeDefeated",         1, -2, NodeKind.Optional, "WallOfFleshDefeated"),
        // Only source of Cryonic Ore, needed for Life Alloy.
        new("CryogenDefeated",            1, -1, NodeKind.Required, "WallOfFleshDefeated"),
        new("TheDestroyerDefeated",       1, 0,  NodeKind.Required, "WallOfFleshDefeated"),
        new("TheTwinsDefeated",           1, 1,  NodeKind.Required, "WallOfFleshDefeated"),
        new("SkeletronPrimeDefeated",     1, 2,  NodeKind.Required, "WallOfFleshDefeated"),
        new("AquaticScourgeDefeated",     2, -2, NodeKind.Optional, "CryogenDefeated"),
        new("BrimstoneElementalDefeated", 3, -2, NodeKind.Optional, "AquaticScourgeDefeated"),
        // Only source of Ashes of Calamity, needed to summon Supreme Calamitas.
        new("CalamitasCloneDefeated",     3, -1, NodeKind.Required, "CryogenDefeated"),
        // The Frosted Key needs souls from all three mechs.
        new("PolarisDefeated",            3, 3,  NodeKind.Optional, "TheDestroyerDefeated", "TheTwinsDefeated", "SkeletronPrimeDefeated"),
        new("PlanteraDefeated",           3, 1,  NodeKind.Gate,     "TheDestroyerDefeated", "TheTwinsDefeated", "SkeletronPrimeDefeated"),
    ];

    private static readonly Node[] post_plantera =
    [
        new("PlanteraDefeated",          0, 0,  NodeKind.Gate),
        new("AstrumAureusDefeated",      1, -2, NodeKind.Optional, "PlanteraDefeated"),
        new("LeviathanDefeated",         1, -1, NodeKind.Optional, "PlanteraDefeated"),
        new("GolemDefeated",             1, 0,  NodeKind.Required, "PlanteraDefeated"),
        new("DukeFishronDefeated",       1, 1,  NodeKind.Optional, "PlanteraDefeated"),
        new("EmpressOfLightDefeated",    1, 2,  NodeKind.Optional, "PlanteraDefeated"),
        // Plague enemies only spawn after Golem.
        new("PlaguebringerDefeated",     2, -1, NodeKind.Optional, "GolemDefeated"),
        new("RavagerDefeated",           2, 1,  NodeKind.Optional, "GolemDefeated"),
        new("LuxDefeated",               2, 2,  NodeKind.Optional, "GolemDefeated"),
        new("LunaticCultistDefeated",    3, 0,  NodeKind.Required, "GolemDefeated"),
        new("AstrumDeusDefeated",        4, -1, NodeKind.Optional, "LunaticCultistDefeated"),
        new("SubspaceSerpentDefeated",   4, 1,  NodeKind.Optional, "LunaticCultistDefeated"),
        new("MoonLordDefeated",          5, 0,  NodeKind.Gate,     "LunaticCultistDefeated"),
    ];

    private static readonly Node[] post_moon_lord =
    [
        new("MoonLordDefeated",          0, 0,  NodeKind.Gate),
        // Effulgent Feathers for the Yharon Egg; Wild Bumblebirbs also drop
        // them.
        new("DragonfollyDefeated",       1, -1, NodeKind.Required, "MoonLordDefeated"),
        // Only source of the Profaned Core.
        new("ProfanedGuardiansDefeated", 1, 0,  NodeKind.Required, "MoonLordDefeated"),
        new("PolterghastDefeated",       1, 2,  NodeKind.Optional, "MoonLordDefeated"),
        new("ProvidenceDefeated",        2, 0,  NodeKind.Required, "ProfanedGuardiansDefeated"),
        new("OldDukeDefeated",           2, 2,  NodeKind.Optional, "PolterghastDefeated"),
        // Each Sentinel drops a Cosmic Worm ingredient.
        new("CeaselessVoidDefeated",     3, -1, NodeKind.Required, "ProvidenceDefeated"),
        new("StormWeaverDefeated",       3, 0,  NodeKind.Required, "ProvidenceDefeated"),
        new("SignusDefeated",            3, 1,  NodeKind.Required, "ProvidenceDefeated"),
        new("DevourerOfGodsDefeated",    4, 0,  NodeKind.Gate,     "CeaselessVoidDefeated", "StormWeaverDefeated", "SignusDefeated"),
    ];

    private static readonly Node[] endgame =
    [
        new("DevourerOfGodsDefeated",   0, 0,  NodeKind.Gate),
        // Auric Bars for the Codebreaker's final cell and the SCal altar.
        new("YharonDefeated",           1, 0,  NodeKind.Required, "DevourerOfGodsDefeated"),
        new("ExoMechsDefeated",         2, -1, NodeKind.Gate,     "YharonDefeated"),
        new("SupremeCalamitasDefeated", 2, 1,  NodeKind.Gate,     "YharonDefeated"),
    ];

    public override void PostSetupContent()
    {
        var book = new TabBook
        {
            NameKey  = Mod.GetLocalizationKey("QuestBooks.Progression.Name"),
            Chapters =
            [
                BuildChapter<ScrollChapter>("PreHardmode", pre_hardmode),
                BuildChapter<HardmodeTierChapter>("EarlyHardmode", early_hardmode),
                BuildChapter<PostPlanteraChapter>("PostPlantera", post_plantera),
                BuildChapter<PostMoonLordChapter>("PostMoonLord", post_moon_lord),
                BuildChapter<EndgameTierChapter>("Endgame", endgame),
            ],
        };

        QuestBooksMod.AddGlobalQuestBooks(QUEST_LOG_KEY, [book], Mod);
    }

    private QuestChapter BuildChapter<TChapter>(string name, Node[] nodes) where TChapter : BasicChapter, new()
    {
        var chapter = new TChapter
        {
            NameKey        = Mod.GetLocalizationKey($"QuestBooks.Progression.{name}"),
            EnableShifting = true,
            DefaultZoom    = 0.6f,
        };

        var displays = new Dictionary<string, QuestDisplay>();

        foreach (var node in nodes)
        {
            var (texture, outline) = node.Kind switch
            {
                NodeKind.Optional => ("Diamond", "DiamondOutline"),
                NodeKind.Gate     => ("Star", "StarOutline"),
                _                 => ("Medium", "MediumOutline"),
            };

            var display = new QuestDisplay
            {
                QuestKey       = node.Key,
                CanvasPosition = new Vector2(node.Column * column_spacing, node.Row * row_spacing),
                UnlockFeeds    = node.After.Length,
                Texture        = texture_path + texture,
                OutlineTexture = texture_path + outline,
            };

            displays[node.Key] = display;
            chapter.Elements.Add(display);
        }

        foreach (var node in nodes)
        {
            foreach (var after in node.After)
            {
                var connector = new Connector
                {
                    Source      = displays[after],
                    Destination = displays[node.Key],
                };

                displays[after].Connections.Add(connector);
                displays[node.Key].Connections.Add(connector);
                chapter.Elements.Add(connector);
            }
        }

        // Centre the view on the tree and allow scrolling across all of it.
        var positions = displays.Values.Select(x => x.CanvasPosition).ToArray();
        var min       = positions.Aggregate(Vector2.Min);
        var max       = positions.Aggregate(Vector2.Max);

        chapter.ViewAnchor   = (min + max) / 2f;
        chapter.MinViewPoint = min;
        chapter.MaxViewPoint = max;

        return chapter;
    }

    // Each tier opens once the previous tier's gate boss is down.

    public sealed class HardmodeTierChapter : ScrollChapter
    {
        public override bool IsUnlocked() => Main.hardMode;
    }

    public sealed class PostPlanteraChapter : ScrollChapter
    {
        public override bool IsUnlocked() => NPC.downedPlantBoss;
    }

    public sealed class PostMoonLordChapter : ScrollChapter
    {
        public override bool IsUnlocked() => NPC.downedMoonlord;
    }

    public sealed class EndgameTierChapter : ScrollChapter
    {
        public override bool IsUnlocked() => DownedBossSystem.downedDoG;
    }
}
