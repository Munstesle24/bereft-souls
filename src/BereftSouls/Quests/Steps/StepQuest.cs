using System.Linq;

using QuestBooks.Quests;

using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

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

    /// <summary>The reward item given to every online player, or 0 for none.</summary>
    protected virtual int RewardType => 0;

    protected virtual int RewardStack => 1;

    // QuestBooks checks incomplete quests every tick; inventory scans for
    // every player don't need to run that often.
    private const int check_interval = 30;

    public override bool CheckCompletion()
    {
        if (Main.GameUpdateCount % check_interval != 0)
        {
            return false;
        }

        return Main.player.Any(p => p.active && IsMetBy(p));
    }

    protected abstract bool IsMetBy(Player player);

    public override void OnCompletion()
    {
        // Completion runs on the server and every client; only hand out
        // rewards once, from the server (or in singleplayer).
        if (RewardType <= 0 || Main.netMode == NetmodeID.MultiplayerClient)
        {
            return;
        }

        var source = new EntitySource_Misc("BereftSouls:QuestReward");

        foreach (var player in Main.player.Where(p => p.active))
        {
            player.QuickSpawnItem(source, RewardType, RewardStack);
        }
    }
}

/// <summary>
///     Completes once a player holds at least <see cref="Stack"/> of an item,
///     however it was obtained (crafted, dropped, mined, bought).
/// </summary>
public abstract class ObtainItemQuest : StepQuest
{
    protected abstract int ItemType { get; }

    protected virtual int Stack => 1;

    protected override bool IsMetBy(Player player) => player.CountItem(ItemType, Stack) >= Stack;
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
