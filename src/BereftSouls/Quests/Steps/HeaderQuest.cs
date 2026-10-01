using QuestBooks.Quests;

namespace BereftSouls.Quests.Steps;

/// <summary>
///     A section heading in a gear guide chapter.  Always complete; it only
///     carries an info page.
/// </summary>
public abstract class HeaderQuest : Quest
{
    public override QuestType QuestType => QuestType.Player;

    public override bool CheckCompletion() => true;
}
