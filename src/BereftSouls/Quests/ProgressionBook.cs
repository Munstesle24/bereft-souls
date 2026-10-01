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
///     is down.  Material and crafting steps sit between bosses; optional
///     bosses branch off and feed nothing later on.  Node data is generated
///     into ProgressionBook.Nodes.cs by tools/generate_quests.py.  Positions
///     are a rough grid meant to be refined in the QuestBooks designer, after
///     which this can be swapped for an exported log.
/// </remarks>
[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
internal sealed partial class ProgressionBook : ModSystem
{
    public const string QUEST_LOG_KEY = "BereftSouls";

    private const float column_spacing = 160f;
    private const float row_spacing    = 130f;

    private const string texture_path = "QuestBooks/Assets/Textures/Quests/";

    private enum NodeKind
    {
        /// <summary>A boss on the critical path to the final bosses.</summary>
        Required,

        /// <summary>A side boss; nothing later depends on it.</summary>
        Optional,

        /// <summary>A boss that unlocks the next chapter, or ends the game.</summary>
        Gate,

        /// <summary>A material, crafting or equipment step between bosses.</summary>
        Step,
    }

    /// <param name="Key">Quest key (the quest class name).</param>
    /// <param name="Column">Grid column, left to right in progression order.</param>
    /// <param name="Row">Grid row; 0 is the main line.</param>
    /// <param name="AnyOf">
    ///     Whether any one of <paramref name="After"/> unlocks this, rather
    ///     than all of them.
    /// </param>
    /// <param name="After">
    ///     Keys of quests in the same chapter that must be completed to unlock
    ///     this one.
    /// </param>
    private readonly record struct Node(string Key, int Column, int Row, NodeKind Kind, bool AnyOf, params string[] After);

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
                NodeKind.Step     => ("Small", "SmallOutline"),
                _                 => ("Medium", "MediumOutline"),
            };

            var display = new QuestDisplay
            {
                QuestKey       = node.Key,
                CanvasPosition = new Vector2(node.Column * column_spacing, node.Row * row_spacing),
                UnlockFeeds    = node.AnyOf ? 1 : node.After.Length,
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
