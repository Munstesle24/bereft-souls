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
