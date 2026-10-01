using CalamityMod;

using QuestBooks.Quests;

using SOTS;

using Terraria.ModLoader;

namespace BereftSouls.Quests.Bosses;

/// <summary>
///     A quest completed by defeating a Calamity or SotS boss.
/// </summary>
/// <remarks>
///     Quests are listed in BossChecklist progression order across both mods.
///     Their layout lives in <see cref="ProgressionBook"/>.
/// </remarks>
public abstract class BossQuest : Quest
{
    protected abstract bool Downed { get; }

    public override bool CheckCompletion() => Downed;
}

// 1.6
public sealed class DesertScourgeDefeated : BossQuest
{
    protected override bool Downed => DownedBossSystem.downedDesertScourge;
}

// 2.1
public sealed class GlowmothDefeated : BossQuest
{
    protected override bool Downed => SOTSWorld.downedGlowmoth;
}

// 2.7
public sealed class CrabulonDefeated : BossQuest
{
    protected override bool Downed => DownedBossSystem.downedCrabulon;
}

// 3.98
public sealed class HiveMindDefeated : BossQuest
{
    protected override bool Downed => DownedBossSystem.downedHiveMind;
}

// 3.99
public sealed class PerforatorsDefeated : BossQuest
{
    protected override bool Downed => DownedBossSystem.downedPerforator;
}

// 4.25
public sealed class PutridPinkyDefeated : BossQuest
{
    protected override bool Downed => SOTSWorld.downedPinky;
}

// 4.5
public sealed class PharaohsCurseDefeated : BossQuest
{
    protected override bool Downed => SOTSWorld.downedCurse;
}

// 6.7
public sealed class SlimeGodDefeated : BossQuest
{
    protected override bool Downed => DownedBossSystem.downedSlimeGod;
}

// 6.8
public sealed class ExcavatorDefeated : BossQuest
{
    protected override bool Downed => SOTSWorld.downedExcavator;
}

// 6.9
public sealed class AdvisorDefeated : BossQuest
{
    protected override bool Downed => SOTSWorld.downedAdvisor;
}

// 8.5
public sealed class CryogenDefeated : BossQuest
{
    protected override bool Downed => DownedBossSystem.downedCryogen;
}

// 9.5
public sealed class AquaticScourgeDefeated : BossQuest
{
    protected override bool Downed => DownedBossSystem.downedAquaticScourge;
}

// 10.5
public sealed class BrimstoneElementalDefeated : BossQuest
{
    protected override bool Downed => DownedBossSystem.downedBrimstoneElemental;
}

// 11.01
public sealed class PolarisDefeated : BossQuest
{
    protected override bool Downed => SOTSWorld.downedAmalgamation;
}

// 11.7
public sealed class CalamitasCloneDefeated : BossQuest
{
    protected override bool Downed => DownedBossSystem.downedCalamitasClone;
}

// 12.8
public sealed class LeviathanDefeated : BossQuest
{
    protected override bool Downed => DownedBossSystem.downedLeviathan;
}

// 12.81
public sealed class AstrumAureusDefeated : BossQuest
{
    protected override bool Downed => DownedBossSystem.downedAstrumAureus;
}

// 14.5
public sealed class PlaguebringerDefeated : BossQuest
{
    protected override bool Downed => DownedBossSystem.downedPlaguebringer;
}

// 16.5
public sealed class RavagerDefeated : BossQuest
{
    protected override bool Downed => DownedBossSystem.downedRavager;
}

// 16.5
public sealed class LuxDefeated : BossQuest
{
    protected override bool Downed => SOTSWorld.downedLux;
}

// 17.5
public sealed class AstrumDeusDefeated : BossQuest
{
    protected override bool Downed => DownedBossSystem.downedAstrumDeus;
}

// 17.9
public sealed class SubspaceSerpentDefeated : BossQuest
{
    protected override bool Downed => SOTSWorld.downedSubspace;
}

// 18.5
public sealed class ProfanedGuardiansDefeated : BossQuest
{
    protected override bool Downed => DownedBossSystem.downedGuardians;
}

// 18.6
public sealed class DragonfollyDefeated : BossQuest
{
    protected override bool Downed => DownedBossSystem.downedDragonfolly;
}

// 19
public sealed class ProvidenceDefeated : BossQuest
{
    protected override bool Downed => DownedBossSystem.downedProvidence;
}

// 19.6
public sealed class CeaselessVoidDefeated : BossQuest
{
    protected override bool Downed => DownedBossSystem.downedCeaselessVoid;
}

// 19.61
public sealed class StormWeaverDefeated : BossQuest
{
    protected override bool Downed => DownedBossSystem.downedStormWeaver;
}

// 19.62
public sealed class SignusDefeated : BossQuest
{
    protected override bool Downed => DownedBossSystem.downedSignus;
}

// 20
public sealed class PolterghastDefeated : BossQuest
{
    protected override bool Downed => DownedBossSystem.downedPolterghast;
}

// 20.5
public sealed class OldDukeDefeated : BossQuest
{
    protected override bool Downed => DownedBossSystem.downedBoomerDuke;
}

// 21
public sealed class DevourerOfGodsDefeated : BossQuest
{
    protected override bool Downed => DownedBossSystem.downedDoG;
}

// 22
public sealed class YharonDefeated : BossQuest
{
    protected override bool Downed => DownedBossSystem.downedYharon;
}

// 22.99
public sealed class ExoMechsDefeated : BossQuest
{
    protected override bool Downed => DownedBossSystem.downedExoMechs;
}

// 23
public sealed class SupremeCalamitasDefeated : BossQuest
{
    protected override bool Downed => DownedBossSystem.downedCalamitas;
}
