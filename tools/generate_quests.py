"""Generates the BereftSouls progression book from tools/quest_steps.json.

Writes:
  src/BereftSouls/Quests/ProgressionBook.Nodes.cs  - chapter node layout
  src/BereftSouls/Quests/Steps/StepQuests.cs       - one quest class per step
  src/BereftSouls/Localization/en-US_Mods.BereftSouls.hjson

Run from the repository root:  python tools/generate_quests.py
"""

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
TOOLS = ROOT / "tools"
MOD = ROOT / "src" / "BereftSouls"

CHAPTERS = [
    # (data name, C# field, display name)
    ("PreHardmode", "pre_hardmode", "Pre-Hardmode"),
    ("EarlyHardmode", "early_hardmode", "Early Hardmode"),
    ("PostPlantera", "post_plantera", "Post-Plantera"),
    ("PostMoonLord", "post_moon_lord", "Post-Moon Lord"),
    ("Endgame", "endgame", "Endgame"),
]

# Boss quests provided by QuestBooks itself.
VANILLA_BOSSES = {
    "KingSlimeDefeated", "EyeOfCthulhuDefeated", "EvilBossDefeated", "QueenBeeDefeated",
    "SkeletronDefeated", "DeerclopsDefeated", "WallOfFleshDefeated", "QueenSlimeDefeated",
    "TheTwinsDefeated", "TheDestroyerDefeated", "SkeletronPrimeDefeated", "PlanteraDefeated",
    "GolemDefeated", "DukeFishronDefeated", "EmpressOfLightDefeated", "LunaticCultistDefeated",
    "MoonLordDefeated",
}

TAGS = {
    "Required": "[c/FFD700:Required]",
    "Gate": "[c/FFD700:Required]",
    "Optional": "[c/9AA0A6:Optional]",
    "Final": "[c/FF4040:Final boss]",
}

FINAL_BOSSES = {"ExoMechsDefeated", "SupremeCalamitasDefeated"}


def fail(message):
    print(f"error: {message}", file=sys.stderr)
    sys.exit(1)


def hjson_string(value):
    return '"' + value.replace("\\", "\\\\").replace('"', '\\"') + '"'


def item_expr(item):
    """C# expression for an item's type, from a step, set piece or reward."""
    if item.get("vanilla_id"):
        return f"ItemID.{item['vanilla_id']}"
    return f"ModContent.ItemType<global::{item['namespace']}.{item['class']}>()"


def modded_bosses():
    source = (MOD / "Quests" / "Bosses" / "BossQuests.cs").read_text(encoding="utf-8")
    return set(re.findall(r"public sealed class (\w+) : BossQuest", source))


def validate(chapter, nodes):
    keys = [n["key"] for n in nodes]
    duplicates = {k for k in keys if keys.count(k) > 1}
    if duplicates:
        fail(f"{chapter}: duplicate keys {sorted(duplicates)}")

    by_key = {n["key"]: n for n in nodes}
    for node in nodes:
        for after in node["after"]:
            if after not in by_key:
                fail(f"{chapter}: {node['key']} depends on {after}, which isn't in this chapter")

    if not any(not n["after"] for n in nodes):
        fail(f"{chapter}: no entry node")

    # Longest-path depth doubles as the cycle check.
    depth = {}
    visiting = set()

    def visit(key):
        if key in depth:
            return depth[key]
        if key in visiting:
            fail(f"{chapter}: dependency cycle through {key}")
        visiting.add(key)
        afters = by_key[key]["after"]
        depth[key] = 0 if not afters else 1 + max(visit(a) for a in afters)
        visiting.discard(key)
        return depth[key]

    for key in keys:
        visit(key)

    return depth


def layout(nodes, depth):
    """Assigns grid rows per column, keeping the critical path on row 0 and
    placing everything else as close to its parents as possible."""
    priority = {"Gate": 0, "Required": 1, "Optional": 2}
    rows = {}

    for column in sorted(set(depth.values())):
        in_column = [n for n in nodes if depth[n["key"]] == column]
        in_column.sort(key=lambda n: (priority[n["kind"]], n["is_step"], n["key"]))

        taken = set()
        for node in in_column:
            parents = [rows[a] for a in node["after"] if a in rows]
            if node["kind"] in ("Gate", "Required") and not node["is_step"]:
                target = 0
            else:
                target = round(sum(parents) / len(parents)) if parents else 0

            offset = 0
            while True:
                for row in (target + offset, target - offset):
                    if row not in taken:
                        break
                else:
                    offset += 1
                    continue
                break

            taken.add(row)
            rows[node["key"]] = row

    return rows


