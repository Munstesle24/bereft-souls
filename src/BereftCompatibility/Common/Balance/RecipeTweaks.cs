using System.Collections.Generic;

using CalamityMod.Items.Accessories;
using CalamityMod.Items.Armor.Astral;
using CalamityMod.Items.Armor.Daedalus;
using CalamityMod.Items.Armor.Empyrean;
using CalamityMod.Items.Armor.Statigel;
using CalamityMod.Items.Armor.Victide;
using CalamityMod.Items.Materials;
using CalamityMod.Items.Placeables.SunkenSea;
using CalamityMod.Items.SummonItems;

using JetBrains.Annotations;

using SOTS.Items;
using SOTS.Items.Celestial;
using SOTS.Items.Chaos;
using SOTS.Items.Earth;
using SOTS.Items.Earth.Glowmoth;
using SOTS.Items.Fragments;
using SOTS.Items.Nature;
using SOTS.Items.Permafrost;
using SOTS.Items.Planetarium.FromChests;
using SOTS.Items.Planetarium.Furniture;
using SOTS.Items.Pyramid;
using SOTS.Items.Slime;

using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

using static Terraria.ModLoader.ModContent;

namespace BereftCompatibility.Common.Balance;

/// <summary>
///     Interlocks SotS and Calamity progression by having each mod's key
///     items need a same-tier material from the other.
/// </summary>
/// <remarks>
///     Every added material is obtainable at or before the result's existing
///     tier.  Some additions make an otherwise optional boss part of the
///     critical path; those are called out below and mirrored in the
///     BereftSouls progression book.
/// </remarks>
[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
internal sealed class RecipeTweaks : ModSystem
{
    private readonly record struct Addition(int Ingredient, int Stack);

    public override void PostAddRecipes()
    {
        base.PostAddRecipes();

        var additions = GetAdditions();

        foreach (var recipe in Main.recipe)
        {
            if (recipe.createItem is not { } result || result.IsAir)
            {
                continue;
            }

            // Frost Key
            if (recipe.HasResult<FrostedKey>())
            {
                // Swap out 1 Frost Core for 8 Cryonic Bars.
                recipe.RemoveIngredient(ItemID.FrostCore);
                recipe.AddIngredient<CryonicBar>(8);
            }

            // Apocalypse
            if (recipe.HasResult<Apocalypse>())
            {
                // Gate behind Astrum Deus' tier rather than just the Subspace
                // Serpent.
                recipe.AddIngredient<AstralBar>(8);
            }

            // SotS' alloys can also be made by transmuting each other; only
            // gate the Hardlight Fabricator recipes.
            if ((recipe.HasResult<StarlightAlloy>() || recipe.HasResult<HardlightAlloy>() || recipe.HasResult<OtherworldlyAlloy>())
             && recipe.HasTile<HardlightFabricatorTile>())
            {
                recipe.AddIngredient<AerialiteBar>();
            }

            if (!additions.TryGetValue(result.type, out var toAdd))
            {
                continue;
            }

            // Only touch the owning mod's own recipes, not alternatives other
            // mods (e.g. Fargo's) add for the same item.
            if (recipe.Mod is null || recipe.Mod != result.ModItem?.Mod)
            {
                continue;
            }

            foreach (var addition in toAdd)
            {
                recipe.AddIngredient(addition.Ingredient, addition.Stack);
            }
        }
    }

    private static Dictionary<int, List<Addition>> GetAdditions()
    {
        var additions = new Dictionary<int, List<Addition>>();

        // SotS items gaining Calamity materials.

        Add<SuspiciousLookingCandle, PrismShard>(3);
        // Puts Putrid Pinky after the Hive Mind or Perforators, which unlock
        // Aerialite.
        Add<JarOfPeanuts, AerialiteBar>(2);
        Add<ElectromagneticLure, LifeAlloy>(2);
        Add<CatalystBomb, MeldBlob>(4);

        Add<VibrantBar, WulfrumMetalScrap>(1);
        Add<AbsoluteBar, EssenceofEleum>(1);
        Add<PhaseBar, EssenceofSunlight>(2);

        Add<NatureWreath, SulphuricScale>(3);
        Add<NatureShirt, SulphuricScale>(4);
        Add<NatureLeggings, SulphuricScale>(3);

        Add<CursedHood, StormlionMandible>(2);
        Add<CursedRobe, StormlionMandible>(3);

        Add<TwilightAssassinsCirclet, PurifiedGel>(4);
        Add<TwilightAssassinsChestplate, PurifiedGel>(6);
        Add<TwilightAssassinsLeggings, PurifiedGel>(5);

        Add<FrostArtifactHelmet, CryonicBar>(4);
        Add<FrostArtifactChestplate, CryonicBar>(6);
        Add<FrostArtifactTrousers, CryonicBar>(5);

        Add<ElementalHelmet, ScoriaBar>(4);
        Add<ElementalBreastplate, ScoriaBar>(6);
        Add<ElementalLeggings, ScoriaBar>(5);

        Add<VoidspaceMask, AstralBar>(4);
        Add<VoidspaceBreastplate, AstralBar>(6);
        Add<VoidspaceLeggings, AstralBar>(5);

        Add<KingBlade, EssenceofEleum>(6);
        Add<Metalmalgamation, CryonicBar>(4);
        Add<RealityShatter, PlagueCellCanister>(6);

        // Calamity items gaining SotS materials.

        Add<DesertMedallion, FragmentOfEarth>(3);
        Add<DecapoditaSprout, FragmentOfNature>(3);
        Add<Teratoma, FragmentOfEvil>(3);
        Add<BloodyWormFood, FragmentOfEvil>(3);
        // Wormwood comes from Putrid Pinky, or from wood and gel at a
        // Solidifier.
        Add<OverloadedSludge, Wormwood>(5);
        Add<CryoKey, FragmentOfPermafrost>(6);
        Add<Seafood, FragmentOfTide>(5);
        Add<CharredIdol, FragmentOfInferno>(5);
        // Puts Polaris on the critical path, before the Calamitas Clone.
        Add<EyeofDesolation, SoulOfPlight>(3);
        Add<AstralChunk, FragmentOfOtherworld>(6);
        // Astral Bars already need a first Astrum Deus kill, so this only
        // affects the reusable rematch summon.
        Add<Starcore, PhaseBar>(3);
        // Puts the Subspace Serpent on the critical path, before the Profaned
        // Guardians.
        Add<ProfanedShard, SanguiteBar>(4);

        Add<AerialiteBar, FragmentOfOtherworld>(1);
        Add<CryonicBar, FragmentOfPermafrost>(1);
        Add<PerennialBar, FragmentOfNature>(1);
        Add<ScoriaBar, FragmentOfInferno>(1);
        Add<AstralBar, FragmentOfChaos>(1);
        Add<LifeAlloy, AbsoluteBar>(1);
        Add<CoreofCalamity, SoulOfPlight>(1);

        Add<VictideBreastplate, FragmentOfTide>(4);
        Add<VictideGreaves, FragmentOfTide>(3);
        Add<StatigelArmor, Wormwood>(6);
        Add<StatigelGreaves, Wormwood>(5);
        Add<DaedalusBreastplate, FrigidBar>(6);
        Add<DaedalusLeggings, FrigidBar>(4);
        Add<AstralHelm, PhaseBar>(3);
        Add<AstralBreastplate, PhaseBar>(6);
        Add<AstralLeggings, PhaseBar>(4);
        Add<EmpyreanMask, SanguiteBar>(3);
        Add<EmpyreanCloak, SanguiteBar>(6);
        Add<EmpyreanCuisses, SanguiteBar>(4);

        Add<OrnateShield, FragmentOfPermafrost>(6);
        Add<AsgardsValor, AbsoluteBar>(4);
        Add<ElementalGauntlet, SanguiteBar>(6);

        return additions;

        void Add<TResult, TIngredient>(int stack) where TResult : ModItem where TIngredient : ModItem
        {
            var type = ItemType<TResult>();

            if (!additions.TryGetValue(type, out var list))
            {
                additions[type] = list = [];
            }

            list.Add(new Addition(ItemType<TIngredient>(), stack));
        }
    }
}
