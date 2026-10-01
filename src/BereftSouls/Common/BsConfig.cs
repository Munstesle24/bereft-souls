using System.ComponentModel;

using JetBrains.Annotations;

using QuestBooks;

using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace BereftSouls.Common;

[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
public sealed class BsConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;

    /// <summary>
    ///     Turns on the QuestBooks in-game designer so the book layout can be
    ///     rearranged and exported while testing.  Not for players.
    /// </summary>
    [DefaultValue(false)]
    [ReloadRequired]
    public bool EnableQuestDesigner { get; set; }

    /// <summary>Shows the quest book button beside the inventory.</summary>
    [DefaultValue(true)]
    public bool ShowQuestBookButton { get; set; }

    /// <summary>Moves the button, in pixels, if it overlaps another mod's UI.</summary>
    [Range(-600, 600)]
    [DefaultValue(0)]
    public int QuestBookButtonOffsetX { get; set; }

    [Range(-400, 400)]
    [DefaultValue(0)]
    public int QuestBookButtonOffsetY { get; set; }
}

[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
internal sealed class QuestDesignerSystem : ModSystem
{
    public override void PostSetupContent()
    {
        if (ModContent.GetInstance<BsConfig>().EnableQuestDesigner)
        {
            QuestBooksMod.EnableDesigner(Mod);
        }
    }
}