def main():
    data = json.loads((TOOLS / "quest_steps.json").read_text(encoding="utf-8"))
    boss_text = json.loads((TOOLS / "boss_text.json").read_text(encoding="utf-8"))
    known_bosses = VANILLA_BOSSES | modded_bosses()

    chapters = {c["name"]: c for c in data["chapters"]}
    missing = [name for name, _, _ in CHAPTERS if name not in chapters]
    if missing:
        fail(f"missing chapters {missing}")

    node_fields = []
    step_classes = []
    localization = []
    boss_kinds = {}
    step_keys = set()

    for name, field, _ in CHAPTERS:
        chapter = chapters[name]
        nodes = []

        for boss in chapter["bosses"]:
            if boss["key"] not in known_bosses:
                fail(f"{name}: unknown boss quest {boss['key']}")
            nodes.append({**boss, "is_step": False})
            # A boss can appear in two chapters (as one's gate and the next's
            # entry); keep the most important kind for its text.
            if boss_kinds.get(boss["key"]) != "Gate":
                boss_kinds[boss["key"]] = boss["kind"]

        for step in chapter["steps"]:
            if step["key"] in known_bosses or step["key"] in step_keys:
                fail(f"{name}: step key {step['key']} is already used")
            step_keys.add(step["key"])
            nodes.append({**step, "is_step": True})

        depth = validate(name, nodes)
        rows = layout(nodes, depth)

        lines = [f"    private static readonly Node[] {field} =", "    ["]
        for node in sorted(nodes, key=lambda n: (depth[n["key"]], rows[n["key"]])):
            kind = "Step" if node["is_step"] else node["kind"]
            any_of = "true" if node.get("any_of") else "false"
            after = "".join(f', "{a}"' for a in node["after"])
            lines.append(
                f'        new("{node["key"]}", {depth[node["key"]]}, {rows[node["key"]]}, '
                f"NodeKind.{kind}, {any_of}{after}),"
            )
        lines.append("    ];")
        node_fields.append("\n".join(lines))

        for step in chapter["steps"]:
            if step["type"] not in ("obtain", "equip", "equip_set"):
                fail(f"{step['key']}: unknown step type {step['type']}")

            body = []
            stack = step.get("stack", 1)

            if step["type"] == "equip_set":
                base = "EquipSetQuest"
                pieces = ", ".join(item_expr(p) for p in step["pieces"])
                body.append(f"    protected override int[] Pieces => [{pieces}];")
                goal = f"Wear the full {step['title']}"
            elif step["type"] == "equip":
                base = "EquipItemQuest"
                body.append(f"    protected override int ItemType => {item_expr(step)};")
                goal = f"Equip {step['title']}"
            else:
                base = "ObtainItemQuest"
                body.append(f"    protected override int ItemType => {item_expr(step)};")
                if stack > 1:
                    body.append(f"    protected override int Stack => {stack};")
                    goal = f"Obtain {stack} {step['title']}"
                else:
                    goal = f"Obtain {step['title']}"

            reward = step.get("reward")
            reward_text = ""
            if reward:
                body.append(f"    protected override int RewardType => {item_expr(reward)};")
                reward_stack = reward.get("stack", 1)
                if reward_stack > 1:
                    body.append(f"    protected override int RewardStack => {reward_stack};")
                count = f"{reward_stack} " if reward_stack > 1 else ""
                reward_text = f" [c/7FD4FF:Reward: {count}{reward['title']}]"

            step_classes.append(
                f"public sealed class {step['key']} : {base}\n{{\n" + "\n\n".join(body) + "\n}\n"
            )

            tag = TAGS[step["kind"]]
            contents = f"{tag} {goal}. " + step["how"] + reward_text
            tooltip = f"{tag} {goal}" + reward_text
            localization.append(
                f"\t{step['key']}: {{\n"
                f"\t\tTitle: {hjson_string(step['title'])}\n"
                f"\t\tContents: {hjson_string(contents)}\n"
                f"\t\tTooltip: {hjson_string(tooltip)}\n"
                "\t}\n"
            )

    for key, kind in boss_kinds.items():
        if key in VANILLA_BOSSES:
            continue
        if key not in boss_text:
            fail(f"no boss text for {key}")
        title, how, why = boss_text[key]
        tag = TAGS["Final" if key in FINAL_BOSSES else kind]
        contents = " ".join(part for part in (f"{tag} Defeat {title}.", how, why) if part)
        tooltip = f"{tag} Defeat {title}"
        localization.append(
            f"\t{key}: {{\n"
            f"\t\tTitle: {hjson_string(title)}\n"
            f"\t\tContents: {hjson_string(contents)}\n"
            f"\t\tTooltip: {hjson_string(tooltip)}\n"
            "\t}\n"
        )

    header = "// <auto-generated>\n//     Generated by tools/generate_quests.py from tools/quest_steps.json.\n// </auto-generated>\n\n"

    (MOD / "Quests" / "ProgressionBook.Nodes.cs").write_text(
        header
        + "namespace BereftSouls.Quests;\n\n"
        + "internal sealed partial class ProgressionBook\n{\n"
        + "\n\n".join(node_fields)
        + "\n}\n",
        encoding="utf-8",
    )

    (MOD / "Quests" / "Steps" / "StepQuests.cs").write_text(
        header
        + "using Terraria.ID;\nusing Terraria.ModLoader;\n\n"
        + "namespace BereftSouls.Quests.Steps;\n\n"
        + "\n".join(step_classes),
        encoding="utf-8",
    )

    book = ["\tProgression: {", "\t\tName: Bereft Progression"]
    book += [f"\t\t{name}: {display}" for name, _, display in CHAPTERS]
    book.append("\t}\n")

    (MOD / "Localization" / "en-US_Mods.BereftSouls.hjson").write_text(
        "QuestBooks: {\n" + "\n".join(book) + "\n" + "\n".join(localization) + "}\n",
        encoding="utf-8",
    )

    total = sum(len(c["steps"]) for c in data["chapters"])
    print(f"generated {total} step quests across {len(CHAPTERS)} chapters")


if __name__ == "__main__":
    main()
