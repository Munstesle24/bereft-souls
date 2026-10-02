using System.Linq;

using JetBrains.Annotations;

using QuestBooks;
using QuestBooks.QuestLog;
using QuestBooks.QuestLog.DefaultBooks;
using QuestBooks.QuestLog.DefaultChapters;
using QuestBooks.QuestLog.DefaultQuestBooks;
using QuestBooks.Systems;

using Terraria.ModLoader;

namespace BereftSouls.Quests;

/// <summary>
///     Registers the boss progression book, combining vanilla, Calamity and
///     SotS bosses.
/// </summary>
/// <remarks>
///     Each chapter is one tier and unlocks once the previous tier's gate boss
///     is down.  Material and crafting steps sit between bosses; optional
///     bosses branch off and feed nothing later on.  Node data is generated
///     into ProgressionBook.Nodes.cs by tools/generate_quests.py.  Positions
///     are a rough grid meant to be refined in the QuestBooks designer, after
///     which this can be swapped for an exported log.
/// </remarks>
[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
internal sealed partial class ProgressionBook : ModSystem
{
    public const string QUEST_LOG_KEY = "BereftSouls";

    /// <param name="Tier">The book the section belongs to, e.g. "PreHardmode".</param>
    /// <param name="Name">The section's localization name.</param>
    /// <param name="UnlockedBy">
    ///     Boss quests that must all be complete to open it; "A|B" accepts
    ///     either.
    /// </param>
    private readonly record struct Section(string Tier, string Name, Node[] Nodes, params string[] UnlockedBy);

    public override void PostSetupContent()
    {
        // One book per tier, each split into sections of about a dozen quests.
        var books = sections
                   .GroupBy(s => s.Tier)
                   .Select(
                        tier => (QuestBook)new TabBook
                        {
                            NameKey  = Mod.GetLocalizationKey($"QuestBooks.Progression.{tier.Key}"),
                            Chapters = [.. tier.Select(Chapter)],
                        }
                    )
                   .ToList();

        QuestBooksMod.AddGlobalQuestBooks(QUEST_LOG_KEY, books, Mod);

        RemoveComingSoonBooks();
    }

    public override void PostAddRecipes()
    {
        base.PostAddRecipes();

        // Global books are listed in registration order, and the gear guide
        // registers first.  Put the progression books ahead of it so the list
        // reads The Basics, the five tiers, then the gear books.
        // Rebuilt rather than re-added, since a dictionary may reuse a removed
        // entry's slot and keep the old order.
        var books   = QuestManager.GlobalQuestBooks;
        var ordered = books.OrderBy(kvp => kvp.Key == QUEST_LOG_KEY ? 0 : 1).ToList();
        books.Clear();
        foreach (var (key, value) in ordered)
        {
            books.Add(key, value);
        }
    }

    /// <summary>
    ///     QuestBooks' built-in log ships "Coming Soon" placeholder books, which
    ///     sit next to ours and lead nowhere.
    /// </summary>
    private static void RemoveComingSoonBooks()
    {
        foreach (var log in QuestManager.QuestLogs.Values)
        {
            foreach (var placeholder in log.OfType<LockedBook>().ToList())
            {
                log.Remove(placeholder);
            }
        }
    }

    private QuestChapter Chapter(Section section)
    {
        var chapter = QuestTree.Build<SectionChapter>(
            Mod.GetLocalizationKey($"QuestBooks.Progression.Sections.{section.Tier}{section.Name}"),
            section.Nodes,
            openAtStart: true
        );

        chapter.UnlockedBy = section.UnlockedBy;
        return chapter;
    }
}
