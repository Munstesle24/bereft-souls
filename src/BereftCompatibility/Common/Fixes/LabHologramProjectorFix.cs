using System;

using CalamityMod.TileEntities;
using CalamityMod.Tiles.DraedonStructures;

using JetBrains.Annotations;

using Terraria;
using Terraria.ModLoader;

namespace BereftCompatibility.Common.Fixes;

/// <summary>
///     Stops a broken Draedon lab hologram projector from making a world
///     impossible to load.
/// </summary>
/// <remarks>
///     Calamity's <see cref="TELabHologramProjector.IsTileValidForEntity"/>
///     looks up the tile's object data before checking the tile is a
///     projector, so a saved projector whose tile has since become something
///     else (a world generated while another mod's pass failed, or a mod
///     added that changes tiles) throws while the world loads.  Checking the
///     tile first lets tModLoader simply drop the orphaned entity.
/// </remarks>
[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
internal sealed class LabHologramProjectorFix : ModSystem
{
    public override void Load()
    {
        base.Load();

        MonoModHooks.Add(
            typeof(TELabHologramProjector).GetMethod(nameof(TELabHologramProjector.IsTileValidForEntity), GENERIC_FLAGS)!,
            IsTileValidForEntity_CheckTileFirst
        );
    }

    private static bool IsTileValidForEntity_CheckTileFirst(Func<TELabHologramProjector, int, int, bool> orig, TELabHologramProjector self, int x, int y)
    {
        var tile = Main.tile[x, y];
        return tile.HasTile && tile.TileType == ModContent.TileType<LabHologramProjector>() && orig(self, x, y);
    }
}
