using System;

using Microsoft.Xna.Framework;

namespace BereftCompatibility.Content.BossChain;

/// <summary>How a boss's first kill is gated, beyond its predecessors being down.</summary>
internal enum Gate
{
    /// <summary>Only the lock: its summon item's recipe needs the Sigil.</summary>
    Recipe,

    /// <summary>
    ///     It wakes from the world (a structure, a found item, an NPC), so
    ///     someone nearby must carry the Sigil when it spawns.
    /// </summary>
    Carry,

    /// <summary>It's fished up with a Sigil bait (see <see cref="ChainFishing"/>).</summary>
    Bait,

    /// <summary>
    ///     It's a dormant world NPC whose guards can't be hit without the Sigil
    ///     (see <see cref="ChainGuards"/>), so it is never sealed on spawn.
    /// </summary>
    Guarded,
}

/// <summary>One required boss in the Bereft boss chain.</summary>
/// <param name="Id">Internal name, also the Sigil's item name (<c>{Id}Sigil</c>).</param>
/// <param name="BossName">Name shown in Sigil and chat text.</param>
/// <param name="Tint">Colour the shared Sigil sprite is tinted to.</param>
/// <param name="Downed">Whether the boss has been beaten in this world.</param>
/// <param name="SpawnTypes">NPCs that mean this boss is starting a fight.</param>
/// <param name="LootTypes">NPCs that drop its Sigil.</param>
/// <param name="Needs">Ids of the bosses before it; their Sigils unlock it.</param>
/// <param name="NeedsAny">Whether any one of <paramref name="Needs"/> is enough.</param>
/// <param name="Gate">How its first fight is gated.</param>
/// <param name="ShopProgression">Where the Mutant lists its Sigil.</param>
/// <param name="Tier">0 pre-Hardmode to 4 endgame; sets price and rarity.</param>
/// <param name="DropCount">Sigils dropped per player per kill.</param>
/// <param name="Drop">Extra condition for multi-part bosses.</param>
/// <param name="HasSigil">False for the last boss, whose Sigil would open nothing.</param>
internal sealed record ChainLink(
    string       Id,
    string       BossName,
    Color        Tint,
    Func<bool>   Downed,
    Func<int[]>  SpawnTypes,
    Func<int[]>  LootTypes,
    string[]     Needs,
    bool         NeedsAny,
    Gate         Gate,
    float        ShopProgression,
    int          Tier,
    int          DropCount = 1,
    DropRule     Drop      = DropRule.Always,
    bool         HasSigil  = true
)
{
    public string SigilName => Id + "Sigil";
}

/// <summary>Which kill of a multi-part boss drops its Sigil.</summary>
internal enum DropRule
{
    Always,

    /// <summary>The last Eater of Worlds segment (vanilla's own rule).</summary>
    LastEaterSegment,

    /// <summary>Whichever twin dies second.</summary>
    LastTwin,

    /// <summary>Whichever of Leviathan and Anahita dies last.</summary>
    LastLeviathan,

    /// <summary>The final Astrum Deus worm.</summary>
    LastAstrumDeus,
}
