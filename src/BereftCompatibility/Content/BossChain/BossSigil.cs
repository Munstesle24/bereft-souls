using System.Linq;

using JetBrains.Annotations;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace BereftCompatibility.Content.BossChain;

/// <summary>
///     The Sigil a chain boss drops.  One class, loaded once per boss by
///     <see cref="SigilLoader"/>, sharing Fargo's Souls' Sigil of Champions
///     sprite recoloured to the boss (see <see cref="SigilTextures"/>).
/// </summary>
[Autoload(false)]
internal sealed class BossSigil(ChainLink link) : ModItem
{
    public ChainLink Link { get; } = link;

    public override string Name => Link.SigilName;

    public override string Texture => SigilTextures.BASE_TEXTURE;

    protected override bool CloneNewInstances => true;

    public override LocalizedText DisplayName =>
        Language.GetText("Mods.BereftCompatibility.BossChain.SigilName").WithFormatArgs(Link.BossName);

    public override LocalizedText Tooltip
    {
        get
        {
            var unlocks = BossChain.Unlocks(Link).Select(l => l.BossName).ToArray();
            if (unlocks.Length == 0)
            {
                return Language.GetText("Mods.BereftCompatibility.BossChain.SigilTooltipLast").WithFormatArgs(Link.BossName);
            }

            return Language.GetText("Mods.BereftCompatibility.BossChain.SigilTooltip")
                           .WithFormatArgs(Link.BossName, string.Join(", ", unlocks));
        }
    }

    public override void SetStaticDefaults()
    {
        base.SetStaticDefaults();

        Item.ResearchUnlockCount = 3;
    }

    public override void SetDefaults()
    {
        base.SetDefaults();

        Item.width    = 30;
        Item.height   = 30;
        Item.maxStack = Item.CommonMaxStack;
        Item.rare = Link.Tier switch
        {
            0 => ItemRarityID.Orange,
            1 => ItemRarityID.Pink,
            2 => ItemRarityID.Lime,
            3 => ItemRarityID.Purple,
            _ => ItemRarityID.Red,
        };
        Item.value = SigilShop.Price(Link) / 5;
    }

    public override bool PreDrawInInventory(
        SpriteBatch spriteBatch,
        Vector2     position,
        Rectangle   frame,
        Color       drawColor,
        Color       itemColor,
        Vector2     origin,
        float       scale
    )
    {
        if (SigilTextures.Get(Link) is not { } texture)
        {
            return true;
        }

        spriteBatch.Draw(texture, position, frame, drawColor, 0f, origin, scale, SpriteEffects.None, 0f);
        return false;
    }

    public override bool PreDrawInWorld(
        SpriteBatch spriteBatch,
        Color       lightColor,
        Color       alphaColor,
        ref float   rotation,
        ref float   scale,
        int         whoAmI
    )
    {
        if (SigilTextures.Get(Link) is not { } texture)
        {
            return true;
        }

        var position = Item.Bottom - Main.screenPosition - new Vector2(0f, texture.Height * scale * 0.5f);
        spriteBatch.Draw(texture, position, null, lightColor, rotation, texture.Size() * 0.5f, scale, SpriteEffects.None, 0f);
        return false;
    }
}

/// <summary>Registers one <see cref="BossSigil"/> per chain boss.</summary>
[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
internal sealed class SigilLoader : ModSystem
{
    public override void Load()
    {
        base.Load();

        foreach (var link in BossChain.WithSigils)
        {
            Mod.AddContent(new BossSigil(link));
        }
    }

    public static int Type(string id) => ModContent.Find<ModItem>("BereftCompatibility", id + "Sigil").Type;

    public static int Type(ChainLink link) => Type(link.Id);
}
