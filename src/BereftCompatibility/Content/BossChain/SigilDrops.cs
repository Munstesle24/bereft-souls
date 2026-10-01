using System;
using System.Collections.Generic;
using System.Linq;

using CalamityMod.CalPlayer;
using CalamityMod.NPCs.AstrumDeus;
using CalamityMod.NPCs.Leviathan;
using CalamityMod.NPCs.OldDuke;

using CalamityMod;

using JetBrains.Annotations;

using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

using static Terraria.ModLoader.ModContent;

namespace BereftCompatibility.Content.BossChain;

/// <summary>Every chain boss drops its Sigil for each player, on every kill.</summary>
[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
internal sealed class SigilDrops : GlobalNPC
{
    private sealed class When(Func<DropAttemptInfo, bool> canDrop) : IItemDropRuleCondition
    {
        public bool CanDrop(DropAttemptInfo info) => canDrop(info);

        public bool CanShowItemDropInUI() => true;

        public string? GetConditionDescription() => null;
    }

    private static Dictionary<int, List<ChainLink>>? loot_links;

    public override void Unload()
    {
        base.Unload();

        loot_links = null;
    }

    public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
    {
        base.ModifyNPCLoot(npc, npcLoot);

        loot_links ??= BossChain.WithSigils
                                .SelectMany(l => l.LootTypes().Select(t => (t, l)))
                                .GroupBy(p => p.t)
                                .ToDictionary(g => g.Key, g => g.Select(p => p.l).ToList());

        if (!loot_links.TryGetValue(npc.type, out var links))
        {
            return;
        }

        foreach (var link in links)
        {
            var rule = new LeadingConditionRule(Condition(link.Drop));
            rule.OnSuccess(new DropHelper.PerPlayerDropRule(SigilLoader.Type(link), 1, link.DropCount, link.DropCount));
            npcLoot.Add(rule);
        }
    }

    private static IItemDropRuleCondition Condition(DropRule rule) => rule switch
    {
        DropRule.LastEaterSegment => new Conditions.LegacyHack_IsABoss(),
        DropRule.LastTwin         => new Conditions.MissingTwin(),
        DropRule.LastLeviathan    => new When(_ => Leviathan.LastAnLStanding()),
        DropRule.LastAstrumDeus   => new When(info => !AstrumDeusHead.ShouldNotDropThings(info.npc)),
        _                         => new When(_ => true),
    };
}

/// <summary>
///     Duke Fishron and the Old Duke only come for Sigil baits until they've
///     been beaten once.
/// </summary>
[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
internal sealed class ChainFishing : ModPlayer
{
    public override void CatchFish(
        FishingAttempt              attempt,
        ref int                     itemDrop,
        ref int                     npcSpawn,
        ref AdvancedPopupRequest    sonar,
        ref Microsoft.Xna.Framework.Vector2 sonarPosition
    )
    {
        base.CatchFish(attempt, ref itemDrop, ref npcSpawn, ref sonar, ref sonarPosition);

        if (attempt.inLava || attempt.inHoney)
        {
            return;
        }

        var bait    = attempt.playerFishingConditions.BaitItemType;
        var oldDuke = NPCType<OldDuke>();

        if (bait == ItemType<SigilTruffleWorm>())
        {
            if (Player.ZoneBeach && !NPC.AnyNPCs(NPCID.DukeFishron))
            {
                npcSpawn = NPCID.DukeFishron;
                itemDrop = -1;
            }

            return;
        }

        if (bait == ItemType<SigilBloodworm>())
        {
            // Our hook runs after Calamity's, which only reacts to its own Bloodworm.
            if (Player.GetModPlayer<CalamityPlayer>().ZoneSulphur && !NPC.AnyNPCs(oldDuke))
            {
                npcSpawn = oldDuke;
                itemDrop = -1;
            }

            return;
        }

        if (npcSpawn == NPCID.DukeFishron && !NPC.downedFishron)
        {
            npcSpawn = -1;
            ChainRules.Warn(Player, ChainRules.Text("NeedTruffleWorm"));
        }
        else if (npcSpawn == oldDuke && !DownedBossSystem.downedBoomerDuke)
        {
            npcSpawn = -1;
            ChainRules.Warn(Player, ChainRules.Text("NeedBloodworm"));
        }
    }
}
