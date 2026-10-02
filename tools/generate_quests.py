"""Generates the BereftSouls progression book from tools/quest_steps.json.

Writes:
  src/BereftSouls/Quests/ProgressionBook.Nodes.cs  - chapter node layout
  src/BereftSouls/Quests/Steps/StepQuests.cs       - one quest class per step
  src/BereftSouls/Quests/Steps/QuestRewards.cs     - reward per quest key
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

# Each tier is split into sections of about a dozen quests, each ending at a
# boss and opening once the previous section's last boss is down.
#   tier -> [(section name, display name, bosses in chain order)]
SECTIONS = {
    "PreHardmode": [
        ("FirstSteps", "First Steps", ["KingSlimeDefeated", "GlowmothDefeated", "DesertScourgeDefeated"]),
        ("TheEye", "The Eye", ["EyeOfCthulhuDefeated", "CrabulonDefeated"]),
        ("WorldEvil", "The World's Evil", ["EvilBossDefeated", "HiveMindDefeated", "PerforatorsDefeated"]),
        ("HiveAndPyramid", "Hive and Pyramid", ["QueenBeeDefeated", "PutridPinkyDefeated", "PharaohsCurseDefeated"]),
        ("Dungeon", "The Dungeon", ["SkeletronDefeated", "DeerclopsDefeated", "SlimeGodDefeated"]),
        ("Underworld", "Into the Underworld", ["ExcavatorDefeated", "AdvisorDefeated", "WallOfFleshDefeated"]),
    ],
    "EarlyHardmode": [
        ("NewWorld", "A New World", ["QueenSlimeDefeated", "CryogenDefeated", "AquaticScourgeDefeated"]),
        ("Mechanical", "The Mechanical Bosses",
         ["BrimstoneElementalDefeated", "TheDestroyerDefeated", "TheTwinsDefeated", "SkeletronPrimeDefeated"]),
        ("CalamitysShadow", "Calamity's Shadow", ["PolarisDefeated", "CalamitasCloneDefeated", "PlanteraDefeated"]),
    ],
    "PostPlantera": [
        ("DepthsAndStars", "Depths and Stars", ["LeviathanDefeated", "AstrumAureusDefeated", "GolemDefeated"]),
        ("JungleAndSea", "Jungle and Sea", ["PlaguebringerDefeated", "DukeFishronDefeated", "EmpressOfLightDefeated"]),
        ("TheCult", "The Cult", ["RavagerDefeated", "LuxDefeated", "LunaticCultistDefeated"]),
        ("CelestialAscent", "Celestial Ascent", ["AstrumDeusDefeated", "SubspaceSerpentDefeated", "MoonLordDefeated"]),
    ],
    "PostMoonLord": [
        ("TheProfaned", "The Profaned", ["ProfanedGuardiansDefeated", "DragonfollyDefeated", "ProvidenceDefeated"]),
        ("TheSentinels", "The Sentinels", ["StormWeaverDefeated", "CeaselessVoidDefeated", "SignusDefeated"]),
        ("TheDevourer", "The Devourer", ["PolterghastDefeated", "OldDukeDefeated", "DevourerOfGodsDefeated"]),
    ],
    "Endgame": [
        ("DragonAndWitch", "Dragon and Witch", ["YharonDefeated", "SupremeCalamitasDefeated"]),
        ("DraedonsArsenal", "Draedon's Arsenal", ["ExoMechsDefeated"]),
    ],
}

# Only one evil boss exists per world, so either closes its section.
EITHER = {
    "HiveMindDefeated": ["HiveMindDefeated", "PerforatorsDefeated"],
    "PerforatorsDefeated": ["HiveMindDefeated", "PerforatorsDefeated"],
}


def split_sections(tier, nodes):
    """Assigns each quest to a section: bosses by SECTIONS, steps to the
    section of the earliest boss that needs them (or, if nothing later needs
    them, the section they open in).  Lines between sections are dropped; a
    section only opens once the earlier ones are done."""
    sections = SECTIONS[tier]
    boss_section = {b: i for i, (_, _, bosses) in enumerate(sections) for b in bosses}
    closers = {bosses[-1] for _, _, bosses in sections} | {"HiveMindDefeated"}
    by_key = {n["key"]: n for n in nodes}
    for node in nodes:
        if not node["is_step"] and node["key"] not in boss_section:
            fail(f"{tier}: boss {node['key']} isn't in any section")

    children = {k: [] for k in by_key}
    for node in nodes:
        for after in node["after"]:
            children[after].append(node["key"])

    order = []
    seen = set()

    def visit(key):
        if key in seen:
            return
        seen.add(key)
        for after in by_key[key]["after"]:
            visit(after)
        order.append(key)

    for key in by_key:
        visit(key)

    # Earliest section a quest can be in, from what it follows.  A quest after
    # a section's last boss belongs to the next section.
    low = {}
    for key in order:
        bound = boss_section.get(key, 0)
        for after in by_key[key]["after"]:
            if after in boss_section:
                parent = boss_section[after] + (1 if after in closers else 0)
            else:
                parent = low[after]
            bound = max(bound, parent)
        low[key] = min(bound, len(sections) - 1)

    # Then pull each step as late as the first quest that needs it.
    section = {}
    for key in reversed(order):
        if key in boss_section:
            section[key] = boss_section[key]
            continue
        needed = [section[c] for c in children[key]]
        section[key] = max(low[key], min(needed)) if needed else low[key]

    result = []
    for index, (name, display, _) in enumerate(sections):
        members = [
            {**n, "after": [a for a in n["after"] if section[a] == index]}
            for n in nodes
            if section[n["key"]] == index
        ]
        result.append((name, display, members))
    return result


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


REFS = ROOT / "src" / "refs"
_non_public = {}


def non_public_classes(mod):
    """Names of classes in a referenced mod's source that aren't public, so
    can't be named from our assembly (e.g. Calamity's internal bobbers)."""
    if mod not in _non_public:
        found = set()
        decl = re.compile(
            r"^[ \t]*((?:(?:public|internal|private|protected|abstract|sealed|static|partial|new)\s+)*)class\s+(\w+)",
            re.M,
        )
        for path in (REFS / mod).rglob("*.cs") if (REFS / mod).is_dir() else []:
            for modifiers, name in decl.findall(path.read_text(encoding="utf-8-sig", errors="replace")):
                if "public" not in modifiers.split():
                    found.add(name)
        _non_public[mod] = found
    return _non_public[mod]


def item_expr(item):
    """C# expression for an item's type, from a step, set piece or reward."""
    if item.get("vanilla_id"):
        return f"ItemID.{item['vanilla_id']}"
    mod = item.get("mod") or item["namespace"].split(".")[0]
    if item["class"] in non_public_classes(mod):
        # Not accessible from another assembly; look it up by name instead.
        return f'ModContent.Find<ModItem>("{mod}/{item["class"]}").Type'
    return f"ModContent.ItemType<global::{item['namespace']}.{item['class']}>()"


