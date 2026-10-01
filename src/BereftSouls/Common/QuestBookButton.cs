using System.Collections.Generic;

using JetBrains.Annotations;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using QuestBooks.Systems;

using ReLogic.Content;

using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace BereftSouls.Common;

/// <summary>
///     Draws a quest book button beside the inventory that opens the
///     QuestBooks log, so players don't need to know its keybind.
/// </summary>
[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
internal sealed class QuestBookButton : ModSystem
{
    // Just right of the coin and ammo columns, level with the second
    // inventory row.
    private static readonly Vector2 default_position = new(580f, 100f);

    private static Asset<Texture2D>? icon;
    private static Asset<Texture2D>? outline;

    public override void Load()
    {
        if (Main.dedServ)
        {
            return;
        }

        icon    = ModContent.Request<Texture2D>("QuestBooks/Assets/Textures/QuestLog/QuestBookIcon");
        outline = ModContent.Request<Texture2D>("QuestBooks/Assets/Textures/QuestLog/QuestBookOutline");
    }

    public override void Unload()
    {
        icon    = null;
        outline = null;
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        var index = layers.FindIndex(layer => layer.Name == "Vanilla: Inventory");
        if (index < 0)
        {
            return;
        }

        layers.Insert(
            index + 1,
            new LegacyGameInterfaceLayer(
                "BereftSouls: Quest Book Button",
                () =>
                {
                    Draw(Main.spriteBatch);
                    return true;
                },
                InterfaceScaleType.UI
            )
        );
    }

    private static void Draw(SpriteBatch spriteBatch)
    {
        var config = ModContent.GetInstance<BsConfig>();

        if (!Main.playerInventory || !config.ShowQuestBookButton || icon is null || outline is null)
        {
            return;
        }

        var texture  = icon.Value;
        var position = default_position + new Vector2(config.QuestBookButtonOffsetX, config.QuestBookButtonOffsetY);
        var bounds   = new Rectangle((int)position.X, (int)position.Y, texture.Width, texture.Height);
        var hovered  = bounds.Contains(Main.MouseScreen.ToPoint());

        if (hovered)
        {
            var edge = outline.Value;
            spriteBatch.Draw(edge, position - (edge.Size() - texture.Size()) / 2f, Main.OurFavoriteColor);

            Main.LocalPlayer.mouseInterface = true;
            Main.hoverItemName              = ModContent.GetInstance<BsMod>().GetLocalization("Common.QuestBookButton").Value;

            if (Main.mouseLeft && Main.mouseLeftRelease)
            {
                // Close the inventory so the book isn't drawn over it; opening
                // the inventory again closes the book, as usual.
                Main.playerInventory = false;
                QuestLogDrawer.Toggle(true);
                SoundEngine.PlaySound(SoundID.MenuOpen);
            }
        }

        spriteBatch.Draw(texture, position, Color.White);
    }
}
