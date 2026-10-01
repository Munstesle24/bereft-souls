using CalamityMod.Items.Materials;

using JetBrains.Annotations;

using SOTS.Items.Celestial;
using SOTS.Items.Permafrost;

using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BereftCompatibility.Common.Balance;

[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
internal sealed class RecipeTweaks : ModSystem
{
    public override void PostAddRecipes()
    {
        base.PostAddRecipes();

        foreach (var recipe in Main.recipe)
        {
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
        }
    }
}