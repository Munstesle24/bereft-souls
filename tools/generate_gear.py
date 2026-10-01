"""Generates the BereftSouls gear guide books.

Reads tools/gear_modded.json (from tools/scan_gear.py) and, if present,
tools/gear_vanilla.json, and writes:
  src/BereftSouls/Quests/GearGuide.Nodes.cs            - book/chapter layout
  src/BereftSouls/Quests/Steps/GearQuests.cs           - one quest per entry
  src/BereftSouls/Localization/en-US_Mods.BereftSouls.QuestBooks.hjson

Run from the repository root after generate_quests.py:
  python tools/generate_gear.py
"""

import json
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from generate_quests import CHAPTERS, MOD, TOOLS, fail, hjson_string, item_expr  # noqa: E402

BOOKS = [
    # (name, class filter, display name)
    ("Melee", "melee", "Melee Gear"),
    ("Ranged", "ranged", "Ranged Gear"),
    ("Magic", "magic", "Magic Gear"),
    ("Summoner", "summon", "Summoner Gear"),
    ("Rogue", "rogue", "Rogue Gear"),
    ("General", None, "General Gear"),
]

SECTIONS = [
    # (category, heading, blurb)
    ("armor", "Armor Sets", "Full sets for this class at this tier. Wear every piece to complete one."),
    ("accessory", "Accessories", "Trinkets worth slotting at this tier. Equip one to complete it."),
    ("weapon", "Weapons", "Notable weapons at this tier. Own one to complete it."),
    ("potion", "Potions and Flasks", "Buffs worth brewing for this tier. Own one to complete it."),
]

TAG = {
    "armor": "[c/E0B14A:Armor set]",
    "accessory": "[c/6CC4E8:Accessory]",
    "weapon": "[c/E2735C:Weapon]",
    "potion": "[c/79BD84:Potion]",
}

PER_ROW = 8
UNCERTAIN_NOTE = " [c/9AA0A6:(source unconfirmed)]"


def sanitize(text):
    return re.sub(r"[^A-Za-z0-9]", "", text)


def entry_key(record):
    if record.get("vanilla_id"):
        return "Gear" + record["vanilla_id"]
    return "Gear" + record["class"]


def load_records():
    records = []
    for name in ("gear_modded.json", "gear_vanilla.json"):
        path = TOOLS / name
        if path.exists():
            records += json.loads(path.read_text(encoding="utf-8"))
        else:
            print(f"warning: {name} not found, skipping")
    return records


def build_entries(records):
    """Turns item records into guide entries, merging armor pieces into sets."""
    entries = {}
    sets = {}

    for r in records:
        if r["tier"] not in {name for name, _, _ in CHAPTERS}:
            fail(f"{r.get('name')}: unknown tier {r['tier']}")

        if r["category"] == "armor":
            info = r.get("set")
            if not info:
                continue
            # Calamity shares body and legs between helmet variants; the
            # scanner lists the variant sets they belong to.
            for set_id in info.get("variants") or [info["set_id"]]:
                sets.setdefault(set_id, []).append(r)
            continue

        key = entry_key(r)
        if key in entries:
            continue
        entries[key] = {
            "key": key,
            "category": r["category"],
            "classes": r.get("classes", []),
            "tier": r["tier"],
            "title": r["name"],
            "type": "equip" if r["category"] == "accessory" else "obtain",
            "item": r,
            "pieces": None,
            "stats": r.get("stats", ""),
            "tooltip": r.get("tooltip", ""),
            "how": r.get("how", ""),
            "uncertain": r.get("uncertain", False),
            "order": r.get("rarity", ""),
        }

    slot_order = {"head": 0, "body": 1, "legs": 2}
    for set_id, pieces in sets.items():
        pieces.sort(key=lambda p: slot_order.get(p["set"]["slot"], 3))
        first = pieces[0]
        # A set is identified by its helmet; skip leftover shared pieces.
        if first["set"]["slot"] != "head" or len(pieces) < 2:
            continue
        key = "GearSet" + sanitize(set_id)
        how = "; ".join(f"{p['name']}: {p.get('how', '')}" for p in pieces)
        defense = [re.search(r"(\d+) defense", p.get("stats", "")) for p in pieces]
        if all(defense):
            values = [int(m.group(1)) for m in defense]
            stats = f"{sum(values)} defense ({' / '.join(map(str, values))})"
        else:
            stats = ", ".join(p.get("stats", "") for p in pieces if p.get("stats"))
        # The helmet decides a class variant; shared pieces list every class.
        classes = sorted(set(first.get("classes", [])) or {c for p in pieces for c in p.get("classes", [])})
        entries[key] = {
            "key": key,
            "category": "armor",
            "classes": classes,
            "tier": max((p["tier"] for p in pieces), key=[c[0] for c in CHAPTERS].index),
            "title": first["set"]["set_name"],
            "type": "equip_set",
            "item": None,
            "pieces": pieces,
            "stats": stats,
            "tooltip": first["set"].get("set_bonus", ""),
            "how": how,
            "uncertain": any(p.get("uncertain") for p in pieces),
            "order": first.get("rarity", ""),
        }

    return list(entries.values())


