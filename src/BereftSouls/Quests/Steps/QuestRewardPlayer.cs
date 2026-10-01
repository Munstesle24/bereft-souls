using System.Collections.Generic;
using System.Linq;

using JetBrains.Annotations;

using Microsoft.Xna.Framework;

using QuestBooks.Systems;

using Terraria;
using Terraria.DataStructures;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace BereftSouls.Quests.Steps;

/// <summary>
///     Hands out step quest rewards to each player, including any they missed
///     while offline.
/// </summary>
/// <remarks>
///     Steps are shared world quests, but rewards are per character: each
///     player remembers which rewards it has claimed in each world, and claims
///     the rest whenever it is in a world where those steps are complete.
///     This covers players online when a step completes as well as anyone
///     joining later.
/// </remarks>
[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
internal sealed class QuestRewardPlayer : ModPlayer
{
    private const string claimed_key = "ClaimedRewards";

    // How often to look for unclaimed rewards.
    private const int claim_interval = 60;

    // "<world unique id>:<quest key>"
    private HashSet<string> claimed = [];

    public override void SaveData(TagCompound tag)
    {
        tag[claimed_key] = claimed.ToList();
    }

    public override void LoadData(TagCompound tag)
    {
        claimed = tag.GetList<string>(claimed_key).ToHashSet();
    }

    public override void PostUpdate()
    {
        // Rewards are spawned by the owning client, like opening a treasure
        // bag, so they sync in multiplayer.
        if (Player.whoAmI != Main.myPlayer || Main.GameUpdateCount % claim_interval != 0)
        {
            return;
        }

        if (QuestManager.ActiveQuests is not { } quests)
        {
            return;
        }

        var world  = Main.ActiveWorldFileData.UniqueId.ToString();
        var source = new EntitySource_Misc("BereftSouls:QuestReward");

        foreach (var (key, quest) in quests)
        {
            if (quest is not StepQuest { Completed: true, RewardType: > 0 } step)
            {
                continue;
            }

            if (!claimed.Add($"{world}:{key}"))
            {
                continue;
            }

            Player.QuickSpawnItem(source, step.RewardType, step.RewardStack);

            var title = Language.GetTextValue(step.GetLocalizationKey("Title"));
            Main.NewText(Mod.GetLocalization("QuestBooks.RewardClaimed").Format(title), new Color(127, 212, 255));
        }
    }
}
