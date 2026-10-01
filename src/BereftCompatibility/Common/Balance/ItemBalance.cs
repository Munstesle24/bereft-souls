using System.Collections.Generic;

using JetBrains.Annotations;

using SOTS.Items;
using SOTS.Items.AbandonedVillage;
using SOTS.Items.Celestial;
using SOTS.Items.Chaos;
using SOTS.Items.ChestItems;
using SOTS.Items.Earth.Glowmoth;
using SOTS.Items.Planetarium.FromChests;
using SOTS.Items.Pyramid;
using SOTS.Items.Slime;
using SOTS.Items.Tide;

using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

using static Terraria.ModLoader.ModContent;

namespace BereftCompatibility.Common.Balance;

[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
internal sealed class ItemBalance : GlobalItem
{
    /// <param name="Damage">New base damage.</param>
    /// <param name="Rarity">New rarity, or -1 to leave it alone.</param>
    private readonly record struct WeaponStats(int Damage, int Rarity = -1);

    private static readonly HashSet<int> summon_items =
    [
        ItemType<SuspiciousLookingCandle>(),
        ItemType<JarOfPeanuts>(),
    ];

    // SotS weapons whose nominal DPS is well off Calamity's curve for the
    // point in progression they become obtainable.  Original stats are noted
    // alongside; the tier is where the weapon is first obtainable.
    private static readonly Dictionary<int, WeaponStats> weapon_stats = [];

    public override void SetStaticDefaults()
    {
        base.SetStaticDefaults();

        // Pre-boss.  Calamity curve is 30-50 DPS.
        weapon_stats[ItemType<PistolShrimp>()] = new WeaponStats(7);  // 11
        weapon_stats[ItemType<MinersSword>()]  = new WeaponStats(13); // 20

        // Putrid Pinky / Pharaoh's Curse, alongside the Hive Mind and
        // Perforators.  Calamity curve is 55-95 DPS.
        weapon_stats[ItemType<WormWoodStaff>()] = new WeaponStats(12, ItemRarityID.Orange); // 20, LightRed
        weapon_stats[ItemType<SpiritTracer>()]  = new WeaponStats(22, ItemRarityID.Orange); // 33, Pink

        // Advisor, alongside the Slime God.  Calamity curve is 60-120 DPS.
        weapon_stats[ItemType<StarcoreAssaultRifle>()] = new WeaponStats(13, ItemRarityID.LightRed); // 23, LightPurple
        weapon_stats[ItemType<CrescentStaff>()]        = new WeaponStats(20, ItemRarityID.LightRed); // 36, LightPurple
        weapon_stats[ItemType<SkywardBlades>()]        = new WeaponStats(22, ItemRarityID.LightRed); // 33, LightPurple

        // Early Hardmode.  Calamity curve is 100-220 DPS.
        weapon_stats[ItemType<StreetCleaner>()]   = new WeaponStats(15); // 24
        weapon_stats[ItemType<JeweledGauntlet>()] = new WeaponStats(38); // 54
        weapon_stats[ItemType<GhoulBlaster>()]    = new WeaponStats(18); // 28

        // Post-Plantera.  Calamity curve is 120-250 DPS.
        weapon_stats[ItemType<RebarRifle>()] = new WeaponStats(85); // 135

        // Lux, alongside Ravager.  Calamity curve is 190-420 DPS.
        weapon_stats[ItemType<Armaggedon>()]   = new WeaponStats(65); // 190
        weapon_stats[ItemType<ChaosChamber>()] = new WeaponStats(45); // 70
        weapon_stats[ItemType<StellarShot>()]  = new WeaponStats(35); // 50

        // Subspace Serpent, alongside Astrum Deus.  Calamity curve is 250-450
        // DPS.  Also gated behind Astral Bars, see RecipeTweaks.
        weapon_stats[ItemType<Apocalypse>()] = new WeaponStats(75, ItemRarityID.Cyan); // 330, Yellow
    }

    public override void Unload()
    {
        weapon_stats.Clear();
    }

    public override void SetDefaults(Item entity)
    {
        base.SetDefaults(entity);

        // Make uncomsumable like Calamity's edits to boss summons.
        if (summon_items.Contains(entity.type))
        {
            entity.maxStack   = 1;
            entity.consumable = false;

            // Calamity additionally makes this change, for some reason.  We
            // won't do this because it causes items to be used multiple times
            // if the useAnimation value is greater than useTime.  Seems
            // pointless.
            // entity.useTime = 10;
        }

        if (weapon_stats.TryGetValue(entity.type, out var stats))
        {
            entity.damage = stats.Damage;

            if (stats.Rarity >= 0)
            {
                entity.rare = stats.Rarity;
            }
        }
    }

    public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
    {
        var balanceLine = Language.GetTextValue("Mods.FargowiltasCrossmod.EModeBalance.CrossBalanceGeneric");

        var balanceUpLine   = $"[c/00A36C:{balanceLine}]";
        var balanceDownLine = $"[c/FF0000:{balanceLine}]";

        if (item.type == ItemType<FoggyClairvoyance>())
        {
            NerfTooltip("FoggyClairvoyance");
        }

        if (weapon_stats.ContainsKey(item.type))
        {
            NerfTooltip("DamageReduced");
        }

        return;

        void BuffTooltip(string key)
        {
            tooltips.Add(new TooltipLine(Mod, "BalanceUp", $"{balanceUpLine}" + BalanceTooltips(key)));
        }

        void NerfTooltip(string key)
        {
            tooltips.Add(new TooltipLine(Mod, "BalanceDown", $"{balanceDownLine}" + BalanceTooltips(key)));
        }

        static string BalanceTooltips(string key)
        {
            return Language.GetTextValue($"Mods.BereftCompatibility.ItemBalance.{key}");
        }
    }
}
