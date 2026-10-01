using System.Reflection;

using JetBrains.Annotations;

using QuestBooks.Quests.QuestSystems;

using Terraria;
using Terraria.ModLoader;

namespace BereftSouls.Common;

/// <summary>
///     Stops QuestBooks' tile-breaking quests from running during world
///     generation.
/// </summary>
/// <remarks>
///     World generation kills tiles (e.g. the "Smooth World" pass) before any
///     quest log is active, so <see cref="KillTileHook"/>'s completion callback
///     hits a null quest table and the new world fails to generate.  No player
///     can complete a quest then anyway.
/// </remarks>
[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
internal sealed class QuestBooksWorldGenFix : ModSystem
{
    private delegate void KillTileOrig(KillTileHook self, int i, int j, int type, ref bool fail, ref bool effectOnly, ref bool noItem);

    private delegate void KillTileDetour(KillTileOrig orig, KillTileHook self, int i, int j, int type, ref bool fail, ref bool effectOnly, ref bool noItem);

    public override void Load()
    {
        base.Load();

        var byRefBool = typeof(bool).MakeByRefType();
        var method = typeof(KillTileHook).GetMethod(
            nameof(KillTileHook.KillTile),
            BindingFlags.Public | BindingFlags.Instance,
            [typeof(int), typeof(int), typeof(int), byRefBool, byRefBool, byRefBool]
        );

        MonoModHooks.Add(method!, (KillTileDetour)SkipDuringWorldGen);
    }

    private static void SkipDuringWorldGen(KillTileOrig orig, KillTileHook self, int i, int j, int type, ref bool fail, ref bool effectOnly, ref bool noItem)
    {
        if (WorldGen.generatingWorld)
        {
            return;
        }

        orig(self, i, j, type, ref fail, ref effectOnly, ref noItem);
    }
}
