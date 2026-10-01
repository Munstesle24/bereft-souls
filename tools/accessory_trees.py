"""Lays out accessory crafting trees for the gear guide books.

Each book gets one chapter: every accessory for that class (or with no class,
for the General book), plus the components needed to craft them, joined by
recipe lines from component to result. Higher-tier entries are hidden in-game
until the world reaches their tier, so the chapter grows with progression.
"""

import re

from generate_quests import CHAPTERS

TIERS = [name for name, _, _ in CHAPTERS]
PER_ROW = 8


def norm(text):
    return re.sub(r"[^a-z0-9]", "", text.lower())


def split_camel(ident):
    return re.sub(r"(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", " ", ident)


def recipe_names(entry):
    """Names of everything in an accessory's recipe."""
    record = entry["item"]
    if record.get("ingredients"):
        return record["ingredients"]
    match = re.search(r"from (.*)$", record.get("how", ""))
    if not match:
        return []
    names = []
    for part in re.split(r", | and ", match.group(1)):
        part = re.sub(r"^(a|an|\d+) ", "", part.strip()).rstrip(".")
        if part:
            names.append(part)
    return names


def build_graph(entries):
    """Returns accessory entries by key and component keys per result key."""
    accessories = {e["key"]: e for e in entries if e["category"] == "accessory"}

    index = {}
    for key, e in accessories.items():
        index.setdefault(norm(e["title"]), key)
        if e["item"].get("vanilla_id"):
            index.setdefault(norm(split_camel(e["item"]["vanilla_id"])), key)

    components = {}
    for key, e in accessories.items():
        found = []
        for name in recipe_names(e):
            component = index.get(norm(name))
            if component and component != key and component not in found:
                found.append(component)
        components[key] = found

    return accessories, components


def book_nodes(book, class_filter, accessories, components):
    """Node lines and header keys for one book's accessory chapter."""
    def belongs(e):
        return class_filter in e["classes"] if class_filter else not e["classes"]

    # Everything for this book, plus whatever it takes to craft it.
    included = set()
    stack = [k for k, e in accessories.items() if belongs(e)]
    while stack:
        key = stack.pop()
        if key in included:
            continue
        included.add(key)
        stack.extend(components[key])

    parts = {k: [c for c in components[k] if c in included] for k in included}
    results = {k: [] for k in included}
    for k, cs in parts.items():
        for c in cs:
            results[c].append(k)

    depth = {}

    def column(key, seen=()):
        if key in depth:
            return depth[key]
        if key in seen:  # recipe loops (e.g. transmutations) stop here
            return 0
        depth[key] = 0 if not parts[key] else 1 + max(column(c, seen + (key,)) for c in parts[key])
        return depth[key]

    for key in included:
        column(key)

    # Drop links that run backwards, which only happen in recipe loops (two
    # items craftable from each other), so every line points left to right.
    parts = {k: [c for c in cs if depth[c] < depth[k]] for k, cs in parts.items()}
    results = {k: [] for k in included}
    for k, cs in parts.items():
        for c in cs:
            results[c].append(k)

    # Connected groups, linked through recipes in either direction.
    groups, assigned = [], set()
    for key in sorted(included):
        if key in assigned:
            continue
        group, todo = set(), [key]
        while todo:
            k = todo.pop()
            if k in group:
                continue
            group.add(k)
            todo.extend(parts[k] + results[k])
        assigned |= group
        groups.append(group)

    tier_of = {k: TIERS.index(accessories[k]["tier"]) for k in included}
    trees = [g for g in groups if len(g) > 1]
    singles = sorted((k for g in groups if len(g) == 1 for k in g), key=lambda k: (tier_of[k], accessories[k]["title"]))
    trees.sort(key=lambda g: (min(tier_of[k] for k in g), -len(g)))

    rows = {}
    next_row = 0
    for group in trees:
        taken = {}
        leaf = [next_row]

        def place(key):
            if key in rows:
                return rows[key]
            children = sorted(parts[key], key=lambda c: accessories[c]["title"])
            if children:
                child_rows = [place(c) for c in children]
                row = round(sum(child_rows) / len(child_rows))
            else:
                row = leaf[0]
                leaf[0] += 1
            col = depth[key]
            while (col, row) in taken:
                row += 1
            taken[(col, row)] = key
            rows[key] = row
            leaf[0] = max(leaf[0], row + 1)
            return row

        finals = sorted((k for k in group if not results[k]), key=lambda k: accessories[k]["title"])
        for key in finals:
            place(key)
        for key in sorted(group):  # anything only reachable through a loop
            place(key)
        next_row = max(rows[k] for k in group) + 2

    lines = []

    def node(key, col, row, kind="Gear", after=()):
        tier = tier_of.get(key, 0)
        suffix = "".join(f', "{a}"' for a in after)
        init = f" {{ Tier = {tier} }}" if tier else ""
        lines.append(f'            new("{key}", {col}, {row}, NodeKind.{kind}, false{suffix}){init},')

    for key in sorted(rows, key=lambda k: (rows[k], depth[k])):
        node(key, depth[key], rows[key], after=parts[key])

    headers = []
    for tier_index, tier in enumerate(TIERS):
        tier_singles = [k for k in singles if tier_of[k] == tier_index]
        if not tier_singles:
            continue
        header = f"GearHeader{book}Accessories{tier}"
        headers.append((header, tier))
        lines.append(f'            new("{header}", 0, {next_row}, NodeKind.Header, false)' + (f" {{ Tier = {tier_index} }}" if tier_index else "") + ",")
        for i, key in enumerate(tier_singles):
            node(key, 1 + i % PER_ROW, next_row + i // PER_ROW)
        next_row += (len(tier_singles) - 1) // PER_ROW + 2

    return lines, headers, included