def describe_reward(reward):
    if not reward:
        return ""
    stack = reward.get("stack", 1)
    count = f"{stack} " if stack > 1 else ""
    return f" [c/7FD4FF:Reward: {count}{reward['title']}]"


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


def compact(nodes, depth):
    """Moves each node right to just before its earliest dependent, so
    starting steps (Lenses, Iron Bars...) sit beside what they unlock instead
    of all piling into the first column with long lines across the chapter."""
    children = {n["key"]: [] for n in nodes}
    for node in nodes:
        for after in node["after"]:
            children[after].append(node["key"])

    for key in sorted(depth, key=lambda k: -depth[k]):
        if children[key]:
            depth[key] = max(depth[key], min(depth[c] for c in children[key]) - 1)

    return depth, children


def layout(nodes, depth, children):
    """Assigns grid rows per column, keeping required bosses on row 0 and
    placing everything else as close to the quests it connects to as
    possible."""
    priority = {"Gate": 0, "Required": 1, "Optional": 2}
    by_key = {n["key"]: n for n in nodes}
    rows = {}
    taken = {}

    def place(node, target):
        column = depth[node["key"]]
        used = taken.setdefault(column, set())
        offset = 0
        while True:
            for row in (target + offset, target - offset):
                if row not in used:
                    used.add(row)
                    rows[node["key"]] = row
                    return
            offset += 1

    def on_path(node):
        return node["kind"] in ("Gate", "Required") and not node["is_step"]

    # Bosses on the critical path first, so everything else arranges around them.
    for node in nodes:
        if on_path(node):
            place(node, 0)

    # Then left to right, each node near the average row of its parents.
    ordered = sorted(nodes, key=lambda n: (depth[n["key"]], priority[n["kind"]], n["is_step"], n["key"]))
    for node in ordered:
        if node["key"] in rows or not node["after"]:
            continue
        parents = [rows[a] for a in node["after"] if a in rows]
        place(node, round(sum(parents) / len(parents)) if parents else 0)

    # Starting steps last, next to the quests they lead to (right to left so
    # chains of them follow each other).
    for node in sorted(nodes, key=lambda n: -depth[n["key"]]):
        if node["key"] in rows:
            continue
        targets = [rows[c] for c in children[node["key"]] if c in rows]
        place(node, round(sum(targets) / len(targets)) if targets else 0)

    # Untangle: a few sweeps moving every off-path node towards the average
    # row of everything it connects to, keeping whichever pass crosses least.
    best, best_crossings = dict(rows), crossings(nodes, depth, rows)
    for sweep in range(8):
        columns = sorted(set(depth.values()), reverse=sweep % 2 == 1)
        for column in columns:
            in_column = [n for n in nodes if depth[n["key"]] == column and not on_path(n)]
            targets = {}
            for node in in_column:
                linked = [rows[a] for a in node["after"]] + [rows[c] for c in children[node["key"]]]
                targets[node["key"]] = sum(linked) / len(linked) if linked else rows[node["key"]]
            taken[column] = {rows[n["key"]] for n in nodes if depth[n["key"]] == column and on_path(n)}
            for node in sorted(in_column, key=lambda n: (priority[n["kind"]], abs(targets[n["key"]]))):
                place(node, round(targets[node["key"]]))
        count = crossings(nodes, depth, rows)
        if count < best_crossings:
            best, best_crossings = dict(rows), count

    return best


