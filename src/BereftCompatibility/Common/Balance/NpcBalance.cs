using System;
using System.Collections.Generic;

using CalamityMod;
using CalamityMod.World;

using FargowiltasSouls.Core.Systems;

using JetBrains.Annotations;

using SOTS.NPCs;
using SOTS.NPCs.Boss;
using SOTS.NPCs.Boss.Advisor;
using SOTS.NPCs.Boss.Curse;
using SOTS.NPCs.Boss.Excavator;
using SOTS.NPCs.Boss.Glowmoth;
using SOTS.NPCs.Boss.Lux;
using SOTS.NPCs.Boss.Polaris;
using SOTS.NPCs.Boss.Polaris.NewPolaris;

using Terraria;
using Terraria.ModLoader;

using static Terraria.ModLoader.ModContent;

// ReSharper disable InconsistentNaming

namespace BereftCompatibility.Common.Balance;

/// <summary>
///     Rebalances SotS bosses against Calamity's progression curve.
/// </summary>
/// <remarks>
///     Health values are written the way Calamity writes its own bosses: the
///     pre-Expert value for Normal and Revengeance/Death.  Targets were picked
///     by interpolating the neighbouring Calamity bosses' health by
///     BossChecklist progression weight (Calamity 2.2.2, SotS 0.25.1.9).
/// </remarks>
[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
public sealed class NpcBalance : GlobalNPC
{
    /// <param name="Life">Base health, or 0 to leave it alone.</param>
    /// <param name="RevLife">Base health in Revengeance and Death.</param>
    /// <param name="Damage">Contact damage, or 0 to leave it alone.</param>
    /// <param name="SotsLifeScale">
    ///     The Expert health multiplier SotS applies in
    ///     <c>ApplyDifficultyAndPlayerScaling</c>, or 0 if it applies none.
    /// </param>
    /// <param name="SotsDamageScale">
    ///     The Expert damage multiplier SotS applies, or 0 if it applies none.
    /// </param>
    /// <param name="ExpertDamageScale">
    ///     Calamity's Expert damage multiplier for bosses at this point in
    ///     progression (0.8 for its earliest bosses, otherwise 1).
    /// </param>
    /// <param name="EternityLifeScale">
    ///     Extra health in Eternity Mode, mirroring the Fargo's DLC buffs to
    ///     nearby Calamity bosses.
    /// </param>
    private readonly record struct BossStats(
        int   Life,
        int   RevLife,
        int   Damage,
        float SotsLifeScale,
        float SotsDamageScale,
        float ExpertDamageScale = 1f,
        float EternityLifeScale = 1f
    );

    // Calamity's own bosses multiply health by this in Expert and above.
    private const float calamity_expert_life_scale = 0.8f;

    private static readonly Dictionary<int, BossStats> boss_stats = [];

    public override void SetStaticDefaults()
    {
        base.SetStaticDefaults();

        // Between Desert Scourge (4000/5000) and Crabulon (3500/4400).  Base
        // was 2800 HP / 28 damage.
        boss_stats[NPCType<Glowmoth>()] = new BossStats(3800, 4700, 40, 0.75f, 0.75f, 0.8f, 1.2f);

        // One projectile, spawned from Glowmoth minion, has explicitly-typed
        // damage.  This applies to the rest of the fight's projectiles.
        boss_stats[NPCType<GlowmothMinion>()] = new BossStats(0, 0, 40, 0f, 0f);

        // Just after the Hive Mind (5000/7000), well before the Slime God.
        // Base was 5500 HP / 40 damage.
        boss_stats[NPCType<PutridPinky1>()]      = new BossStats(0,    0,    40, 0f,    0f);
        boss_stats[NPCType<PutridPinkyPhase2>()] = new BossStats(6000, 8100, 40, 0.7f,  0.8f,  0.8f, 1.2f);
        boss_stats[NPCType<PutridHook>()]        = new BossStats(450,  600,  40, 0.75f, 0.75f, 0.8f, 1.2f);

        // Refills to full health once on entering its second phase, so this is
        // half of the 7000/9200 target.  Base was 4000 HP / 45 damage.
        boss_stats[NPCType<PharaohsCurse>()] = new BossStats(3500, 4600, 42, 0.625f, 0.75f, 0.8f, 1.2f);

        // Just after the Slime God (15500/18600 across both paladins).  The
        // body segments share the head's health.  Base was 17500 HP / 42
        // damage.
        boss_stats[NPCType<Excavator>()] = new BossStats(16000, 19500, 42, 0.6723f, 0.75f, 1f, 1.2f);

        // Same tier as the Excavator.  Its health is reset every tick while
        // dormant, see Advisor_ScaleExpertStats_Detour.  Base was 12500 HP / 54
        // damage.
        boss_stats[NPCType<TheAdvisorHead>()] = new BossStats(16500, 20000, 50, 0.64002f, 0.8f, 1f, 1.2f);
        boss_stats[NPCType<PhaseEye>()]       = new BossStats(68,    68,    47, 0f,       0f);

        // Between Brimstone Elemental (30000/49200) and Calamitas Clone plus
        // her brothers (44000/66875).  Base was 33000 HP / 80 damage.
        boss_stats[NPCType<NewPolaris>()] = new BossStats(36000, 56000, 62, 0.636364f, 0.75f);
        boss_stats[NPCType<Polaris>()]    = new BossStats(36000, 56000, 62, 0.63889f,  0.75f);

        // Same progression weight as Ravager, short of Astrum Deus.  Midpoint
        // of their effective health after DR (46k/83k and 167k/267k).  Base was
        // 60000 HP / 100 damage.
        boss_stats[NPCType<Lux>()]     = new BossStats(105000, 170000, 100, 0.75f, 0.75f);
        boss_stats[NPCType<FakeLux>()] = new BossStats(0,      0,      90,  0f,    0f);

        // Just after Astrum Deus (150000/240000).  Base was 160000 HP / 100
        // damage.
        boss_stats[NPCType<SubspaceSerpentHead>()] = new BossStats(160000, 250000, 110, 0.75f, 0.8f);
    }

    public override void SetDefaults(NPC npc)
    {
        if (boss_stats.TryGetValue(npc.type, out var stats))
        {
            if (stats.Life > 0)
            {
                npc.lifeMax = GetLife(stats);
            }

            if (stats.Damage > 0)
            {
                npc.damage = stats.Damage;
            }
        }

        var calNpc = npc.Calamity();

        // Glowmoth
        if (npc.type == NPCType<Glowmoth>())
        {
            calNpc.VulnerableToSickness = true;
        }

        // Putrid Pinky
        if (npc.type == NPCType<PutridPinky1>() || npc.type == NPCType<PutridPinkyPhase2>() || npc.type == NPCType<PutridHook>())
        {
            calNpc.VulnerableToSickness = false;
            calNpc.VulnerableToHeat     = true;
        }

        // Pharaoh
        if (npc.type == NPCType<PharaohsCurse>())
        {
            ModCalls.SetDefenseDamageNPC(npc, true);

            calNpc.VulnerableToCold     = true;
            calNpc.VulnerableToHeat     = false;
            calNpc.VulnerableToSickness = false;
        }

        // Excavator
        if (npc.type == NPCType<Excavator>())
        {
            ModCalls.SetDefenseDamageNPC(npc, true);

            calNpc.VulnerableToElectricity = true;
            calNpc.VulnerableToSickness    = false;
        }

        // The Advisor
        if (npc.type == NPCType<TheAdvisorHead>() || npc.type == NPCType<PhaseEye>())
        {
            calNpc.VulnerableToElectricity = true;
            calNpc.VulnerableToSickness    = false;
        }

        // Polaris
        if (npc.type == NPCType<Polaris>() || npc.type == NPCType<NewPolaris>())
        {
            ModCalls.SetDefenseDamageNPC(npc, true);

            calNpc.VulnerableToElectricity = true;
            calNpc.VulnerableToHeat        = true;
            calNpc.VulnerableToCold        = false;
            calNpc.VulnerableToSickness    = false;
        }

        // Lux
        if (npc.type == NPCType<Lux>())
        {
            ModCalls.SetDefenseDamageNPC(npc, true);

            calNpc.VulnerableToSickness = false;
        }

        // Subspace Serpent
        if (npc.type == NPCType<SubspaceSerpentHead>())
        {
            ModCalls.SetDefenseDamageNPC(npc, true);

            calNpc.VulnerableToHeat     = false;
            calNpc.VulnerableToCold     = true;
            calNpc.VulnerableToWater    = true;
            calNpc.VulnerableToSickness = false;
        }
    }

    public override void ApplyDifficultyAndPlayerScaling(NPC npc, int numPlayers, float balance, float bossAdjustment)
    {
        if (!boss_stats.TryGetValue(npc.type, out var stats))
        {
            return;
        }

        // SotS shrinks Expert stats by its own per-boss factors; swap those
        // for the ones Calamity uses on its bosses.  Runs after the ModNPC
        // hook.
        if (stats.SotsLifeScale > 0f)
        {
            npc.lifeMax = (int)Math.Round(npc.lifeMax * (calamity_expert_life_scale / stats.SotsLifeScale));
        }

        if (stats.SotsDamageScale > 0f)
        {
            npc.damage = (int)Math.Round(npc.damage * (stats.ExpertDamageScale / stats.SotsDamageScale));
        }
    }

    public override void Load()
    {
        MonoModHooks.Add(
            typeof(TheAdvisorHead).GetMethod(nameof(TheAdvisorHead.ScaleExpertStats), GENERIC_FLAGS),
            Advisor_ScaleExpertStats_Detour
        );
    }

    public override void Unload()
    {
        boss_stats.Clear();
    }

    private static void Advisor_ScaleExpertStats_Detour(Action<TheAdvisorHead> orig, TheAdvisorHead self)
    {
        orig(self);

        var stats = boss_stats[NPCType<TheAdvisorHead>()];

        self.NPC.life = self.NPC.lifeMax = GetLife(stats);

        self.NPC.damage = stats.Damage;
        self.NPC.ScaleStats(null, Main.GameModeInfo, null);
    }

    private static int GetLife(BossStats stats)
    {
        // Leave bestiary and menu stats at their base values.
        if (Main.gameMenu)
        {
            return stats.Life;
        }

        var lifeMax = CalamityWorld.revenge ? stats.RevLife : stats.Life;

        if (WorldSavingSystem.EternityMode)
        {
            lifeMax = (int)Math.Round(lifeMax * stats.EternityLifeScale);
        }

        // Calamity applies its boss health boost config in its own
        // SetDefaults, which runs before ours, so it has to be reapplied after
        // overwriting lifeMax.
        if (CalamityServerConfig.Instance is { } config)
        {
            lifeMax += (int)Math.Round(lifeMax * config.BossHealthBoost * 0.01);
        }

        return lifeMax;
    }
}
