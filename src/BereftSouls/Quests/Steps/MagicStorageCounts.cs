using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using JetBrains.Annotations;

using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace BereftSouls.Quests.Steps;

/// <summary>
///     Counts items held in Magic Storage, when it is loaded, so obtain steps
///     complete for items players have stored instead of carried.
/// </summary>
/// <remarks>
///     Magic Storage has no cross-mod call for listing contents, so this
///     reads <c>TEStorageHeart.GetStoredItems()</c> by reflection.  Counts are
///     snapshotted at most once per interval and shared by every quest.
/// </remarks>
[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
internal sealed class MagicStorageCounts : ModSystem
{
    private const int refresh_interval = 30;

    private static Type? heartType;
    private static MethodInfo? getStoredItems;

    private static readonly Dictionary<int, int> counts = [];
    private static uint lastRefresh = uint.MaxValue;

    public override void PostSetupContent()
    {
        if (!ModLoader.TryGetMod("MagicStorage", out var magicStorage))
        {
            return;
        }

        heartType      = magicStorage.Code?.GetType("MagicStorage.Components.TEStorageHeart");
        getStoredItems = heartType?.GetMethod("GetStoredItems", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);

        if (getStoredItems is null)
        {
            Mod.Logger.Warn("Magic Storage is loaded but TEStorageHeart.GetStoredItems was not found; stored items won't count toward quests.");
        }
    }

    public override void Unload()
    {
        heartType      = null;
        getStoredItems = null;
        counts.Clear();
    }

    public override void OnWorldUnload()
    {
        counts.Clear();
        lastRefresh = uint.MaxValue;
    }

    /// <summary>How many of an item are stored across every storage network in the world.</summary>
    public static int Count(int type)
    {
        if (getStoredItems is null)
        {
            return 0;
        }

        if (lastRefresh == uint.MaxValue || Main.GameUpdateCount - lastRefresh >= refresh_interval)
        {
            Refresh();
        }

        return counts.GetValueOrDefault(type);
    }

    private static void Refresh()
    {
        lastRefresh = Main.GameUpdateCount;
        counts.Clear();

        foreach (TileEntity entity in TileEntity.ByID.Values)
        {
            if (!heartType!.IsInstanceOfType(entity))
            {
                continue;
            }

            try
            {
                if (getStoredItems!.Invoke(entity, null) is not IEnumerable items)
                {
                    continue;
                }

                foreach (var obj in items)
                {
                    if (obj is Item { IsAir: false } item)
                    {
                        counts[item.type] = counts.GetValueOrDefault(item.type) + item.stack;
                    }
                }
            }
            catch (Exception e)
            {
                // A storage network mid-update shouldn't break quest checks.
                ModContent.GetInstance<BsMod>().Logger.Debug($"Couldn't read a Magic Storage network: {e.Message}");
            }
        }
    }
}
