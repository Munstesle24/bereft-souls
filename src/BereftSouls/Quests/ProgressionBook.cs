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

    public override void PostSetupContent()
    {
        var book = new TabBook
        {
            NameKey  = Mod.GetLocalizationKey("QuestBooks.Progression.Name"),
            Chapters =
            [
                Chapter<ScrollChapter>("PreHardmode", pre_hardmode),
                Chapter<HardmodeTierChapter>("EarlyHardmode", early_hardmode),
                Chapter<PostPlanteraChapter>("PostPlantera", post_plantera),
                Chapter<PostMoonLordChapter>("PostMoonLord", post_moon_lord),
                Chapter<EndgameTierChapter>("Endgame", endgame),
            ],
        };

        QuestBooksMod.AddGlobalQuestBooks(QUEST_LOG_KEY, [book], Mod);

        RemoveComingSoonBooks();
    }

    public override void PostAddRecipes()
    {
        base.PostAddRecipes();

        // Global books are listed in registration order, and the gear guide
        // registers first.  Put the progression book ahead of it so the list
        // reads The Basics, Bereft Progression, then the gear books.
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

    private QuestChapter Chapter<TChapter>(string name, Node[] nodes) where TChapter : BasicChapter, new()
    {
        return QuestTree.Build<TChapter>(Mod.GetLocalizationKey($"QuestBooks.Progression.{name}"), nodes, openAtStart: true);
    }
}
