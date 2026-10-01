using CalamityMod.Items.SummonItems;

using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

using static Terraria.ModLoader.ModContent;

namespace BereftCompatibility.Content.BossChain;

/// <summary>
///     Summons the Wall of Flesh when thrown into Underworld lava.  Until the
///     Wall has been beaten, this is the only way to summon it (see
///     <see cref="ChainGuards"/>).
/// </summary>
internal sealed class FleshboundEffigy : ModItem
{
    /// <summary>True while this item is spawning the Wall of Flesh.</summary>
    public static bool Summoning { get; private set; }

    public override string Texture => $"Terraria/Images/Item_{ItemID.GuideVoodooDoll}";

    public override void SetStaticDefaults()
    {
        base.SetStaticDefaults();

        // Its rarity already keeps lava from destroying it.
        Item.ResearchUnlockCount = 1;
    }

    public override void SetDefaults()
    {
        base.SetDefaults();

        Item.width    = 28;
        Item.height   = 28;
        Item.maxStack = 20;
        Item.rare     = ItemRarityID.Orange;
        Item.value    = Item.sellPrice(gold: 2);
    }

    public override void Update(ref float gravity, ref float maxFallSpeed)
    {
        base.Update(ref gravity, ref maxFallSpeed);

        if (!Item.lavaWet || Main.netMode == NetmodeID.MultiplayerClient)
        {
            return;
        }

        if (Item.position.Y / 16f < Main.UnderworldLayer || NPC.AnyNPCs(NPCID.WallofFlesh))
        {
            return;
        }

        Summoning = true;
        try
        {
            NPC.SpawnWOF(Item.position);
        }
        finally
        {
            Summoning = false;
        }

        if (!NPC.AnyNPCs(NPCID.WallofFlesh))
        {
            return;
        }

        Item.stack--;
        if (Item.stack <= 0)
        {
            Item.TurnToAir();
        }

        if (Main.netMode == NetmodeID.Server)
        {
            NetMessage.SendData(MessageID.SyncItem, number: Item.whoAmI);
        }
    }

    public override void AddRecipes()
    {
        base.AddRecipes();

        CreateRecipe()
           .AddIngredient(ItemID.GuideVoodooDoll)
           .AddIngredient(SigilLoader.Type("EyeOfCthulhu"))
           .AddIngredient(SigilLoader.Type("WorldEvil"))
           .AddIngredient(SigilLoader.Type("Skeletron"))
           .AddIngredient(SigilLoader.Type("Advisor"))
           .AddTile(TileID.DemonAltar)
           .Register();
    }
}

/// <summary>The only bait Duke Fishron bites on before his first defeat.</summary>
internal sealed class SigilTruffleWorm : ModItem
{
    public override string Texture => $"Terraria/Images/Item_{ItemID.TruffleWorm}";

    public override void SetStaticDefaults()
    {
        base.SetStaticDefaults();

        Item.ResearchUnlockCount = 3;
    }

    public override void SetDefaults()
    {
        base.SetDefaults();

        Item.width    = 20;
        Item.height   = 20;
        Item.maxStack = Item.CommonMaxStack;
        Item.bait     = 50;
        Item.rare     = ItemRarityID.Lime;
        Item.value    = Item.sellPrice(gold: 5);
    }

    public override bool? CanConsumeBait(Player player) => true;

    public override void AddRecipes()
    {
        base.AddRecipes();

        CreateRecipe()
           .AddIngredient(ItemID.TruffleWorm)
           .AddIngredient(SigilLoader.Type("Plaguebringer"))
           .Register();
    }
}

/// <summary>The only bait the Old Duke rises for before his first defeat.</summary>
internal sealed class SigilBloodworm : ModItem
{
    public override string Texture => "CalamityMod/Items/SummonItems/BloodwormItem";

    public override void SetStaticDefaults()
    {
        base.SetStaticDefaults();

        Item.ResearchUnlockCount = 3;
    }

    public override void SetDefaults()
    {
        base.SetDefaults();

        Item.width    = 20;
        Item.height   = 20;
        Item.maxStack = Item.CommonMaxStack;
        Item.bait     = 50;
        Item.rare     = ItemRarityID.Purple;
        Item.value    = Item.sellPrice(gold: 20);
    }

    public override bool? CanConsumeBait(Player player) => true;

    public override void AddRecipes()
    {
        base.AddRecipes();

        CreateRecipe()
           .AddIngredient(ItemType<BloodwormItem>())
           .AddIngredient(SigilLoader.Type("Polterghast"))
           .Register();
    }
}
