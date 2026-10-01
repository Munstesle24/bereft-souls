using System.Linq;

using QuestBooks.Quests;

using Terraria;

namespace BereftSouls.Quests.Steps;

/// <summary>
///     A progression step between bosses, such as farming a material or
///     crafting a summon.
/// </summary>
/// <remarks>
///     Steps are world quests, so progress is shared by everyone on the
///     server: one player meeting the goal completes it for all of them.
///     World quests are only checked in singleplayer and on the server.
/// </remarks>
public abstract class StepQuest : Quest
{
    public override QuestType QuestType => QuestType.World;

    // QuestBooks checks incomplete quests every tick; inventory scans for
    // every player don't need to run that often.
    private const int check_interval = 30;

    public override bool CheckCompletion()
    {
        if (Main.GameUpdateCount % check_interval != 0)
        {
            return false;
        }

        return IsMetByWorld() || Main.player.Any(p => p.active && IsMetBy(p));
    }

    protected abstract bool IsMetBy(Player player);

    /// <summary>Whether the world itself meets the goal, e.g. through shared storage.</summary>
    protected virtual bool IsMetByWorld() => false;
}

/// <summary>
///     Completes once a player holds at least <see cref="Stack"/> of an item,
///     however it was obtained (crafted, dropped, mined, bought), or that many
///     are kept in Magic Storage.
/// </summary>
public abstract class ObtainItemQuest : StepQuest
{
    protected abstract int ItemType { get; }

    protected virtual int Stack => 1;

    protected override bool IsMetBy(Player player) => player.CountItem(ItemType, Stack) >= Stack;

    // Items kept in Magic Storage count too.
    protected override bool IsMetByWorld() => MagicStorageCounts.Count(ItemType) >= Stack;
}

/// <summary>
///     Completes once a player has an armor piece or accessory equipped.
/// </summary>
public abstract class EquipItemQuest : StepQuest
{
    protected abstract int ItemType { get; }

    protected override bool IsMetBy(Player player) => IsEquipped(player, ItemType);

    /// <summary>Functional armor and accessory slots only, not vanity.</summary>
    internal static bool IsEquipped(Player player, int type)
    {
        for (var i = 0; i < 10; i++)
        {
            if (player.armor[i].type == type)
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
///     Completes once a player wears every piece of an armor set.
/// </summary>
public abstract class EquipSetQuest : StepQuest
{
    protected abstract int[] Pieces { get; }

    protected override bool IsMetBy(Player player) => Pieces.All(type => EquipItemQuest.IsEquipped(player, type));
}
