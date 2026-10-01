using System.Collections.Generic;

using CalamityMod.Items.DraedonMisc;
using CalamityMod.Items.SummonItems;

using JetBrains.Annotations;

using SOTS.Items.Celestial;
using SOTS.Items.Earth.Glowmoth;
using SOTS.Items.Permafrost;
using SOTS.Items.Slime;

using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

using static Terraria.ModLoader.ModContent;

namespace BereftCompatibility.Content.BossChain;

/// <summary>
///     Adds each boss's predecessor Sigil to its summon item's recipe.
/// </summary>
/// <remarks>
///     Fargo's copies of summon items are left alone: they're crafted 1:1
///     from the originals (which now need the Sigil) or bought from the Mutant
///     after the boss is beaten.  Found summons (Gelatin Crystal, Lihzahrd
///     Power Cell, Profaned Core) are gated by <see cref="ChainGuards"/>
///     instead.
/// </remarks>
[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
internal sealed class SigilRecipes : ModSystem
{
    public const string HIVE_OR_PERFORATOR_GROUP = "BereftCompatibility:HiveMindOrPerforatorSigil";

    public override void AddRecipeGroups()
    {
        base.AddRecipeGroups();

        RecipeGroup.RegisterGroup(
            HIVE_OR_PERFORATOR_GROUP,
            new RecipeGroup(
                () => Language.GetTextValue("Mods.BereftCompatibility.BossChain.HiveOrPerforatorGroup"),
                SigilLoader.Type("HiveMind"),
                SigilLoader.Type("Perforator")
            )
        );
    }

    public override void PostAddRecipes()
    {
        base.PostAddRecipes();

        var additions = GetAdditions();

        for (var i = 0; i < Recipe.numRecipes; i++)
        {
            var recipe = Main.recipe[i];
            if (recipe?.createItem is not { } result || result.IsAir || recipe.Disabled)
            {
                continue;
            }

            // Calamity's sequence-break Devourer of Gods summon (Luminite and
            // fragments) would skip the Sentinels and the Old Duke.
            if (recipe.HasResult<CosmicWorm>() && recipe.HasIngredient(ItemID.LunarBar))
            {
                recipe.DisableRecipe();
                continue;
            }

            if (!additions.TryGetValue(result.type, out var sigils) || recipe.Mod?.Name is "Fargowiltas" or "FargowiltasCrossmod")
            {
                continue;
            }

            foreach (var sigil in sigils)
            {
                if (sigil == HIVE_OR_PERFORATOR_GROUP)
                {
                    recipe.AddRecipeGroup(HIVE_OR_PERFORATOR_GROUP);
                }
                else
                {
                    recipe.AddIngredient(SigilLoader.Type(sigil));
                }
            }
        }
    }

    private static Dictionary<int, string[]> GetAdditions()
    {
        return new Dictionary<int, string[]>
        {
            // Pre-Hardmode.
            [ItemType<SuspiciousLookingCandle>()] = ["KingSlime"],
            [ItemType<DesertMedallion>()]         = ["Glowmoth"],
            [ItemID.SuspiciousLookingEye]         = ["DesertScourge"],
            [ItemType<DecapoditaSprout>()]        = ["EyeOfCthulhu"],
            [ItemID.WormFood]                     = ["Crabulon"],
            [ItemID.BloodySpine]                  = ["Crabulon"],
            [ItemType<Teratoma>()]                = ["WorldEvil"],
            [ItemType<BloodyWormFood>()]          = ["WorldEvil"],
            [ItemID.Abeemination]                 = [HIVE_OR_PERFORATOR_GROUP],
            [ItemType<JarOfPeanuts>()]            = ["QueenBee"],
            [ItemID.DeerThing]                    = ["Skeletron"],
            [ItemType<OverloadedSludge>()]        = ["Deerclops"],

            // Early Hardmode.
            [ItemType<CryoKey>()]         = ["QueenSlime"],
            [ItemType<Seafood>()]         = ["Cryogen"],
            [ItemType<CharredIdol>()]     = ["AquaticScourge"],
            [ItemID.MechanicalWorm]       = ["BrimstoneElemental"],
            [ItemID.MechanicalEye]        = ["Destroyer"],
            [ItemID.MechanicalSkull]      = ["Twins"],
            [ItemType<FrostedKey>()]      = ["SkeletronPrime"],
            [ItemType<EyeofDesolation>()] = ["Polaris"],

            // Post-Plantera.
            [ItemType<NaiadsWarhorn>()] = ["Plantera"],
            [ItemType<AstralChunk>()]   = ["Leviathan"],
            [ItemType<Abombination>()]  = ["Golem"],
            [ItemType<DeathWhistle>()]  = ["EmpressOfLight"],
            [ItemType<Starcore>()]      = ["LunaticCultist"],
            [ItemType<CatalystBomb>()]  = ["AstrumDeus"],
            [ItemID.CelestialSigil]     = ["SubspaceSerpent"],

            // Post-Moon Lord.
            [ItemType<ProfanedShard>()]      = ["MoonLord"],
            [ItemType<ExoticPheromones>()]   = ["ProfanedGuardians"],
            [ItemType<MarkofProvidence>()]   = ["Providence"],
            [ItemType<NecroplasmicBeacon>()] = ["StormWeaver", "CeaselessVoid", "Signus"],
            [ItemType<CosmicWorm>()]         = ["OldDuke"],

            // Endgame.
            [ItemType<YharonEgg>()]               = ["DevourerOfGods"],
            [ItemType<AuricQuantumCoolingCell>()] = ["SupremeCalamitas"],
        };
    }
}

/// <summary>Lists every Sigil in Fargo's Mutant shop once its boss is beaten.</summary>
[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
internal sealed class SigilShop : ModSystem
{
    private static readonly int[] tier_prices =
    [
        Item.buyPrice(gold: 3),
        Item.buyPrice(gold: 15),
        Item.buyPrice(gold: 40),
        Item.buyPrice(platinum: 1),
        Item.buyPrice(platinum: 3),
    ];

    public static int Price(ChainLink link) => tier_prices[link.Tier];

    public override void PostSetupContent()
    {
        base.PostSetupContent();

        // Summons must be registered before Fargo's finalises them in AddRecipes.
        if (!ModLoader.TryGetMod("Fargowiltas", out var mutant))
        {
            return;
        }

        foreach (var link in BossChain.WithSigils)
        {
            mutant.Call("AddSummon", link.ShopProgression, Mod.Name, link.SigilName, link.Downed, Price(link));
        }
    }
}
