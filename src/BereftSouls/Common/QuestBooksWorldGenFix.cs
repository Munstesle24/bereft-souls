using System;
using System.Reflection;

using JetBrains.Annotations;

using QuestBooks.Quests;
using QuestBooks.Systems;

using Terraria.ModLoader;

namespace BereftSouls.Common;

/// <summary>
///     Keeps QuestBooks' quest hooks from crashing while no quest log is
///     active, such as during world generation.
/// </summary>
/// <remarks>
///     QuestBooks' tile, item and NPC hooks (chopping a tree, breaking an
///     orb...) complete quests through <see cref="QuestManager"/>, whose quest
///     table only exists inside a world.  World generation breaks tiles and
///     trees before then, so the lookup threw and the new world failed to
///     generate.  While the table is missing, lookups now find nothing and
///     completing nothing does nothing; nobody can earn a quest then anyway.
/// </remarks>
[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
internal sealed class QuestBooksWorldGenFix : ModSystem
{
    private const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;

    public override void Load()
    {
        base.Load();

        MonoModHooks.Add(Method("GetQuest", typeof(string)), GetQuest_NoneWithoutQuestLog);

        foreach (var name in new[] { "CompleteQuest", "MarkComplete", "MarkIncomplete" })
        {
            MonoModHooks.Add(Method(name, typeof(Quest)), SkipMissingQuest);
        }
    }

    private static MethodInfo Method(string name, Type parameter)
    {
        return typeof(QuestManager).GetMethod(name, flags, [parameter])
            ?? throw new MissingMethodException(nameof(QuestManager), name);
    }

    private static Quest? GetQuest_NoneWithoutQuestLog(Func<string, Quest> orig, string questName)
    {
        return QuestManager.ActiveQuests is null ? null : orig(questName);
    }

    private static void SkipMissingQuest(Action<Quest> orig, Quest? quest)
    {
        if (quest is null)
        {
            return;
        }

        orig(quest);
    }
}