def main():
    records = load_records()
    if not records:
        fail("no gear records")

    entries = build_entries(records)
    step_keys = set(re.findall(r"public sealed class (\w+) :", (MOD / "Quests" / "Steps" / "StepQuests.cs").read_text(encoding="utf-8")))
    clashes = [e["key"] for e in entries if e["key"] in step_keys]
    if clashes:
        fail(f"gear keys clash with step quests: {clashes[:5]}")

    book_fields = []
    headers = []
    localization = []

    for book, class_filter, display in BOOKS:
        chapter_fields = []

        for tier, _, tier_name in CHAPTERS:
            nodes = []
            row = 0

            for category, heading, blurb in SECTIONS:
                items = [
                    e for e in entries
                    if e["tier"] == tier and e["category"] == category
                    and (class_filter in e["classes"] if class_filter else not e["classes"])
                ]
                if not items:
                    continue

                items.sort(key=lambda e: (e["order"], e["title"]))
                header = f"GearHeader{book}{tier}{category.capitalize()}"
                headers.append(header)
                localization.append(
                    f"\t{header}: {{\n"
                    f"\t\tTitle: {hjson_string(f'{display}: {heading}')}\n"
                    f"\t\tContents: {hjson_string(f'{tier_name}. {blurb}')}\n"
                    f"\t\tTooltip: {hjson_string(heading)}\n"
                    "\t}\n"
                )
                nodes.append(f'            new("{header}", 0, {row}, NodeKind.Header, false),')

                for i, e in enumerate(items):
                    nodes.append(
                        f'            new("{e["key"]}", {1 + i % PER_ROW}, {row + i // PER_ROW}, NodeKind.Gear, false),'
                    )
                row += (len(items) - 1) // PER_ROW + 2

            chapter_fields.append("            [\n" + "\n".join(nodes) + ("\n" if nodes else "") + "            ]")

        book_fields.append(
            f'        new("{book}",\n' + ",\n".join(chapter_fields) + "\n        ),"
        )

    # Quest classes and text for every entry that appears in some book.
    used = set()
    for field in book_fields:
        used.update(re.findall(r'new\("(Gear(?!Header)\w+)"', field))

    classes = []
    for e in entries:
        if e["key"] not in used:
            continue

        if e["type"] == "equip_set":
            pieces = ", ".join(item_expr(p) for p in e["pieces"])
            body = f"    protected override int[] Pieces => [{pieces}];"
            base = "EquipSetQuest"
            goal = f"Wear the full {e['title']}"
        elif e["type"] == "equip":
            body = f"    protected override int ItemType => {item_expr(e['item'])};"
            base = "EquipItemQuest"
            goal = f"Equip {e['title']}"
        else:
            body = f"    protected override int ItemType => {item_expr(e['item'])};"
            base = "ObtainItemQuest"
            goal = f"Obtain {e['title']}"

        classes.append(f"public sealed class {e['key']} : {base}\n{{\n{body}\n}}\n")

        tag = TAG[e["category"]]
        tooltip_text = e["tooltip"].strip()
        if tooltip_text and tooltip_text[-1] not in ".!?":
            tooltip_text += "."
        detail = " ".join(part for part in (e["stats"] + "." if e["stats"] else "", tooltip_text) if part)
        note = UNCERTAIN_NOTE if e["uncertain"] else ""
        contents = f"{tag} {goal}. {detail} How to get it: {e['how']}{note}"
        tooltip = f"{tag} {e['title']}" + (f": {e['stats']}" if e["stats"] else "")
        localization.append(
            f"\t{e['key']}: {{\n"
            f"\t\tTitle: {hjson_string(e['title'])}\n"
            f"\t\tContents: {hjson_string(contents)}\n"
            f"\t\tTooltip: {hjson_string(tooltip)}\n"
            "\t}\n"
        )

    for header in headers:
        classes.append(f"public sealed class {header} : HeaderQuest;\n")

    header_text = "// <auto-generated>\n//     Generated by tools/generate_gear.py.\n// </auto-generated>\n\n"

    (MOD / "Quests" / "GearGuide.Nodes.cs").write_text(
        header_text
        + "namespace BereftSouls.Quests;\n\n"
        + "internal sealed partial class GearGuide\n{\n"
        + "    private static readonly GearBook[] gear_books =\n    [\n"
        + "\n".join(book_fields)
        + "\n    ];\n}\n",
        encoding="utf-8",
    )

    (MOD / "Quests" / "Steps" / "GearQuests.cs").write_text(
        header_text
        + "using Terraria.ID;\nusing Terraria.ModLoader;\n\n"
        + "namespace BereftSouls.Quests.Steps;\n\n"
        + "\n".join(classes),
        encoding="utf-8",
    )

    names = "\n".join(f"\t\t{book}: {display}" for book, _, display in BOOKS)
    (MOD / "Localization" / "en-US_Mods.BereftSouls.QuestBooks.hjson").write_text(
        "Gear: {\n" + names.replace("\t\t", "\t") + "\n}\n\n" + "\n".join(localization).replace("\n\t", "\n").lstrip("\t"),
        encoding="utf-8",
    )

    print(f"generated {len(used)} gear entries and {len(headers)} section headers across {len(BOOKS)} books")


if __name__ == "__main__":
    main()
