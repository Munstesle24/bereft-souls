#!/usr/bin/env python3
"""
scan_gear.py -- extract player gear (armor, accessories, weapons, buff potions)
from the Calamity and SOTS reference sources for the Bereft Souls quest book.

Usage (from the repo root or anywhere):
    python tools/scan_gear.py [--out tools/gear_modded.json]

Requires Python 3.10+, standard library only.  The reference sources under
src/refs are READ ONLY; this script never writes there.

What it does
------------
1.  Parses every .hjson localization file of each mod (small built-in Hjson
    reader) into a flat {full.key: text} table and resolves {$Key} references.
2.  Lightly parses every .cs file of each mod (comments stripped, strings
    masked, brace matching) into classes with their base type, attributes,
    methods and numeric fields.
3.  Walks every non-abstract ModItem descendant under Items/ and classifies it
    as armor / accessory / weapon / potion (or skips it), pulling stats out of
    SetDefaults (or SOTS' SafeSetDefaults), recipes out of AddRecipes /
    Recipe.Create, and drop sources out of ModifyNPCLoot / ModifyItemLoot /
    shops / world-gen chest code across the whole mod.
4.  Applies the Bereft Souls pack's balance overrides (ItemBalance.cs) and
    recipe additions (RecipeTweaks.cs).
5.  Writes tools/gear_modded.json and prints a summary.

Tier mapping (from Item.rare)
-----------------------------
    White, Blue, Green, Orange (0-3), Gray(-1)        -> PreHardmode
    LightRed, Pink, LightPurple (4-6)                 -> EarlyHardmode
    Lime, Yellow, Cyan (7-9)                          -> PostPlantera
    Red, Purple (10-11), Calamity Turquoise, PureGreen -> PostMoonLord
    Calamity DarkBlue, CosmicPurple, BurnishedAuric, Violet, HotPink,
    CalamityRed, ExoticRainbow, DarkOrange            -> Endgame (post-DoG)
Rarities that carry no tier (Expert/Master/Quest, SOTS custom ModRarity such
as AnomalyRarity) fall back to: max tier of recipe ingredients, then the
rarity of the treasure bag / boss bag the item drops from, else PreHardmode
(counted as "tier fallback" in the summary).

Known limitations (heuristics, not a C# compiler): items whose stats are set
through unusual helper calls, recipes registered through custom helpers, and
drops added through indirect tables can be missed; those get uncertain=true
when no source at all is found.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from collections import Counter, defaultdict
from dataclasses import dataclass, field
from pathlib import Path

# --------------------------------------------------------------------------
# Paths
# --------------------------------------------------------------------------

REPO = Path(__file__).resolve().parent.parent
REFS = REPO / "src" / "refs"
BALANCE = REPO / "src" / "BereftCompatibility" / "Common" / "Balance"

MODS = {
    "CalamityMod": REFS / "CalamityMod",
    "SOTS": REFS / "SOTS",
}

# --------------------------------------------------------------------------
# Tier / rarity tables
# --------------------------------------------------------------------------

TIERS = ["PreHardmode", "EarlyHardmode", "PostPlantera", "PostMoonLord", "Endgame"]

VANILLA_RARITY_NAMES = {
    "Gray": -1, "White": 0, "Blue": 1, "Green": 2, "Orange": 3, "LightRed": 4,
    "Pink": 5, "LightPurple": 6, "Lime": 7, "Yellow": 8, "Cyan": 9, "Red": 10,
    "Purple": 11, "Expert": -12, "Master": -13, "Quest": -11,
}
RARITY_INT_TIER = {
    -1: "PreHardmode", 0: "PreHardmode", 1: "PreHardmode", 2: "PreHardmode", 3: "PreHardmode",
    4: "EarlyHardmode", 5: "EarlyHardmode", 6: "EarlyHardmode",
    7: "PostPlantera", 8: "PostPlantera", 9: "PostPlantera",
    10: "PostMoonLord", 11: "PostMoonLord",
}
MOD_RARITY_TIER = {
    "Turquoise": "PostMoonLord", "PureGreen": "PostMoonLord",
    "DarkBlue": "Endgame", "CosmicPurple": "Endgame", "BurnishedAuric": "Endgame",
    "Violet": "Endgame", "HotPink": "Endgame", "CalamityRed": "Endgame",
    "ExoticRainbow": "Endgame", "DarkOrange": "Endgame",
}
DEV_RARITIES = {"HotPink"}

# --------------------------------------------------------------------------
# Exclusions
# --------------------------------------------------------------------------

# Localization categories (Calamity "Items.X") / source dirs that never hold gear.
EXCLUDED_CATEGORY_WORDS = {
    "Debug", "Vanity", "Dyes", "Dye", "Lore", "LoreItems", "Pets", "Placeables",
    "SummonItems", "TreasureBags", "Materials", "Ammo", "Mounts", "Critters",
    "Banners", "MusicBoxes", "Furniture", "DoorItems", "LabFinders",
    "DummyTooltipItem", "PermanentBoosters", "DraedonMisc",
}
EXCLUDED_BASES = {"BaseDye", "LoreItem", "BaseBanner", "DummyTooltipItem", "ModBanner"}
JOKE_ITEMS = {"SOTS": {"SupremSticker"}, "CalamityMod": set()}

# --------------------------------------------------------------------------
# Vanilla text tables (English values of vanilla keys used by the mods)
# --------------------------------------------------------------------------

VANILLA_TEXT = {
    "CommonItemTooltip.PercentIncreasedDamage": "{0}% increased damage",
    "CommonItemTooltip.PercentIncreasedCritChance": "{0}% increased critical strike chance",
    "CommonItemTooltip.PercentIncreasedDamageCritChance": "{0}% increased damage and critical strike chance",
    "CommonItemTooltip.PercentIncreasedMeleeDamage": "{0}% increased melee damage",
    "CommonItemTooltip.PercentIncreasedRangedDamage": "{0}% increased ranged damage",
    "CommonItemTooltip.PercentIncreasedMagicDamage": "{0}% increased magic damage",
    "CommonItemTooltip.PercentIncreasedSummonDamage": "{0}% increased summon damage",
    "CommonItemTooltip.PercentIncreasedMeleeCritChance": "{0}% increased melee critical strike chance",
    "CommonItemTooltip.PercentIncreasedRangedCritChance": "{0}% increased ranged critical strike chance",
    "CommonItemTooltip.PercentIncreasedMagicCritChance": "{0}% increased magic critical strike chance",
    "CommonItemTooltip.PercentIncreasedMeleeDamageCritChance": "{0}% increased melee damage and critical strike chance",
    "CommonItemTooltip.PercentIncreasedRangedDamageCritChance": "{0}% increased ranged damage and critical strike chance",
    "CommonItemTooltip.PercentIncreasedMagicDamageCritChance": "{0}% increased magic damage and critical strike chance",
    "CommonItemTooltip.PercentIncreasedMeleeSpeed": "{0}% increased melee speed",
    "CommonItemTooltip.PercentIncreasedMovementSpeed": "{0}% increased movement speed",
    "CommonItemTooltip.IncreasesMaxMinionsBy": "Increases your max number of minions by {0}",
    "CommonItemTooltip.IncreasesMaxSentriesBy": "Increases your max number of sentries by {0}",
    "CommonItemTooltip.IncreasesMaxManaBy": "Increases maximum mana by {0}",
    "CommonItemTooltip.PercentReducedManaCost": "{0}% reduced mana cost",
    "CommonItemTooltip.PercentChanceToSaveAmmo": "{0}% chance to not consume ammo",
    "CommonItemTooltip.Sentry": "Summons a sentry",
    "CommonItemTooltip.FlightAndSlowfall": "Allows flight and slow fall",
    "CommonItemTooltip.RightClickToOpen": "Right Click to open",
    "CommonItemTooltip.MinorStats": "Minor improvements to all stats",
    "CommonItemTooltip.MediumStats": "Medium improvements to all stats",
    "CommonItemTooltip.MajorStats": "Major improvements to all stats",
    "CommonItemTooltip.PressDownToHover": "Press DOWN to toggle hover",
    "CommonItemTooltip.PressUpToBooster": "Hold UP to boost faster",
    "CommonItemTooltip.SpecialCrafting": "Used for special crafting",
    "CommonItemTooltip.MinuteDuration": "{0} minute duration",
    "CommonItemTooltip.SecondDuration": "{0} second duration",
    "CommonItemTooltip.BannerBonus": "Nearby players get a bonus against: ",
    "CommonItemTooltip.TeleportationPylon": "Teleport to another pylon",
}

# Readable names for vanilla crafting stations (TileID.X).
VANILLA_TILES = {
    "WorkBenches": "Work Bench", "Anvils": "Anvil", "MythrilAnvil": "Mythril/Orichalcum Anvil",
    "Furnaces": "Furnace", "Hellforge": "Hellforge", "AdamantiteForge": "Adamantite/Titanium Forge",
    "TinkerersWorkbench": "Tinkerer's Workshop", "LunarCraftingStation": "Ancient Manipulator",
    "DemonAltar": "Demon/Crimson Altar", "Bottles": "Placed Bottle", "AlchemyTable": "Alchemy Table",
    "Tables": "Table", "Chairs": "Chair", "Loom": "Loom", "Sawmill": "Sawmill",
    "CrystalBall": "Crystal Ball", "ImbuingStation": "Imbuing Station", "Autohammer": "Autohammer",
    "HeavyWorkBench": "Heavy Work Bench", "Solidifier": "Solidifier", "SkyMill": "Sky Mill",
    "IceMachine": "Ice Machine", "DyeVat": "Dye Vat", "Kegs": "Keg", "CookingPots": "Cooking Pot",
    "BewitchingTable": "Bewitching Table", "Blendomatic": "Blend-O-Matic", "MeatGrinder": "Meat Grinder",
    "LihzahrdFurnace": "Lihzahrd Furnace", "SteampunkBoiler": "Steampunk Boiler",
    "FleshCloningVat": "Flesh Cloning Vat", "BoneWelder": "Bone Welder", "GlassKiln": "Glass Kiln",
    "LivingLoom": "Living Loom", "Extractinator": "Extractinator", "Bookcases": "Bookcase",
    "DemonAltars": "Demon/Crimson Altar", "Campfire": "Campfire",
}

VANILLA_ITEM_FIXES = {
    "CopperShortsword": "Copper Shortsword", "Gel": "Gel", "PinkGel": "Pink Gel",
    "Ectoplasm": "Ectoplasm", "BeetleHusk": "Beetle Husk", "LunarBar": "Luminite Bar",
    "ChlorophyteBar": "Chlorophyte Bar", "HallowedBar": "Hallowed Bar",
    "SoulofLight": "Soul of Light", "SoulofNight": "Soul of Night", "SoulofFlight": "Soul of Flight",
    "SoulofMight": "Soul of Might", "SoulofSight": "Soul of Sight", "SoulofFright": "Soul of Fright",
    "FragmentSolar": "Solar Fragment", "FragmentVortex": "Vortex Fragment",
    "FragmentNebula": "Nebula Fragment", "FragmentStardust": "Stardust Fragment",
    "Bottle": "Bottle", "BottledWater": "Bottled Water", "FallenStar": "Fallen Star",
    "SpookyWood": "Spooky Wood", "Ebonwood": "Ebonwood", "Shadewood": "Shadewood",
    "DemoniteBar": "Demonite Bar", "CrimtaneBar": "Crimtane Bar", "MeteoriteBar": "Meteorite Bar",
    "ShroomiteBar": "Shroomite Bar", "SpectreBar": "Spectre Bar", "MythrilBar": "Mythril Bar",
    "TurtleShell": "Turtle Shell", "BrokenHeroSword": "Broken Hero Sword",
    "Daybloom": "Daybloom", "Blinkroot": "Blinkroot", "Moonglow": "Moonglow", "Fireblossom": "Fireblossom",
    "Waterleaf": "Waterleaf", "Deathweed": "Deathweed", "Shiverthorn": "Shiverthorn",
}


# --------------------------------------------------------------------------
# Generic text helpers
# --------------------------------------------------------------------------

def split_camel(name: str) -> str:
    """'BrokenHeroSword' -> 'Broken Hero Sword', 'SoulofLight' -> 'Soul of Light'."""
    s = re.sub(r"(?<=[a-z])of(?=[A-Z])", " of ", name)
    s = re.sub(r"(?<=[a-z0-9])(?=[A-Z])", " ", s)
    s = re.sub(r"(?<=[A-Z])(?=[A-Z][a-z])", " ", s)
    s = re.sub(r"(?<=[A-Za-z])(?=[0-9])", " ", s)
    return re.sub(r"\s+", " ", s).strip()


def vanilla_item_name(ident: str) -> str:
    if ident in VANILLA_ITEM_FIXES:
        return VANILLA_ITEM_FIXES[ident]
    m = re.fullmatch(r"(\w+?)BossBag|BossBag(\w+)", ident)
    if m:
        return f"Treasure Bag ({split_camel(m.group(1) or m.group(2))})"
    m = re.fullmatch(r"(\w+Crate)Hard", ident)
    if m:
        return f"{split_camel(m.group(1))} (Hardmode)"
    return split_camel(ident)


def slug(s: str) -> str:
    return re.sub(r"[^a-z0-9]+", "_", s.lower()).strip("_")


# --------------------------------------------------------------------------
# Hjson reader (subset sufficient for tModLoader localization files)
# --------------------------------------------------------------------------

class HjsonReader:
    def __init__(self, text: str):
        self.s = text.lstrip("﻿")
        self.i = 0
        self.n = len(self.s)

    def _skip(self, newlines: bool = True) -> None:
        s = self.s
        while self.i < self.n:
            c = s[self.i]
            if c in " \t\r" or (newlines and c == "\n"):
                self.i += 1
            elif c == "#" or s.startswith("//", self.i):
                while self.i < self.n and s[self.i] != "\n":
                    self.i += 1
            elif s.startswith("/*", self.i):
                end = s.find("*/", self.i + 2)
                self.i = self.n if end < 0 else end + 2
            else:
                break

    def parse(self) -> dict:
        self._skip()
        if self.i < self.n and self.s[self.i] == "{":
            self.i += 1
            return self._object("}")
        return self._object(None)

    def _object(self, close: str | None) -> dict:
        out: dict = {}
        while True:
            self._skip()
            if self.i >= self.n:
                return out
            c = self.s[self.i]
            if close and c == close:
                self.i += 1
                return out
            if c == ",":
                self.i += 1
                continue
            key = self._key()
            self._skip()
            if self.i < self.n and self.s[self.i] == ":":
                self.i += 1
            out[key] = self._value()

    def _key(self) -> str:
        c = self.s[self.i]
        if c in "\"'":
            return self._quoted(c)
        start = self.i
        while self.i < self.n and self.s[self.i] not in ":\n{":
            self.i += 1
        return self.s[start:self.i].strip()

    def _quoted(self, q: str) -> str:
        self.i += 1
        buf = []
        while self.i < self.n:
            c = self.s[self.i]
            if c == "\\" and self.i + 1 < self.n:
                nxt = self.s[self.i + 1]
                buf.append({"n": "\n", "t": "\t", "r": "", "\\": "\\", '"': '"', "'": "'", "/": "/"}.get(nxt, "\\" + nxt))
                self.i += 2
                continue
            if c == q:
                self.i += 1
                break
            buf.append(c)
            self.i += 1
        return "".join(buf)

    def _value(self):
        self._skip()
        if self.i >= self.n:
            return ""
        s = self.s
        c = s[self.i]
        if c == "{":
            self.i += 1
            return self._object("}")
        if c == "[":
            self.i += 1
            arr = []
            while True:
                self._skip()
                if self.i >= self.n:
                    return arr
                if s[self.i] == "]":
                    self.i += 1
                    return arr
                if s[self.i] == ",":
                    self.i += 1
                    continue
                arr.append(self._value())
        if s.startswith("'''", self.i):
            col = self.i - (s.rfind("\n", 0, self.i) + 1)
            end = s.find("'''", self.i + 3)
            end = self.n if end < 0 else end
            raw = s[self.i + 3:end]
            self.i = end + 3
            lines = raw.split("\n")
            if lines and not lines[0].strip():
                lines = lines[1:]
            if lines and not lines[-1].strip():
                lines = lines[:-1]
            cleaned = []
            for ln in lines:
                k = 0
                while k < col and k < len(ln) and ln[k] in " \t":
                    k += 1
                cleaned.append(ln[k:].rstrip("\r"))
            return "\n".join(cleaned)
        if c in "\"'":
            return self._quoted(c)
        start = self.i
        while self.i < self.n and s[self.i] != "\n":
            self.i += 1
        return s[start:self.i].strip()


def flatten(obj, prefix: str, out: dict) -> None:
    if isinstance(obj, dict):
        for k, v in obj.items():
            flatten(v, f"{prefix}.{k}" if prefix else k, out)
    elif isinstance(obj, list):
        out[prefix] = "\n".join(str(x) for x in obj)
    else:
        out[prefix] = str(obj)


def load_localization(mod_root: Path) -> dict:
    """Flat table of every en-US key -> string for one mod."""
    table: dict = {}
    for path in sorted((mod_root / "Localization").rglob("*.hjson")):
        rel = path.relative_to(mod_root / "Localization").as_posix()
        if "en-US" not in rel:
            continue
        stem = path.stem
        # File naming forms: en-US.hjson, en-US/Mods.X.Y.hjson, en-US_Mods.X.Y.hjson
        prefix = stem.split("en-US", 1)[-1].lstrip("_.") if stem.startswith("en-US") else stem
        try:
            data = HjsonReader(path.read_text(encoding="utf-8")).parse()
        except Exception as exc:  # pragma: no cover - diagnostics only
            print(f"warning: failed to parse {path}: {exc}", file=sys.stderr)
            continue
        flatten(data, prefix, table)
    return table


class Localizer:
    """Resolves {$Key} substitutions the way tModLoader does (approximately)."""

    def __init__(self, tables: dict[str, dict]):
        self.tables = tables  # mod -> flat table
        self.merged: dict = {}
        for t in tables.values():
            self.merged.update(t)
        # class name -> base key (e.g. 'Mods.CalamityMod.Items.Armor.PreHardmode.VictideHeadMelee')
        self.item_keys: dict[str, dict[str, str]] = defaultdict(dict)
        self.npc_keys: dict[str, dict[str, str]] = defaultdict(dict)
        self.buff_keys: dict[str, dict[str, str]] = defaultdict(dict)
        self.tile_keys: dict[str, dict[str, str]] = defaultdict(dict)
        for mod, t in tables.items():
            for k in t:
                parts = k.split(".")
                if len(parts) < 4 or parts[0] != "Mods" or parts[1] != mod:
                    continue
                leaf = parts[-1]
                if leaf not in ("DisplayName", "MapEntry"):
                    continue
                cls = parts[-2]
                base = ".".join(parts[:-1])
                group = parts[2]
                target = {"Items": self.item_keys, "NPCs": self.npc_keys,
                          "Buffs": self.buff_keys, "Tiles": self.tile_keys}.get(group)
                if target is not None:
                    target[mod].setdefault(cls, base)

    def get(self, key: str) -> str | None:
        return self.merged.get(key)

    def item_base(self, mod: str, cls: str) -> str | None:
        return self.item_keys[mod].get(cls)

    def item_name(self, cls: str, mod: str | None = None) -> str | None:
        mods = [mod] if mod else list(self.tables)
        for m in mods:
            base = self.item_keys[m].get(cls)
            if base and self.merged.get(base + ".DisplayName"):
                return self.resolve(self.merged[base + ".DisplayName"], base, [])
        return None

    def named(self, kind: str, cls: str, mod: str | None = None) -> str | None:
        keys = {"npc": self.npc_keys, "buff": self.buff_keys, "tile": self.tile_keys}[kind]
        mods = [mod] if mod else list(self.tables)
        for m in mods:
            base = keys[m].get(cls)
            if base:
                v = self.merged.get(base + ".DisplayName") or self.merged.get(base + ".MapEntry")
                if v:
                    return clean_text(self.resolve(v, base, []))
        return None

    def _lookup_ref(self, ref: str, context: str) -> str | None:
        if ref in self.merged:
            return self.merged[ref]
        parts = context.split(".")
        # Walk up the context hierarchy: tML resolves relative to the file prefix.
        for i in range(len(parts), 1, -1):
            cand = ".".join(parts[:i]) + "." + ref
            if cand in self.merged:
                return self.merged[cand]
        if len(parts) >= 2:
            cand = f"{parts[0]}.{parts[1]}.{ref}"
            if cand in self.merged:
                return self.merged[cand]
        return VANILLA_TEXT.get(ref)

    def resolve(self, text: str, context: str, args: list[str], depth: int = 0) -> str:
        """Expand {$Ref[@N]} and fill {n} placeholders with args (or 'X')."""
        if text is None:
            return ""

        def sub_ref(m: re.Match) -> str:
            ref, shift = m.group(1), m.group(2)
            if depth > 6:
                return ""
            val = self._lookup_ref(ref, context)
            if val is None:
                return ""
            if shift:
                k = int(shift)
                val = re.sub(r"\{(\^?)(\d+)", lambda mm: "{" + mm.group(1) + str(int(mm.group(2)) + k), val)
            return self.resolve(val, context, None, depth + 1)  # type: ignore[arg-type]

        text = re.sub(r"\{\$([A-Za-z0-9_.]+)(?:@(\d+))?\}", sub_ref, text)
        if args is None:  # nested resolution: leave placeholders for the outermost call
            return text

        def arg(i: int) -> str:
            return args[i] if i < len(args) and args[i] is not None else "X"

        # Plural forms {^0:item;items}
        def sub_plural(m: re.Match) -> str:
            opts = m.group(2).split(";")
            val = arg(int(m.group(1)))
            try:
                return opts[0] if float(val) == 1 else opts[-1]
            except ValueError:
                return opts[-1]

        text = re.sub(r"\{\^(\d+):([^}]*)\}", sub_plural, text)
        text = re.sub(r"\{(\d+)\}", lambda m: arg(int(m.group(1))), text)
        return text


def _default_tag_name(cls: str) -> str:
    return split_camel(cls)


# Replaced in main() once localization is loaded (buff / item display names).
TAG_NAME_RESOLVER = _default_tag_name


def clean_text(text: str, limit: int = 300) -> str:
    """Strip chat tags, placeholders and whitespace; cap at `limit` chars."""
    if not text:
        return ""
    t = text
    t = re.sub(r"\[(?:i|i/[^:\]]*|g|n|a):[^\]]*\]", "", t)  # item / glyph icons
    for _ in range(3):  # nested colour / effect tags: [c/FF00FF:text], [ceffect/dog:text]
        t = re.sub(r"\[\w+/[^:\]]*:([^\]]*)\]", r"\1", t)
    # Calamity reference tags such as [cbuffplr:CalamityMod/FulfilledContract] -> display name
    t = re.sub(r"\[\w+:(?:\w+/)?(\w+)\]", lambda m: TAG_NAME_RESOLVER(m.group(1)), t)
    t = re.sub(r"\[\w+:[^\]]*\]", "", t)  # any other tag
    t = t.replace("[KEY]", "the bound key")
    t = re.sub(r"\[[A-Z_]+\]\.?", "", t)  # runtime-filled tokens like [DAMAGE], [EFFECT]
    t = t.replace("<right>", "Right Click").replace("<left>", "Left Click")
    t = re.sub(r"\{\$[^}]*\}", "", t)
    t = re.sub(r"\{[^}]*\}", "X", t)
    lines = [ln.strip() for ln in t.split("\n") if ln.strip()]
    out = []
    for ln in lines:
        if out and not re.search(r"[.!?:;,]$", out[-1]):
            out[-1] += "."
        out.append(ln)
    t = re.sub(r"\s+", " ", " ".join(out)).strip()
    if len(t) > limit:
        t = t[: limit - 3].rstrip() + "..."
    return t


# --------------------------------------------------------------------------
# Lightweight C# reader
# --------------------------------------------------------------------------

def strip_csharp(src: str) -> tuple[str, str]:
    """Return (code, mask): comments blanked in both, string/char contents also
    blanked in mask.  Both are the same length as src so indices line up."""
    code = list(src)
    mask = list(src)
    i, n = 0, len(src)
    while i < n:
        c = src[i]
        if src.startswith("//", i):
            j = src.find("\n", i)
            j = n if j < 0 else j
            for k in range(i, j):
                code[k] = mask[k] = " "
            i = j
        elif src.startswith("/*", i):
            j = src.find("*/", i + 2)
            j = n if j < 0 else j + 2
            for k in range(i, j):
                if src[k] != "\n":
                    code[k] = mask[k] = " "
            i = j
        elif c == '"' or (c in "@$" and i + 1 < n and src[i + 1] in '"@$'):
            # Regular, verbatim (@"") or interpolated ($"") string.
            j = i
            verbatim = False
            while j < n and src[j] in "@$":
                verbatim = verbatim or src[j] == "@"
                j += 1
            if j >= n or src[j] != '"':
                i += 1
                continue
            j += 1
            while j < n:
                if verbatim:
                    if src[j] == '"' and j + 1 < n and src[j + 1] == '"':
                        j += 2
                        continue
                    if src[j] == '"':
                        break
                else:
                    if src[j] == "\\":
                        j += 2
                        continue
                    if src[j] == '"' or src[j] == "\n":
                        break
                j += 1
            for k in range(i + 1, min(j, n)):
                if src[k] != "\n":
                    mask[k] = " "
            i = j + 1
        elif c == "'":
            j = i + 1
            while j < n and src[j] != "'" and src[j] != "\n":
                j += 2 if src[j] == "\\" else 1
            for k in range(i + 1, min(j, n)):
                mask[k] = " "
            i = j + 1
        else:
            i += 1
    return "".join(code), "".join(mask)


def match_brace(mask: str, open_idx: int, open_ch: str = "{", close_ch: str = "}") -> int:
    depth = 0
    for k in range(open_idx, len(mask)):
        ch = mask[k]
        if ch == open_ch:
            depth += 1
        elif ch == close_ch:
            depth -= 1
            if depth == 0:
                return k
    return len(mask) - 1


def split_args(s: str) -> list[str]:
    out, depth, cur = [], 0, []
    in_str = False
    for ch in s:
        if ch == '"':
            in_str = not in_str
        if not in_str:
            if ch in "([{<":
                depth += 1
            elif ch in ")]}>":
                depth -= 1
            elif ch == "," and depth == 0:
                out.append("".join(cur).strip())
                cur = []
                continue
        cur.append(ch)
    if "".join(cur).strip():
        out.append("".join(cur).strip())
    return out


CLASS_RE = re.compile(
    r"\b((?:(?:public|internal|private|protected|abstract|sealed|static|partial|new)\s+)*)class\s+(\w+)"
    r"(?:\s*<[^>{]*>)?\s*(?::\s*([^{]+?))?\s*(?:where\s[^{]*)?\{"
)
FIELD_RE = re.compile(
    r"(?:(?:public|private|internal|protected|static|const|readonly|new)\s+)+"
    r"(?:int|float|double|long|short|byte)\s+(\w+)\s*=\s*([^;]+);"
)
METHOD_KEYWORDS = {"return", "new", "await", "else", "throw", "case", "in", "is", "as", "yield"}


@dataclass
class CsClass:
    mod: str
    name: str
    namespace: str
    base: str
    abstract: bool
    path: Path
    code: str          # class text, comments stripped
    mask: str          # class text, strings blanked
    attrs: str         # attribute text immediately before the class
    fields: dict = field(default_factory=dict)
    _methods: dict | None = None

    def methods(self) -> dict[str, list[str]]:
        """name -> list of method bodies (code text)."""
        if self._methods is not None:
            return self._methods
        res: dict[str, list[str]] = defaultdict(list)
        # Skip nested class bodies when scanning for methods.
        skip = []
        for m in CLASS_RE.finditer(self.mask, self.mask.find("{") + 1):
            ob = m.end() - 1
            skip.append((m.start(), match_brace(self.mask, ob)))
        for m in re.finditer(r"(\w[\w<>\[\],.?]*)\s+(\w+)\s*\(", self.mask):
            ret, name = m.group(1), m.group(2)
            if ret in METHOD_KEYWORDS or ret.endswith("."):
                continue
            if any(a <= m.start() <= b for a, b in skip):
                continue
            pre = self.mask[max(0, m.start() - 1):m.start()]
            if pre == ".":
                continue
            p_open = m.end() - 1
            p_close = match_brace(self.mask, p_open, "(", ")")
            k = p_close + 1
            while k < len(self.mask) and self.mask[k] in " \t\r\n":
                k += 1
            if self.mask.startswith("{", k):
                end = match_brace(self.mask, k)
                res[name].append(self.code[k + 1:end])
            elif self.mask.startswith("=>", k):
                end = self.mask.find(";", k)
                res[name].append(self.code[k + 2:end])
        self._methods = res
        return res

    def method(self, *names: str) -> str:
        ms = self.methods()
        return "\n".join(b for n in names for b in ms.get(n, []))


def parse_cs_file(mod: str, path: Path) -> list[CsClass]:
    try:
        src = path.read_text(encoding="utf-8-sig", errors="replace")
    except OSError:
        return []
    code, mask = strip_csharp(src)
    ns_matches = [(m.start(), m.group(1)) for m in re.finditer(r"\bnamespace\s+([\w.]+)", mask)]
    classes = []
    for m in CLASS_RE.finditer(mask):
        mods_kw = m.group(1) or ""
        name = m.group(2)
        bases = split_args(m.group(3) or "")
        base = ""
        if bases:
            base = re.sub(r"<.*", "", bases[0]).split(".")[-1].strip()
        ob = m.end() - 1
        cb = match_brace(mask, ob)
        ns = ""
        for pos, nm in ns_matches:
            if pos < m.start():
                ns = nm
        # attributes: text between previous statement end and class keyword
        lo = max(mask.rfind("}", 0, m.start()), mask.rfind(";", 0, m.start()), mask.rfind("{", 0, m.start()))
        attrs = code[lo + 1:m.start()]
        cls = CsClass(mod, name, ns, base, "abstract" in mods_kw, path,
                      code[m.start():cb + 1], mask[m.start():cb + 1], attrs)
        for fm in FIELD_RE.finditer(cls.mask):
            # value text from code (same indices)
            val = cls.code[fm.start(2):fm.end(2)]
            cls.fields.setdefault(fm.group(1), val.strip())
        classes.append(cls)
    return classes


# --------------------------------------------------------------------------
# Expression evaluation for numeric values (damage, format args, ...)
# --------------------------------------------------------------------------

class Evaluator:
    def __init__(self, classes_by_name: dict[str, list[CsClass]]):
        self.classes = classes_by_name

    def _field(self, ident: str, ctx: list[CsClass]) -> str | None:
        if "." in ident:
            owner, fname = ident.rsplit(".", 1)
            owner = owner.split(".")[-1]
            for c in self.classes.get(owner, []):
                if fname in c.fields:
                    return c.fields[fname]
            return None
        for c in ctx:
            if ident in c.fields:
                return c.fields[ident]
        return None

    def eval(self, expr: str, ctx: list[CsClass], depth: int = 0) -> float | None:
        if expr is None or depth > 5:
            return None
        e = expr.strip()
        e = re.sub(r"\.ToPercent\(\)", "*100", e)
        e = re.sub(r"\.FramesToSeconds\(\)", "/60", e)
        e = re.sub(r"\.ToRegenPerSecond\(\)", "/2", e)
        e = re.sub(r"\.Round\(\d*\)", "", e)
        e = re.sub(r"(?:\w+\.)?SecondsToFrames\(", "60*(", e)
        e = re.sub(r"(?:\w+\.)?MinutesToFrames\(", "3600*(", e)
        e = re.sub(r"\((?:int|float|double|long)\)", "", e)
        e = re.sub(r"(?<=\d)[fFdDmM]\b", "", e)

        def sub_ident(m: re.Match) -> str:
            ident = m.group(0)
            if re.fullmatch(r"\d+(\.\d+)?", ident):
                return ident
            val = self._field(ident, ctx)
            if val is None:
                raise KeyError(ident)
            v = self.eval(val, ctx, depth + 1)
            if v is None:
                raise KeyError(ident)
            return repr(v)

        try:
            e = re.sub(r"(?<![\d.])[A-Za-z_][\w]*(?:\.[A-Za-z_]\w*)*", sub_ident, e)
        except KeyError:
            return None
        if not re.fullmatch(r"[\d\s.+\-*/()e]+", e):
            return None
        try:
            return float(eval(e, {"__builtins__": {}}, {}))  # noqa: S307 - digits/operators only
        except Exception:
            return None

    @staticmethod
    def fmt(v: float | None) -> str | None:
        if v is None:
            return None
        if abs(v - round(v)) < 1e-6:
            return str(int(round(v)))
        return f"{v:.2f}".rstrip("0").rstrip(".")

    def format_args(self, arg_text: str, ctx: list[CsClass]) -> list[str]:
        out = []
        for a in split_args(arg_text):
            if re.search(r"Hotkey|Keybind|TooltipHotkeyString|KeyName", a):
                out.append("[key]")
                continue
            out.append(self.fmt(self.eval(a, ctx)) or "X")
        return out


# --------------------------------------------------------------------------
# Mod model
# --------------------------------------------------------------------------

@dataclass
class Recipe:
    ingredients: list  # list of (count, display)
    tiles: list        # list of display
    pack_added: list = field(default_factory=list)


@dataclass
class Source:
    kind: str     # npc | bag | shop | chest | fishing
    text: str
    ref: tuple | None = None  # (mod, class) of the source item/npc when known


class ModData:
    def __init__(self, mod: str, root: Path):
        self.mod = mod
        self.root = root
        self.classes: list[CsClass] = []
        for p in sorted(root.rglob("*.cs")):
            rel = p.relative_to(root).as_posix()
            if rel.startswith(("obj/", "bin/")):
                continue
            self.classes.extend(parse_cs_file(mod, p))
        self.by_name: dict[str, list[CsClass]] = defaultdict(list)
        for c in self.classes:
            self.by_name[c.name].append(c)

    def chain(self, cls: CsClass) -> list[CsClass]:
        """cls followed by its in-mod ancestors."""
        out, seen, cur = [cls], {cls.name}, cls
        while cur.base and cur.base not in seen:
            nxt = self.by_name.get(cur.base)
            if not nxt:
                break
            cur = nxt[0]
            seen.add(cur.name)
            out.append(cur)
        return out

    def root_base(self, cls: CsClass) -> str:
        ch = self.chain(cls)
        return ch[-1].base

    def ancestors(self, cls: CsClass) -> set[str]:
        ch = self.chain(cls)
        return {c.name for c in ch} | {ch[-1].base}


# --------------------------------------------------------------------------
# Item stat extraction
# --------------------------------------------------------------------------

ASSIGN_SPLIT = re.compile(r"(?<![=!<>+\-*/%&|^])=(?!=)")


def parse_defaults(text: str) -> dict[str, str]:
    """Collect `Item.X = value` assignments (handles chained `a = b = v`)."""
    vals: dict[str, str] = {}
    for stmt in text.split(";"):
        if "Item." not in stmt and "item." not in stmt:
            continue
        parts = ASSIGN_SPLIT.split(stmt)
        if len(parts) < 2:
            continue
        value = parts[-1].strip()
        for target in parts[:-1]:
            m = re.search(r"\b[Ii]tem\.(?:(Calamity)\(\)\.)?(\w+)\s*$", target)
            if m:
                key = ("Calamity." if m.group(1) else "") + m.group(2)
                vals[key] = value
    for m in re.finditer(r"Item\.DefaultTo(\w+)\(([^;]*)\)\s*;", text):
        vals["DefaultTo" + m.group(1)] = m.group(2)
    return vals


def rarity_info(expr: str | None) -> tuple[str | None, int | None, str | None]:
    """-> (tier or None, vanilla int or None, mod rarity name or None)."""
    if not expr:
        return None, None, None
    m = re.search(r"RarityType<(\w+)>", expr)
    if m:
        name = m.group(1)
        return MOD_RARITY_TIER.get(name), None, name
    m = re.search(r"ItemRarityID\.(\w+)", expr)
    if m:
        v = VANILLA_RARITY_NAMES.get(m.group(1))
        return RARITY_INT_TIER.get(v) if v is not None else None, v, None
    m = re.fullmatch(r"\s*(-?\d+)\s*", expr)
    if m:
        v = int(m.group(1))
        return RARITY_INT_TIER.get(v), v, None
    return None, None, None


CLASS_PATTERNS = {
    "melee": r"MeleeDamageClass|DamageClass\.Melee|TrueMelee|VoidMelee|meleeSpeed|GetAttackSpeed<Melee|MeleeNoSpeed",
    "ranged": r"RangedDamageClass|DamageClass\.Ranged|VoidRanged|ammoCost|ammoBox|ammoPotion|MeleeRangedHybrid",
    "magic": r"MagicDamageClass|DamageClass\.Magic|VoidMagic|manaCost|statManaMax|manaRegen|manaFlower|manaMagnet",
    "summon": r"SummonDamageClass|DamageClass\.Summon|VoidSummon|maxMinions|maxTurrets|SummonMeleeSpeed|whipRange",
    "rogue": r"RogueDamageClass|ThrowingDamageClass|DamageClass\.Throwing|[Ss]tealth|rogueVelocity|throwingVelocity",
}
TOOLTIP_CLASS_PATTERNS = {
    "melee": r"\bmelee\b|\btrue melee\b",
    "ranged": r"\branged\b|\bammo\b|\bbullets?\b|\barrows?\b",
    "magic": r"\bmagic\b|\bmana\b",
    "summon": r"\bsummon damage\b|\bminions?\b|\bsentr(?:y|ies)\b|\bwhips?\b|\bsummoner\b",
    "rogue": r"\brogue\b|\bstealth\b",
}
CLASS_ORDER = ["melee", "ranged", "magic", "summon", "rogue"]


def classes_from_code(text: str, tooltip: str = "") -> list[str]:
    found = {c for c, p in CLASS_PATTERNS.items() if re.search(p, text)}
    found |= {c for c, p in TOOLTIP_CLASS_PATTERNS.items() if re.search(p, tooltip, re.I)}
    if len(found) >= 4:
        return []
    return [c for c in CLASS_ORDER if c in found]


def classes_from_damage_type(expr: str | None, ancestors: set[str]) -> list[str]:
    if "RogueWeapon" in ancestors:
        return ["rogue"]
    if not expr:
        return []
    e = expr
    if re.search(r"MeleeRangedHybrid", e):
        return ["melee", "ranged"]
    if re.search(r"Rogue|Throwing", e):
        return ["rogue"]
    if re.search(r"Summon", e):  # Summon, SummonMeleeSpeed, VoidSummon
        return ["summon"]
    if re.search(r"Melee", e):
        return ["melee"]
    if re.search(r"Ranged", e):
        return ["ranged"]
    if re.search(r"Magic", e):
        return ["magic"]
    return []  # Generic / Average / AllClass / VoidGeneric


# --------------------------------------------------------------------------
# Recipes and drop sources
# --------------------------------------------------------------------------

INGR_RE = re.compile(
    r"AddIngredient<(?:\w+\.)*(?P<g>\w+)>\((?P<gc>[^)]*)\)"
    r"|AddIngredient\(\s*(?:ModContent\.)?ItemType<(?P<m>\w+)>\(\)\s*(?:,\s*(?P<mc>[^)]+))?\)"
    r"|AddIngredient\(\s*ItemID\.(?P<v>\w+)\s*(?:,\s*(?P<vc>[^)]+))?\)"
    r"|AddIngredient\(\s*(?:null|Mod|mod|this)\s*,\s*\"(?P<s>\w+)\"\s*(?:,\s*(?P<sc>[^)]+))?\)"
    r"|AddIngredient\(\s*(?P<n>\d+)\s*(?:,\s*(?P<nc>[^)]+))?\)"
    r"|AddIngredient\(\s*(?P<x>[^,)]+)\s*(?:,\s*(?P<xc>[^)]+))?\)"
    r"|AddRecipeGroup\(\s*(?P<rg>[^,)]+)\s*(?:,\s*(?P<rgc>[^)]+))?\)"
    r"|AddTile<(?:\w+\.)*(?P<tg>\w+)>\(\)"
    r"|AddTile\(\s*TileID\.(?P<tv>\w+)\s*\)"
    r"|AddTile\(\s*(?:ModContent\.)?TileType<(?:\w+\.)*(?P<tm>\w+)>\(\)\s*\)"
    r"|AddTile\(\s*(?P<tn>\d+)\s*\)"
)


class Resolver:
    """Cross-mod name resolution for items, tiles and NPCs."""

    def __init__(self, mods: dict[str, ModData], loc: Localizer):
        self.mods = mods
        self.loc = loc
        self.tile_item: dict[str, str] = {}  # tile class -> item display name
        for md in mods.values():
            for c in md.classes:
                for m in re.finditer(r"(?:createTile\s*=|DefaultToPlaceableTile\()\s*(?:ModContent\.)?TileType<(\w+)>", c.mask):
                    nm = loc.item_name(c.name, md.mod)
                    if nm:
                        self.tile_item.setdefault(m.group(1), nm)

    def item(self, cls: str) -> str:
        return self.loc.item_name(cls) or split_camel(cls)

    def tile(self, cls: str) -> str:
        return self.tile_item.get(cls) or self.loc.named("tile", cls) or split_camel(re.sub(r"Tile$", "", cls))

    def npc(self, cls: str) -> str:
        return self.loc.named("npc", cls) or split_camel(cls)


def parse_recipe_chunk(chunk: str, res: Resolver, ev: Evaluator, ctx: list[CsClass]) -> Recipe:
    ingredients, tiles = [], []

    def count(txt: str | None) -> int:
        if not txt or not txt.strip():
            return 1
        v = ev.eval(txt, ctx)
        return int(v) if v is not None else 1

    for m in INGR_RE.finditer(chunk):
        g = m.groupdict()
        if g["g"]:
            ingredients.append((count(g["gc"]), res.item(g["g"])))
        elif g["m"]:
            ingredients.append((count(g["mc"]), res.item(g["m"])))
        elif g["v"]:
            ingredients.append((count(g["vc"]), vanilla_item_name(g["v"])))
        elif g["s"]:
            ingredients.append((count(g["sc"]), res.item(g["s"])))
        elif g["n"]:
            ingredients.append((count(g["nc"]), f"item #{g['n']}"))
        elif g["x"]:
            x = g["x"].strip()
            mm = re.search(r"ItemType<(?:\w+\.)*(\w+)>|Find<ModItem>\(\"(\w+)\"\)", x)
            nm = res.item(mm.group(1) or mm.group(2)) if mm else split_camel(x.split(".")[-1])
            ingredients.append((count(g["xc"]), nm))
        elif g["rg"]:
            raw = g["rg"].strip().strip('"')
            raw = raw.split(".")[-1].split(":")[-1]
            raw = re.sub(r"^Any", "", raw)
            ingredients.append((count(g["rgc"]), "Any " + split_camel(raw)))
        elif g["tg"]:
            tiles.append(res.tile(g["tg"]))
        elif g["tv"]:
            tiles.append(VANILLA_TILES.get(g["tv"], split_camel(g["tv"])))
        elif g["tm"]:
            tiles.append(res.tile(g["tm"]))
        elif g["tn"]:
            tiles.append({"16": "Anvil", "18": "Work Bench", "17": "Furnace", "26": "Demon/Crimson Altar",
                          "134": "Mythril/Orichalcum Anvil", "412": "Ancient Manipulator",
                          "114": "Tinkerer's Workshop", "13": "Placed Bottle"}.get(g["tn"], f"tile #{g['tn']}"))
    return Recipe(ingredients, tiles)


def collect_recipes(md: ModData, res: Resolver, ev: Evaluator) -> dict[str, list[Recipe]]:
    """item class -> recipes (owning-mod recipes only)."""
    out: dict[str, list[Recipe]] = defaultdict(list)
    for c in md.classes:
        text = c.code
        if "Register" not in text:
            continue
        ctx = md.chain(c)
        for m in re.finditer(r"\bCreateRecipe\s*\(|Recipe\.Create\s*\(", text):
            if m.group(0).startswith("CreateRecipe"):
                target = c.name
            else:
                inner_end = match_brace(text, m.end() - 1, "(", ")")
                arg = text[m.end():inner_end]
                tm = re.search(r"ItemType<(\w+)>", arg)
                if tm:
                    target = tm.group(1)
                elif re.fullmatch(r"\s*Type\s*(,.*)?", arg):
                    target = c.name
                else:
                    continue
            end = text.find("Register(", m.end())
            nxt = re.search(r"\bCreateRecipe\s*\(|Recipe\.Create\s*\(", text[m.end():])
            if end < 0:
                continue
            if nxt and m.end() + nxt.start() < end:
                end = m.end() + nxt.start()
            out[target].append(parse_recipe_chunk(text[m.end():end], res, ev, ctx))
    return out


ITEM_REF_RE = re.compile(r"ItemType<(?:\w+\.)*(\w+)>|\.Add<(?:\w+\.)*(\w+)>\(|Find<ModItem>\(\"(\w+)\"\)")


def ref_name(m: re.Match) -> str:
    return m.group(1) or m.group(2) or m.group(3)


def collect_sources(md: ModData, res: Resolver) -> dict[str, list[Source]]:
    """item class -> where it comes from (drops, bags, shops, chests, fishing)."""
    out: dict[str, list[Source]] = defaultdict(list)
    weak: dict[str, list[Source]] = defaultdict(list)

    def add(item: str, src: Source) -> None:
        if item and not any(s.text == src.text for s in out[item]):
            out[item].append(src)

    def nearest(body: str, pos: int, id_class: str) -> str | None:
        """Closest preceding `<id_class>.X`, preferring `case X:` / `== X` guards."""
        found = None
        for m in re.finditer(r"(?:case\s+|==\s*)" + id_class + r"\.(\w+)", body[:pos]):
            found = m.group(1)
        if found:
            return found
        for m in re.finditer(id_class + r"\.(\w+)", body[:pos]):
            found = m.group(1)
        return found

    def nearest_npc(body: str, pos: int) -> str | None:
        """Display name of the closest preceding NPC guard (vanilla NPCID or modded NPCType<X>)."""
        found = None
        for m in re.finditer(r"(?:case\s+|==\s*)(?:NPCID\.(\w+)|(?:ModContent\.)?NPCType<(\w+)>\(\))", body[:pos]):
            found = split_camel(m.group(1)) if m.group(1) else res.npc(m.group(2))
        if found:
            return found
        vid = nearest(body, pos, "NPCID")
        return split_camel(vid) if vid else None

    for c in md.classes:
        anc = md.ancestors(c)
        is_global_npc = "GlobalNPC" in anc
        is_global_item = "GlobalItem" in anc
        rel = c.path.relative_to(md.root).as_posix()
        meths = c.methods()

        is_mod_npc = "ModNPC" in anc or (not is_global_npc and "ModifyNPCLoot" in meths)
        is_tile = bool({"ModTile", "GlobalTile"} & anc)

        def is_loot_method(name: str, body: str) -> bool:
            return (name in ("ModifyNPCLoot", "OnKill", "ModifyGlobalLoot") or "Loot" in name
                    or re.search(r"npcLoot|ItemDropRule|DropHelper|Item\.NewItem|LeadingConditionRule", body) is not None)

        # --- NPC loot (incl. helper methods such as DefineXLoot(NPCLoot)) ---
        if is_mod_npc or is_global_npc:
            for mname, bodies in meths.items():
                for body in bodies:
                    if not is_loot_method(mname, body):
                        continue
                    for m in ITEM_REF_RE.finditer(body):
                        item = ref_name(m)
                        line = body[body.rfind("\n", 0, m.start()) + 1:body.find("\n", m.end())]
                        if re.search(r"GFB|zenithWorld|getGoodWorld", line):
                            continue  # secret-seed-only drops
                        if is_global_npc:
                            who = nearest_npc(body, m.start())
                            txt = f"Dropped by {who}" if who else "Dropped by enemies (global loot)"
                            add(item, Source("npc", txt, None))
                        else:
                            add(item, Source("npc", f"Dropped by {res.npc(c.name)}", (md.mod, c.name)))
        if is_mod_npc:
            # Weaker signal: item tables declared as fields (e.g. SOTS treasure slimes).
            for m in ITEM_REF_RE.finditer(c.code):
                line = c.code[c.code.rfind("\n", 0, m.start()) + 1:c.code.find("\n", m.end())]
                if re.search(r"==|!=|Banner|HeldItem|inventory|HasItem|CountItem|GFB|zenithWorld", line):
                    continue
                weak[ref_name(m)].append(Source("npc", f"Dropped by {res.npc(c.name)}", (md.mod, c.name)))

        # --- Tiles (pots, ores, special tiles) ---
        if is_tile:
            for mname, bodies in meths.items():
                for body in bodies:
                    if not (re.search(r"Kill|Drop|RightClick", mname) or "RegisterItemDrop" in body):
                        continue
                    for m in ITEM_REF_RE.finditer(body):
                        line = body[body.rfind("\n", 0, m.start()) + 1:body.find("\n", m.end())]
                        if not re.search(r"Kill|Drop|RightClick", mname) and "RegisterItemDrop" not in line:
                            continue
                        item = ref_name(m)
                        if "GlobalTile" in anc:
                            txt = "Obtained by breaking certain tiles (pots/ores)"
                        elif res.tile(c.name) == res.item(item):
                            txt = f"Found as a naturally placed {res.item(item)} tile (break it to collect)"
                        else:
                            txt = f"Obtained by breaking {res.tile(c.name)}"
                        add(item, Source("tile", txt, None))
        # Calamity splits its global loot over helper methods in CalamityGlobalNPCLoot.cs.
        if is_global_npc and rel.endswith("Loot.cs"):
            body = c.code
            for m in ITEM_REF_RE.finditer(body):
                item = ref_name(m)
                who = nearest_npc(body, m.start())
                txt = f"Dropped by {who}" if who else "Dropped by enemies (global loot)"
                add(item, Source("npc", txt, None))

        # --- Bags / crates ---
        for body in meths.get("ModifyItemLoot", []) + meths.get("RightClick", []) + meths.get("OpenBossBag", []):
            for m in ITEM_REF_RE.finditer(body):
                item = ref_name(m)
                if is_global_item:
                    vid = nearest(body, m.start(), "ItemID")
                    if vid:
                        add(item, Source("bag", f"Found in {vanilla_item_name(vid)}", None))
                elif item != c.name:
                    add(item, Source("bag", f"Found in {res.item(c.name)}", (md.mod, c.name)))

        # --- Shops ---
        shop_bodies = []
        for mname in ("AddShops", "ModifyShop", "SetupShop", "ModifyActiveShop", "SetupTravelShop"):
            shop_bodies += [(mname, b) for b in meths.get(mname, [])]
        for mname, body in shop_bodies:
            for m in ITEM_REF_RE.finditer(body):
                item = ref_name(m)
                vid = nearest(body, m.start(), "NPCID")
                if mname == "SetupTravelShop":
                    who = "Traveling Merchant"
                elif is_global_npc:
                    who = split_camel(vid) if vid else "a town NPC"
                else:
                    who = res.npc(c.name)
                add(item, Source("shop", f"Sold by {who}", None))

        # --- Fishing ---
        for body in meths.get("CatchFish", []) + meths.get("ModifyCaughtFish", []):
            for m in ITEM_REF_RE.finditer(body):
                add(ref_name(m), Source("fishing", "Caught while fishing", None))

        # --- World generation chests ---
        if re.search(r"(^|/)(World|WorldgenHelpers|Schematics)/|World\w*\.cs$|Systems/World/", rel) \
                and not rel.startswith("Items/"):
            stem = Path(rel).stem
            place = split_camel(re.sub(r"(WorldgenHelper|Generation|Worldgen|ManagementSystem|World)$", "", stem)).strip()
            where = f"{place} chests" if place and place not in ("SOTS", "Calamity") else "world-generated chests"
            for m in ITEM_REF_RE.finditer(c.code):
                # Skip comparisons (`if (mainItem == ItemType<X>())`); everything else in
                # world-gen code is chest / structure placement.
                line_start = c.code.rfind("\n", 0, m.start()) + 1
                line_end = c.code.find("\n", m.end())
                line = c.code[line_start:line_end]
                if not re.search(r"==|!=", line):
                    add(ref_name(m), Source("chest", f"Found in {where}", None))

        # --- Calamity: Calamitas enchantment upgrades ([ItemType<A>()] = ItemType<B>()) ---
        if c.name == "EnchantmentManager":
            pat = r"\[\s*(?:ModContent\.)?ItemType<(\w+)>\(\)\s*\]\s*=\s*(?:ModContent\.)?ItemType<(\w+)>"
            for m in re.finditer(pat, c.code):
                add(m.group(2), Source("enchant", f"Enchant {res.item(m.group(1))} at Calamitas' Brimstone altar",
                                       (md.mod, m.group(1))))

        # --- SOTS: wormhole (void anomaly) conversions: new(ItemType<A>(), ItemType<B>()) ---
        for body in meths.get("InitializeWormholeRecipes", []):
            for m in re.finditer(r"new\s*\(\s*(?:ItemType<(\w+)>\(\)|ItemID\.(\w+)|\"([^\"]+)\")\s*,\s*ItemType<(\w+)>", body):
                src = res.item(m.group(1)) if m.group(1) else (
                    vanilla_item_name(m.group(2)) if m.group(2) else "Any " + split_camel(m.group(3).split(":")[-1].replace("Any", "")))
                if src != res.item(m.group(4)):
                    add(m.group(4), Source("wormhole", f"Convert {src} by throwing it into a Wormhole Anomaly",
                                           (md.mod, m.group(1)) if m.group(1) else None))

    for item, srcs in weak.items():
        if item not in out:
            for s_ in srcs:
                add(item, s_)
    return out


# --------------------------------------------------------------------------
# Pack overrides
# --------------------------------------------------------------------------

def load_pack_overrides() -> tuple[dict, dict, dict, dict]:
    """-> (weapon_stats {cls: (damage, rarityExpr|None)}, armor_def {cls: n},
           recipe_add {cls: [(count, ingredientCls)]}, recipe_remove {cls: [vanillaId]})"""
    weapon_stats, armor_def = {}, {}
    recipe_add: dict = defaultdict(list)
    recipe_remove: dict = defaultdict(list)
    ib = BALANCE / "ItemBalance.cs"
    if ib.exists():
        code, _ = strip_csharp(ib.read_text(encoding="utf-8-sig"))
        for m in re.finditer(r"weapon_stats\[ItemType<(\w+)>\(\)\]\s*=\s*new\s+WeaponStats\((\d+)\s*(?:,\s*(ItemRarityID\.\w+))?\)", code):
            weapon_stats[m.group(1)] = (int(m.group(2)), m.group(3))
        for m in re.finditer(r"armor_defense\[ItemType<(\w+)>\(\)\]\s*=\s*(\d+)", code):
            armor_def[m.group(1)] = int(m.group(2))
    rt = BALANCE / "RecipeTweaks.cs"
    if rt.exists():
        code, mask = strip_csharp(rt.read_text(encoding="utf-8-sig"))
        for m in re.finditer(r"\bAdd<(\w+),\s*(\w+)>\((\d*)\)", code):
            recipe_add[m.group(1)].append((int(m.group(3) or 1), m.group(2)))
        # if (recipe.HasResult<X>() ...) { recipe.AddIngredient<Y>(n); recipe.RemoveIngredient(ItemID.Z); }
        for m in re.finditer(r"\bif\s*\(", mask):
            pc = match_brace(mask, m.end() - 1, "(", ")")
            cond = code[m.end():pc]
            results = re.findall(r"HasResult<(\w+)>", cond)
            if not results:
                continue
            k = pc + 1
            while k < len(mask) and mask[k] in " \t\r\n":
                k += 1
            if not mask.startswith("{", k):
                continue
            body = code[k:match_brace(mask, k)]
            for r in results:
                for am in re.finditer(r"AddIngredient<(\w+)>\((\d*)\)", body):
                    recipe_add[r].append((int(am.group(2) or 1), am.group(1)))
                for rm in re.finditer(r"RemoveIngredient\(ItemID\.(\w+)\)", body):
                    recipe_remove[r].append(rm.group(1))
    return weapon_stats, armor_def, recipe_add, recipe_remove


# --------------------------------------------------------------------------
# Main extraction
# --------------------------------------------------------------------------

@dataclass
class ItemInfo:
    mod: str
    cls: CsClass
    chain: list
    d: dict
    loc_base: str | None
    name: str
    category: str | None = None
    slot: str | None = None
    rarity: str | None = None
    tier: str | None = None


def describe_recipe(r: Recipe) -> str:
    parts = [f"{n} {nm}" for n, nm in r.ingredients] + [f"{n} {nm}" for n, nm in r.pack_added]
    station = " and ".join(dict.fromkeys(r.tiles)) if r.tiles else None
    lead = f"Craft at {station}" if station else "Craft by hand"
    return f"{lead} from {', '.join(parts)}" if parts else lead


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--out", default=str(REPO / "tools" / "gear_modded.json"))
    args = ap.parse_args()

    print("Loading localization ...")
    loc = Localizer({mod: load_localization(root) for mod, root in MODS.items()})
    global TAG_NAME_RESOLVER
    TAG_NAME_RESOLVER = lambda cls: loc.named("buff", cls) or loc.item_name(cls) or split_camel(cls)  # noqa: E731
    print("Parsing C# sources ...")
    mods = {mod: ModData(mod, root) for mod, root in MODS.items()}
    res = Resolver(mods, loc)
    weapon_over, armor_over, recipe_add, recipe_remove = load_pack_overrides()

    records = []
    stats_counter = Counter()

    for mod, md in mods.items():
        ev = Evaluator(md.by_name)
        recipes = collect_recipes(md, res, ev)
        sources = collect_sources(md, res)

        # ---- pass 1: every ModItem under Items/ with parsed defaults ----
        items: dict[str, ItemInfo] = {}
        for c in md.classes:
            rel = c.path.relative_to(md.root).as_posix()
            if not rel.startswith("Items/") or c.abstract:
                continue
            ch = md.chain(c)
            if ch[-1].base != "ModItem":
                continue
            defaults_text = "\n".join(
                cc.method("SetDefaults", "SafeSetDefaults") for cc in reversed(ch))
            d = parse_defaults(defaults_text)
            items[c.name] = ItemInfo(mod, c, ch, d, loc.item_base(mod, c.name),
                                     loc.item_name(c.name, mod) or split_camel(c.name))

        for it in items.values():
            it.rarity = it.d.get("rare")
            it.tier = rarity_info(it.rarity)[0]

        # ---- pass 2: classify ----
        for it in items.values():
            c, d = it.cls, it.d
            rel = c.path.relative_to(md.root).as_posix()
            anc = md.ancestors(c)
            cat_words = set(re.split(r"[./]", (it.loc_base or "") + "/" + rel))
            if cat_words & EXCLUDED_CATEGORY_WORDS or anc & EXCLUDED_BASES:
                continue
            if c.name in JOKE_ITEMS.get(mod, ()) or re.search(r"Debug|Test", c.name) \
                    or re.search(r"\b(?:Developer|Debug|Test)\b", it.name):
                continue
            if d.get("vanity", "").strip() == "true" or d.get("dye") or "DefaultToVanitypet" in d:
                continue
            _, _, modrar = rarity_info(it.rarity)
            if modrar in DEV_RARITIES or d.get("Calamity.devItem", "").strip() == "true":
                continue
            equip = re.search(r"AutoloadEquip\(\s*EquipType\.(\w+)", c.attrs)
            equip_t = equip.group(1) if equip else None

            def num(key: str) -> float | None:
                return ev.eval(d[key], it.chain) if key in d else None

            is_acc = d.get("accessory", "").strip() == "true"
            consumable = d.get("consumable", "").strip() == "true"
            if is_acc:
                it.category = "accessory"
            elif equip_t in ("Head", "Body", "Legs"):
                has_effect = bool(c.method("UpdateEquip", "UpdateArmorSet", "IsArmorSet").strip())
                if (num("defense") or 0) > 0 or has_effect:
                    it.category = "armor"
                    it.slot = equip_t.lower()
            elif "DefaultToFood" in d:
                fargs = split_args(d["DefaultToFood"])
                if (len(fargs) >= 5 and fargs[4].strip() == "true"  # isDrink -> potion
                        and fargs[2].strip() != "0" and not re.search(r"WellFed", fargs[2])):
                    d.setdefault("buffType", fargs[2])
                    d.setdefault("buffTime", fargs[3])
                    it.category = "potion"
                else:
                    continue  # food
            elif "buffType" in d and consumable:
                buff = d["buffType"]
                if re.search(r"WellFed|Tipsy", buff) or "mountType" in d:
                    continue
                it.category = "potion"
            else:
                dmg = num("damage")
                if dmg is None and "DefaultToWhip" in d:
                    wargs = split_args(d["DefaultToWhip"])
                    dmg = ev.eval(wargs[1], it.chain) if len(wargs) > 1 else None
                    d.setdefault("DamageType", "DamageClass.SummonMeleeSpeed")
                    d.setdefault("damage", wargs[1] if len(wargs) > 1 else "0")
                if not dmg or dmg <= 0:
                    continue
                if "DamageType" not in d and "RogueWeapon" not in anc and "VoidItem" not in anc:
                    continue
                ammo = d.get("ammo", "")
                if ammo and "None" not in ammo and "useStyle" not in d:
                    continue  # ammunition
                if "createTile" in d or "DefaultToPlaceableTile" in d or "fishingPole" in d:
                    continue
                is_tool = any((num(k) or 0) > 0 for k in ("pick", "axe", "hammer"))
                if is_tool and "Weapons" not in cat_words:
                    continue
                if "Armor" in cat_words:
                    continue  # set-bonus held weapons (e.g. Wulfrum Fusion Array) are never obtained
                it.category = "weapon"

        # ---- armor sets ----
        set_defs: dict[str, list[str]] = {}  # defining class -> member classes
        for it in items.values():
            if it.category != "armor":
                continue
            body = it.cls.method("IsArmorSet")
            if not body.strip():
                continue
            found = re.findall(r"ItemType<(?:\w+\.)*(\w+)>|Find<ModItem>\(\"(\w+)\"\)", body)
            members = [a or b for a, b in found if (a or b) in items]
            if it.cls.method("UpdateArmorSet").strip():
                set_defs[it.cls.name] = [it.cls.name] + [m for m in dict.fromkeys(members) if m != it.cls.name]
        piece_sets: dict[str, list[str]] = defaultdict(list)
        for definer, members in set_defs.items():
            for m in members:
                piece_sets[m].append(definer)

        def tooltip_of(it: ItemInfo) -> str:
            if not it.loc_base:
                return ""
            raw = loc.get(it.loc_base + ".Tooltip") or ""
            argm = re.search(r"Tooltip\s*=>\s*base\.Tooltip\.WithFormatArgs\(", it.cls.code)
            fargs: list[str] = []
            if argm:
                pe = match_brace(it.cls.mask, argm.end() - 1, "(", ")")
                fargs = ev.format_args(it.cls.code[argm.end():pe], it.chain)
            return clean_text(loc.resolve(raw, it.loc_base, fargs))

        def set_bonus_of(definer: ItemInfo) -> str:
            body_code = definer.cls.method("UpdateArmorSet")
            texts = []
            # this.GetLocalization("SetBonus").Format(args) / GetLocalizedValue("SetBonus")
            for m in re.finditer(r"GetLocaliz(?:ation|edValue)\(\"(\w+)\"\)(\s*\.Format\()?", body_code):
                fargs = []
                if m.group(2):
                    s = m.end() - 1
                    fargs = ev.format_args(body_code[s + 1:match_brace(body_code, s, "(", ")")], definer.chain)
                if definer.loc_base:
                    texts.append(loc.resolve(loc.get(f"{definer.loc_base}.{m.group(1)}") or "", definer.loc_base, fargs))
            for m in re.finditer(r"GetTextValueFromModItem<(\w+)>\(\"(\w+)\"\)", body_code):
                b = loc.item_base(mod, m.group(1))
                if b:
                    texts.append(loc.resolve(loc.get(f"{b}.{m.group(2)}") or "", b, []))
            for m in re.finditer(r"GetTextValue\(\"(Mods\.[\w.]+)\"\s*(,[^;]*)?\)", body_code):
                fargs = ev.format_args(m.group(2)[1:], definer.chain) if m.group(2) else []
                texts.append(loc.resolve(loc.get(m.group(1)) or "", m.group(1), fargs))
            if not texts and definer.loc_base and loc.get(definer.loc_base + ".SetBonus"):
                texts.append(loc.resolve(loc.get(definer.loc_base + ".SetBonus"), definer.loc_base, []))
            return clean_text("\n".join(t for t in texts if t), 400)

        def common_prefix_name(names: list[str]) -> str:
            words = [n.split() for n in names]
            pre = []
            for group in zip(*words):
                if all(w == group[0] for w in group):
                    pre.append(group[0])
                else:
                    break
            return " ".join(pre)

        def set_base_name(members: list[str]) -> str:
            pre = common_prefix_name([items[m].name for m in members])
            if not pre and len(members) > 2:  # odd one out is usually the definer (alt body)
                pre = common_prefix_name([items[m].name for m in members[1:]])
            if not pre:
                cls_words = [split_camel(m) for m in members]
                pre = common_prefix_name(cls_words)
            return pre or items[members[0]].name

        # How many definers share each non-definer piece -> variant labelling.
        def set_classes(definer: str) -> list[str]:
            di = items[definer]
            text = di.cls.method("UpdateArmorSet", "UpdateEquip")
            return classes_from_code(text, set_bonus_of(di) + " " + tooltip_of(di))

        set_meta: dict[str, dict] = {}
        for definer, members in set_defs.items():
            base = set_base_name(members)
            shared = any(len(piece_sets[m]) > 1 for m in members if m != definer)
            label = ""
            dname = items[definer].name
            if shared and base != dname:
                cl = set_classes(definer)
                if cl:
                    label = "/".join(x.capitalize() for x in cl)
                elif dname.startswith(base + " "):
                    label = dname[len(base) + 1:]
                else:
                    label = dname
            sid = f"{mod.lower()}:{slug(base)}" + (f"_{slug(label)}" if label else "")
            set_meta[definer] = {
                "set_id": sid,
                "set_name": f"{base} Armor" + (f" ({label})" if label else ""),
                "set_bonus": set_bonus_of(items[definer]),
                "base_id": f"{mod.lower()}:{slug(base)}",
                "base_name": f"{base} Armor",
            }

        # ---- pass 3: build records ----
        for it in items.values():
            if not it.category:
                continue
            c, d = it.cls, it.d
            anc = md.ancestors(c)
            tooltip = tooltip_of(it)
            uncertain = False

            # stats + overrides
            rarity = it.rarity
            over = weapon_over.get(c.name)
            if over and over[1]:
                rarity = over[1]
            tier, _, _ = rarity_info(rarity)

            def num(key: str) -> float | None:
                return ev.eval(d[key], it.chain) if key in d else None

            # classes
            if it.category == "weapon":
                classes = classes_from_damage_type(d.get("DamageType"), anc)
            elif it.category == "potion":
                buff_text = ""
                bm = re.search(r"BuffType<(?:\w+\.)*(\w+)>", d.get("buffType", ""))
                buff_tip = ""
                if bm:
                    for bc in md.by_name.get(bm.group(1), []):
                        buff_text += bc.method("Update")
                    bb = loc.buff_keys[mod].get(bm.group(1))
                    if bb:
                        buff_tip = loc.get(bb + ".Description") or ""
                classes = classes_from_code(buff_text, tooltip + " " + buff_tip)
            elif it.category == "armor":
                own = c.method("UpdateEquip")
                defs = piece_sets.get(c.name, [])
                text = own
                tip = tooltip
                if c.name in set_defs:
                    text += c.method("UpdateArmorSet")
                    tip += " " + set_meta[c.name]["set_bonus"]
                elif len(defs) == 1:
                    text += items[defs[0]].cls.method("UpdateArmorSet", "UpdateEquip")
                    tip += " " + set_meta[defs[0]]["set_bonus"]
                classes = classes_from_code(text, tip)
            else:
                text = c.method("UpdateAccessory", "UpdateEquip", "UpdateInventory")
                classes = classes_from_code(text, tooltip)

            # stats string
            if it.category == "weapon":
                dmg = num("damage")
                dmg_txt = Evaluator.fmt(dmg) or d.get("damage", "?")
                if over:
                    dmg_txt = f"{over[0]}"
                cls_word = f"{classes[0]} " if len(classes) == 1 else ""
                parts = [f"{dmg_txt} {cls_word}damage"]
                if over:
                    parts[0] += f" (pack-balanced, was {Evaluator.fmt(dmg) or d.get('damage', '?')})"
                ut = num("useTime")
                if ut:
                    parts.append(f"use time {Evaluator.fmt(ut)}")
                mana = num("mana")
                if mana and "VoidItem" not in anc:
                    parts.append(f"{Evaluator.fmt(mana)} mana")
                stats = ", ".join(parts)
            elif it.category == "armor":
                dfn = num("defense") or 0
                if c.name in armor_over:
                    stats = f"{armor_over[c.name]} defense (pack-balanced, was {Evaluator.fmt(dfn)})"
                else:
                    stats = f"{Evaluator.fmt(dfn)} defense"
            elif it.category == "accessory":
                dfn = num("defense")
                stats = "Accessory" + (f", {Evaluator.fmt(dfn)} defense" if dfn else "")
            else:
                def buff_name(expr: str) -> str | None:
                    bm_ = re.search(r"BuffType<(?:\w+\.)*(\w+)>|Find<ModBuff>\(\"(\w+)\"\)", expr)
                    if bm_:
                        bcls = bm_.group(1) or bm_.group(2)
                        return loc.named("buff", bcls, mod) or split_camel(bcls)
                    vm_ = re.search(r"BuffID\.(\w+)", expr)
                    return split_camel(vm_.group(1)) if vm_ else None

                # Potions that hand out several buffs from UseItem (e.g. SOTS tonics)
                use_body = c.method("UseItem", "OnConsumeItem")
                granted = [buff_name(x.group(0)) for x in
                           re.finditer(r"BuffType<(?:\w+\.)*\w+>|BuffID\.\w+", use_body)]
                granted = list(dict.fromkeys(g for g in granted if g))
                if len(granted) >= 2:
                    stats = "Buffs: " + ", ".join(granted)
                else:
                    bname = buff_name(d.get("buffType", "")) or "buff"
                    secs = num("buffTime")
                    dur = ""
                    if secs:
                        secs /= 60
                        dur = (f", {Evaluator.fmt(round(secs / 60, 1))} min" if secs >= 60
                               else f", {Evaluator.fmt(round(secs))} s")
                    stats = f"Buff: {bname}{dur}"

            # how to obtain
            hows = []
            item_recipes = recipes.get(c.name, [])
            pack = recipe_add.get(c.name, [])
            for r in item_recipes:
                if pack:
                    r.pack_added = [(n, res.item(i)) for n, i in pack]
                if recipe_remove.get(c.name):
                    rm = {vanilla_item_name(x) for x in recipe_remove[c.name]}
                    r.ingredients = [x for x in r.ingredients if x[1] not in rm]
            if item_recipes:
                txt = describe_recipe(item_recipes[0])
                if len(item_recipes) > 1:
                    txt += f" (+{len(item_recipes) - 1} alternative recipe{'s' if len(item_recipes) > 2 else ''})"
                hows.append(txt)
            srcs = sources.get(c.name, [])
            for s in srcs[:3]:
                hows.append(s.text)
            if len(srcs) > 3:
                hows.append(f"and {len(srcs) - 3} more sources")
            if not hows:
                hows.append("Obtained in-world (source not found)")
                uncertain = True
            how = "; ".join(hows)

            # tier fallback
            tier_fallback = False
            if tier is None:
                cand = []
                for r in item_recipes:
                    for _, nm in r.ingredients:
                        for other in items.values():
                            if other.name == nm and other.tier:
                                cand.append(other.tier)
                for s in srcs:
                    if s.ref and s.ref[0] == mod:
                        if s.ref[1] in items and items[s.ref[1]].tier:
                            cand.append(items[s.ref[1]].tier)
                        elif s.kind == "npc":  # boss -> its treasure bag
                            for nc in md.by_name.get(s.ref[1], []):
                                for bag in re.findall(r"ItemType<(\w+Bag)>", nc.code):
                                    if bag in items and items[bag].tier:
                                        cand.append(items[bag].tier)
                if cand:
                    tier = max(cand, key=TIERS.index)
                else:
                    tier = "PreHardmode"
                tier_fallback = True
                stats_counter["tier_fallback"] += 1

            rec = {
                "mod": mod,
                "namespace": c.namespace,
                "class": c.name,
                "name": it.name,
                "category": it.category,
                "classes": classes,
                "tier": tier,
                "rarity": rarity or "",
                "stats": stats,
                "tooltip": tooltip,
                "how": how,
                "uncertain": uncertain,
            }
            if tier_fallback:
                rec["tier_inferred"] = True
            if item_recipes:
                # Display names of the first recipe's ingredients, used to draw
                # accessory crafting trees.
                first = item_recipes[0]
                rec["ingredients"] = [nm for _, nm in first.ingredients + first.pack_added]
            if it.category == "armor":
                defs = piece_sets.get(c.name, [])
                if c.name in set_defs:
                    meta = set_meta[c.name]
                    rec["set"] = {"set_id": meta["set_id"], "set_name": meta["set_name"],
                                  "slot": it.slot, "set_bonus": meta["set_bonus"]}
                elif len(defs) == 1:
                    meta = set_meta[defs[0]]
                    rec["set"] = {"set_id": meta["set_id"], "set_name": meta["set_name"],
                                  "slot": it.slot, "set_bonus": meta["set_bonus"]}
                elif defs:
                    meta = set_meta[defs[0]]
                    rec["set"] = {"set_id": meta["base_id"], "set_name": meta["base_name"] + " (shared piece)",
                                  "slot": it.slot, "set_bonus": "Depends on the helmet variant",
                                  "variants": sorted(set_meta[x]["set_id"] for x in defs)}
                else:
                    rec["set"] = {"set_id": f"{mod.lower()}:{slug(it.name)}", "set_name": it.name,
                                  "slot": it.slot, "set_bonus": ""}
            records.append(rec)

    records.sort(key=lambda r: (r["mod"], r["category"], TIERS.index(r["tier"]), r["name"]))
    out_path = Path(args.out)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(json.dumps(records, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    # ---- summary ----
    print(f"\nWrote {len(records)} records to {out_path}")
    by_mct = Counter((r["mod"], r["category"], r["tier"]) for r in records)
    by_cls = Counter((r["mod"], r["category"], c) for r in records for c in (r["classes"] or ["generic"]))
    cats = ["armor", "accessory", "weapon", "potion"]
    for mod in MODS:
        print(f"\n== {mod} ==")
        header = f"{'category':<10}" + "".join(f"{t:>14}" for t in TIERS) + f"{'total':>8}"
        print(header)
        for cat in cats:
            row = [by_mct[(mod, cat, t)] for t in TIERS]
            print(f"{cat:<10}" + "".join(f"{v:>14}" for v in row) + f"{sum(row):>8}")
        print(f"{'classes':<10}" + "".join(f"{c:>10}" for c in CLASS_ORDER + ['generic']))
        for cat in cats:
            print(f"{cat:<10}" + "".join(f"{by_cls[(mod, cat, c)]:>10}" for c in CLASS_ORDER + ["generic"]))
    unc = sum(r["uncertain"] for r in records)
    print(f"\nuncertain 'how': {unc} / {len(records)}")
    print(f"tier inferred (rarity carried no tier): {stats_counter['tier_fallback']}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
