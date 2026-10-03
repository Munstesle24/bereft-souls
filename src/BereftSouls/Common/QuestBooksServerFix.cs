using System.Reflection;

using JetBrains.Annotations;

using QuestBooks;
using QuestBooks.Systems;

using Terraria;
using Terraria.ModLoader;

namespace BereftSouls.Common;

/// <summary>
///     Lets a dedicated server check world quests.
/// </summary>
/// <remarks>
///     QuestBooks only checks quests once <c>QuestLoader.QuestsLoaded</c> is
///     set, which happens when a player enters the world on their own machine.
///     A dedicated server (including Host &amp; Play) never does that, and
///     clients are not allowed to check world quests in multiplayer, so no
///     world quest (every Bereft step) ever completed.  Once the server has a
///     world loaded, mark its quests as loaded too.
/// </remarks>
[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
internal sealed class QuestBooksServerFix : ModSystem
{
    private static PropertyInfo? questsLoaded;

    public override void PostSetupContent()
    {
        base.PostSetupContent();

        questsLoaded = typeof(QuestBooksMod).Assembly
                                            .GetType("QuestBooks.Systems.QuestLoader")
                                           ?.GetProperty("QuestsLoaded", BindingFlags.Public | BindingFlags.Static);
        if (questsLoaded?.GetSetMethod(true) is null)
        {
            questsLoaded = null;
        }

        if (questsLoaded is null)
        {
            Mod.Logger.Warn("QuestBooks.Systems.QuestLoader.QuestsLoaded was not found; world quests won't complete on dedicated servers.");
        }
    }

    public override void Unload()
    {
        questsLoaded = null;
    }

    public override void PreUpdateWorld()
    {
        // Runs on the server and in singleplayer; singleplayer already sets it.
        if (!Main.dedServ || questsLoaded is null || QuestManager.ActiveQuests is null)
        {
            return;
        }

        if (questsLoaded.GetValue(null) is false)
        {
            questsLoaded.SetValue(null, true);
        }
    }
}
