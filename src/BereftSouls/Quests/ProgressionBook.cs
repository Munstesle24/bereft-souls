using JetBrains.Annotations;

using QuestBooks;
using QuestBooks.QuestLog;
using QuestBooks.QuestLog.DefaultChapters;
using QuestBooks.QuestLog.DefaultQuestBooks;

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
    }

    private QuestChapter Chapter<TChapter>(string name, Node[] nodes) where TChapter : BasicChapter, new()
    {
        return QuestTree.Build<TChapter>(Mod.GetLocalizationKey($"QuestBooks.Progression.{name}"), nodes);
    }
}
