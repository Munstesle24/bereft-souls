using System;
using System.Collections.Generic;
using System.Linq;

using CalamityMod;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using QuestBooks;
using QuestBooks.QuestLog;
using QuestBooks.QuestLog.DefaultChapters;
using QuestBooks.QuestLog.DefaultElements;
using QuestBooks.Systems;

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
internal readonly record struct Node(string Key, int Column, int Row, NodeKind Kind, bool AnyOf, params string[] After)
{
    /// <summary>
    ///     The tier (0 = Pre-Hardmode .. 4 = Endgame) the world must reach
    ///     before this node is shown, for chapters that grow as the game
    ///     progresses.
    /// </summary>
    public int Tier { get; init; }
}

/// <summary>
///     Builds QuestBooks chapters from generated node grids.
/// </summary>
internal static class QuestTree
{
    private const float column_spacing = 160f;
    private const float row_spacing    = 130f;

    private const string texture_path = "QuestBooks/Assets/Textures/Quests/";

    /// <param name="openAtStart">
    ///     Opens zoomed in on the first quests of the critical path (row 0)
    ///     rather than showing the whole chapter at once, for long chapters
    ///     read left to right.
    /// </param>
    public static TChapter Build<TChapter>(string nameKey, Node[] nodes, bool openAtStart = false) where TChapter : BasicChapter, new()
    {
        var chapter = new TChapter
        {
            NameKey        = nameKey,
            EnableShifting = true,
            DefaultZoom    = openAtStart ? 0.9f : 0.6f,
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

            var display = node.Tier > 0 ? new TierQuestDisplay { Tier = node.Tier } : new IconQuestDisplay();
            display.IconSize = node.Kind switch
            {
                NodeKind.Header                 => 0f,
                NodeKind.Step or NodeKind.Gear  => 26f,
                _                               => 36f,
            };
            display.QuestKey       = node.Key;
            display.CanvasPosition = new Vector2(node.Column * column_spacing, node.Row * row_spacing);
            // Gear entries are a catalog: lines show crafting trees but never lock
            // an entry.
            display.UnlockFeeds    = node.Kind == NodeKind.Gear ? 0 : node.AnyOf ? 1 : node.After.Length;
            display.Texture        = texture_path + texture;
            display.OutlineTexture = texture_path + outline;

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

        // Pad the scroll limits so the outermost quests can be brought in from
        // the page edges.
        var padding = new Vector2(column_spacing, row_spacing);

        // About three columns fit on the page at the opening zoom; start with
        // the first ones in view.
        chapter.ViewAnchor   = openAtStart ? new Vector2(min.X + column_spacing * 1.25f, 0f) : (min + max) / 2f;
        chapter.MinViewPoint = min - padding;
        chapter.MaxViewPoint = max + padding;

        return chapter;
    }
}

/// <summary>
///     A progression section: opens once every boss of the section before it
///     is down, read from those bosses' quests.
/// </summary>
public sealed class SectionChapter : ScrollChapter
{
    /// <summary>Boss quest keys that must all be complete; "A|B" accepts either.</summary>
    public string[] UnlockedBy { get; set; } = [];

    public override bool IsUnlocked() => UnlockedBy.All(group => group.Split('|').Any(Completed));

    private static bool Completed(string questKey) => QuestBooksMod.TryGetQuest(questKey, out var quest) && quest.Completed;
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

/// <summary>
///     A quest display with the quest's item or boss head drawn over its
///     shape.  The picture is greyed while locked and full colour once done.
/// </summary>
public class IconQuestDisplay : QuestDisplay
{
    /// <summary>Canvas size the picture is fitted into, or 0 for none.</summary>
    internal float IconSize;

    // Small sprites aren't blown up past this much, so pixel art stays crisp.
    private const float max_upscale = 2f;

    public override void DrawToCanvas(SpriteBatch spriteBatch, Vector2 canvasViewOffset, float zoom, bool selected, bool hovered)
    {
        base.DrawToCanvas(spriteBatch, canvasViewOffset, zoom, selected, hovered);

        if (IconSize <= 0f || !QuestIcons.TryGet(Quest, out var texture, out var frame))
        {
            return;
        }

        // Locked quests draw their shape black, so their icon is a mid grey
        // silhouette that still reads on top of it.
        var color = Completed() ? Color.White
                  : Unlocked()  ? new Color(210, 210, 210)
                                : new Color(115, 115, 115);

        var scale    = MathF.Min(IconSize / MathF.Max(frame.Width, frame.Height), max_upscale) * zoom;
        var position = (CanvasPosition - canvasViewOffset) * zoom;

        spriteBatch.Draw(texture, position, frame, color, 0f, frame.Size() * 0.5f, scale, SpriteEffects.None, 0f);
    }
}

/// <summary>
///     A quest display that stays hidden until the world reaches its tier,
///     so a single chapter can grow as the game progresses.
/// </summary>
public sealed class TierQuestDisplay : IconQuestDisplay
{
    /// <summary>0 = Pre-Hardmode, 1 = Hardmode, 2 = post-Plantera, 3 = post-Moon Lord, 4 = post-Devourer of Gods.</summary>
    public int Tier { get; set; }

    public static bool TierReached(int tier)
    {
        return tier switch
        {
            <= 0 => true,
            1    => Main.hardMode,
            2    => NPC.downedPlantBoss,
            3    => NPC.downedMoonlord,
            _    => DownedBossSystem.downedDoG,
        };
    }

    // Completed entries stay visible even if the tier check would hide them.
    public override bool VisibleOnCanvas() => TierReached(Tier) ? base.VisibleOnCanvas() : Quest.Completed || QuestLogDrawer.ActiveStyle.UseDesigner;
}