def crossings(nodes, depth, rows):
    """How many connector lines cross, with the book's column and row spacing."""
    edges = []
    for node in nodes:
        for after in node["after"]:
            edges.append(((depth[after] * 160, rows[after] * 130), (depth[node["key"]] * 160, rows[node["key"]] * 130)))

    def side(a, b, c):
        return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])

    count = 0
    for i, (p1, p2) in enumerate(edges):
        for p3, p4 in edges[i + 1:]:
            if len({p1, p2, p3, p4}) == 4 and side(p1, p2, p3) * side(p1, p2, p4) < 0 and side(p3, p4, p1) * side(p3, p4, p2) < 0:
                count += 1
    return count


def main():
    data = json.loads((TOOLS / "quest_steps.json").read_text(encoding="utf-8"))
    boss_text = json.loads((TOOLS / "boss_text.json").read_text(encoding="utf-8"))
    known_bosses = VANILLA_BOSSES | modded_bosses()

    chapters = {c["name"]: c for c in data["chapters"]}
    missing = [name for name, _, _ in CHAPTERS if name not in chapters]
    if missing:
        fail(f"missing chapters {missing}")

    node_fields = []
    section_rows = []
    section_names = []
    previous_bosses = []
    step_classes = []
    localization = []
    boss_kinds = {}
    step_keys = set()
    rewards = {}

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
            if boss.get("reward"):
                rewards.setdefault(boss["key"], boss["reward"])

        for step in chapter["steps"]:
            if step["key"] in known_bosses or step["key"] in step_keys:
                fail(f"{name}: step key {step['key']} is already used")
            step_keys.add(step["key"])
            nodes.append({**step, "is_step": True})

        # The chapter only opens once its entry boss (the previous chapter's
        # gate) is down, so a line from it to a quest says nothing; dropping
        # those keeps dozens of lines from fanning out across the chapter.
        entry = next((n["key"] for n in nodes if n["kind"] == "Gate" and not n["after"] and not n["is_step"]), None)
        if entry:
            # Shown at the end of the previous tier; this tier only opens once
            # it's down, so it isn't repeated here.
            nodes = [{**n, "after": [a for a in n["after"] if a != entry]} for n in nodes if n["key"] != entry]

        for section_index, (section_name, section_display, members) in enumerate(split_sections(name, nodes)):
            label = f"{name}/{section_name}"
            depth, children = compact(members, validate(label, members))
            rows = layout(members, depth, children)

            snake = re.sub(r"(?<!^)(?=[A-Z])", "_", section_name).lower()
            section_field = f"{field}_{snake}"
            lines = [f"    private static readonly Node[] {section_field} =", "    ["]
            for node in sorted(members, key=lambda n: (depth[n["key"]], rows[n["key"]])):
                kind = "Step" if node["is_step"] else node["kind"]
                any_of = "true" if node.get("any_of") else "false"
                after = "".join(f', "{a}"' for a in node["after"])
                lines.append(
                    f'        new("{node["key"]}", {depth[node["key"]]}, {rows[node["key"]]}, '
                    f"NodeKind.{kind}, {any_of}{after}),"
                )
            lines.append("    ];")
            node_fields.append("\n".join(lines))

            # Opens once every boss of the previous section is down (the pack's
            # first section is always open); "A|B" means either one.
            unlocked_by = []
            for boss in previous_bosses:
                group = "|".join(EITHER.get(boss, [boss]))
                if group not in unlocked_by:
                    unlocked_by.append(group)
            section_rows.append(
                f'        new("{name}", "{section_name}", {section_field}'
                + "".join(f', "{k}"' for k in unlocked_by)
                + "),"
            )
            section_names.append(f"\t\t\t{name}{section_name}: {hjson_string(section_display)}")
            previous_bosses = SECTIONS[name][section_index][2]
            print(f"  {label}: {len(members)} quests")

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
            if reward:
                rewards[step["key"]] = reward
            reward_text = describe_reward(reward)

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
        reward_text = describe_reward(rewards.get(key))
        contents = " ".join(part for part in (f"{tag} Defeat {title}.", how, why) if part) + reward_text
        tooltip = f"{tag} Defeat {title}" + reward_text
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
        + "\n\n    /// <summary>Every section in pack order.</summary>\n"
        + "    private static readonly Section[] sections =\n    [\n"
        + "\n".join(section_rows)
        + "\n    ];\n}\n",
        encoding="utf-8",
    )

    (MOD / "Quests" / "Steps" / "StepQuests.cs").write_text(
        header
        + "using Terraria.ID;\nusing Terraria.ModLoader;\n\n"
        + "namespace BereftSouls.Quests.Steps;\n\n"
        + "\n".join(step_classes),
        encoding="utf-8",
    )

    reward_lines = []
    for key, reward in rewards.items():
        reward_lines.append(f'            ["{key}"] = ({item_expr(reward)}, {reward.get("stack", 1)}),')

    (MOD / "Quests" / "Steps" / "QuestRewards.cs").write_text(
        header
        + "using System.Collections.Generic;\n\nusing Terraria.ID;\nusing Terraria.ModLoader;\n\n"
        + "namespace BereftSouls.Quests.Steps;\n\n"
        + "internal static class QuestRewards\n{\n"
        + "    /// <summary>Reward item and stack per quest key, including vanilla boss quests.</summary>\n"
        + "    public static Dictionary<string, (int Type, int Stack)> Create() => new()\n"
        + "    {\n"
        + "\n".join(reward_lines)
        + "\n    };\n}\n",
        encoding="utf-8",
    )

    book = ["\tProgression: {", "\t\tName: Bereft Progression"]
    book += [f"\t\t{name}: {display}" for name, _, display in CHAPTERS]
    book += ["\t\tSections: {"] + section_names + ["\t\t}"]
    book.append("\t}\n")
    book.append(f"\tRewardClaimed: {hjson_string('Quest reward claimed: {0}')}\n")

    (MOD / "Localization" / "en-US_Mods.BereftSouls.hjson").write_text(
        "QuestBooks: {\n" + "\n".join(book) + "\n" + "\n".join(localization) + "}\n",
        encoding="utf-8",
    )

    total = sum(len(c["steps"]) for c in data["chapters"])
    print(f"generated {total} step quests and {len(rewards)} rewards across {len(CHAPTERS)} chapters")


if __name__ == "__main__":
    main()
