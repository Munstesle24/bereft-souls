using System;
using System.Collections.Generic;

using JetBrains.Annotations;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using ReLogic.Content;

using Terraria;
using Terraria.ModLoader;

namespace BereftCompatibility.Content.BossChain;

/// <summary>
///     Builds each Sigil's sprite at load time: Fargo's Souls' Sigil of
///     Champions turned to greyscale, then tinted to the boss.  Nothing from
///     Fargo's is copied into this mod; the recolour only exists in memory.
/// </summary>
/// <remarks>
///     The same formula renders the preview in sigil-preview/: midtones take
///     the tint, the brightest pixels fade towards white so the gems still
///     catch the light.
/// </remarks>
[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
internal sealed class SigilTextures : ModSystem
{
    public const string BASE_TEXTURE = "FargowiltasSouls/Content/Items/Summons/SigilOfChampions";

    private static readonly Dictionary<string, Texture2D> textures = [];

    public static Texture2D? Get(ChainLink link) => textures.GetValueOrDefault(link.Id);

    public override void PostSetupContent()
    {
        base.PostSetupContent();

        if (Main.dedServ)
        {
            return;
        }

        Main.QueueMainThreadAction(
            () =>
            {
                var source = ModContent.Request<Texture2D>(BASE_TEXTURE, AssetRequestMode.ImmediateLoad).Value;
                var pixels = new Color[source.Width * source.Height];
                source.GetData(pixels);

                foreach (var link in BossChain.WithSigils)
                {
                    var tinted = new Color[pixels.Length];
                    for (var i = 0; i < pixels.Length; i++)
                    {
                        tinted[i] = Tint(pixels[i], link.Tint);
                    }

                    var texture = new Texture2D(Main.graphics.GraphicsDevice, source.Width, source.Height);
                    texture.SetData(tinted);
                    textures[link.Id] = texture;
                }
            }
        );
    }

    public override void Unload()
    {
        base.Unload();

        var old = new List<Texture2D>(textures.Values);
        textures.Clear();
        Main.QueueMainThreadAction(
            () =>
            {
                foreach (var texture in old)
                {
                    texture.Dispose();
                }
            }
        );
    }

    private static Color Tint(Color pixel, Color tint)
    {
        if (pixel.A == 0)
        {
            return Color.Transparent;
        }

        // Textures are premultiplied; undo that before measuring brightness.
        var alpha = pixel.A / 255f;
        var luma  = Math.Min(1f, (0.299f * pixel.R + 0.587f * pixel.G + 0.114f * pixel.B) / 255f / alpha);

        var shade     = 0.25f + 1.1f * luma;
        var highlight = Math.Max(0f, luma - 0.75f) * 2.4f;

        var color = new Vector3(Channel(tint.R), Channel(tint.G), Channel(tint.B));
        return new Color(color * alpha) { A = pixel.A };

        float Channel(byte value)
        {
            var c = value / 255f * shade;
            return Math.Min(1f, c + (1f - c) * highlight);
        }
    }
}
