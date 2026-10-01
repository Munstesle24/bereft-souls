using System.Collections.Generic;

using JetBrains.Annotations;

using QuestBooks;
using QuestBooks.QuestLog;
using QuestBooks.QuestLog.DefaultChapters;
using QuestBooks.QuestLog.DefaultQuestBooks;

using Terraria.ModLoader;

namespace BereftSouls.Quests;

/// <summary>
///     Registers the gear guide books: one per class plus a general book,
///     each with a chapter per tier listing that tier's armor sets,
///     accessories, weapons and potions.
/// </summary>
/// <remarks>
///     Entries complete when a player on the server owns or wears the item,
///     and give no rewards; they exist so players don't need a wiki.  Node
///     data is generated into GearGuide.Nodes.cs by tools/generate_gear.py.
/// </remarks>
[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
internal sealed partial class GearGuide : ModSystem
{
    public const string QUEST_LOG_KEY = "BereftSoulsGear";

    private readonly record struct GearBook(
        string Name,
        Node[] PreHardmode,
        Node[] EarlyHardmode,
        Node[] PostPlantera,
        Node[] PostMoonLord,
        Node[] Endgame
    );

    public override void PostSetupContent()
    {
        var books = new List<QuestBook>();

        foreach (var gear in gear_books)
        {
            var chapters = new List<QuestChapter>();

            Add<ScrollChapter>("PreHardmode", gear.PreHardmode);
            Add<HardmodeTierChapter>("EarlyHardmode", gear.EarlyHardmode);
            Add<PostPlanteraChapter>("PostPlantera", gear.PostPlantera);
            Add<PostMoonLordChapter>("PostMoonLord", gear.PostMoonLord);
            Add<EndgameTierChapter>("Endgame", gear.Endgame);

            books.Add(
                new TabBook
                {
                    NameKey  = Mod.GetLocalizationKey($"QuestBooks.Gear.{gear.Name}"),
                    Chapters = chapters,
                }
            );

            continue;

            void Add<TChapter>(string tier, Node[] nodes) where TChapter : BasicChapter, new()
            {
                if (nodes.Length > 0)
                {
                    chapters.Add(QuestTree.Build<TChapter>(Mod.GetLocalizationKey($"QuestBooks.Progression.{tier}"), nodes));
                }
            }
        }

        QuestBooksMod.AddGlobalQuestBooks(QUEST_LOG_KEY, books, Mod);
    }
}
