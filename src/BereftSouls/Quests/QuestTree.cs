using System.Collections.Generic;
using System.Linq;

using CalamityMod;

using Microsoft.Xna.Framework;

using QuestBooks.QuestLog;
using QuestBooks.QuestLog.DefaultChapters;
using QuestBooks.QuestLog.DefaultElements;

using Terraria;

namespace BereftSouls.Quests;

internal enum NodeKind
{
    /// <summary>A boss on the critical path to the final bosses.</summary>
    Required,

    /// <summary>A side boss; nothing later depends on it.</summary>
    Optional,

    /// <summary>A boss that unlocks the next chapter, or ends the game.</summary>
    Gate,

    /// <summary>A material, crafting or equipment step between bosses.</summary>
    Step,

    /// <summary>A gear guide entry: an armor set, accessory, weapon or potion.</summary>
    Gear,

    /// <summary>A gear guide section heading, read to complete.</summary>
    Header,
}

/// <param name="Key">Quest key (the quest class name).</param>
/// <param name="Column">Grid column, left to right.</param>
/// <param name="Row">Grid row.</param>
/// <param name="AnyOf">
///     Whether any one of <paramref name="After"/> unlocks this, rather than
///     all of them.
/// </param>
/// <param name="After">
///     Keys of quests in the same chapter that must be completed to unlock
///     this one.
/// </param>
internal readonly record struct Node(string Key, int Column, int Row, NodeKind Kind, bool AnyOf, params string[] After);

/// <summary>
///     Builds QuestBooks chapters from generated node grids.
/// </summary>
internal static class QuestTree
{
    private const float column_spacing = 160f;
    private const float row_spacing    = 130f;

    private const string texture_path = "QuestBooks/Assets/Textures/Quests/";

    public static QuestChapter Build<TChapter>(string nameKey, Node[] nodes) where TChapter : BasicChapter, new()
    {
        var chapter = new TChapter
        {
            NameKey        = nameKey,
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
                NodeKind.Gear     => ("Small", "SmallOutline"),
                NodeKind.Header   => ("Info", "MediumOutline"),
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
