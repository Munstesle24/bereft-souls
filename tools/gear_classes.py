"""Decides which class books an accessory or armor set belongs in.

Uses the item's own wording: a class goes in when the tooltip or set bonus
names something only that class uses (melee speed, mana, minions, stealth...).
Anything that names no class, or only boosts damage in general, goes to the
General book. Weapons keep the class of their damage type instead.
"""

import re

CLASS_TERMS = {
    "melee": [r"\bmelee\b", r"\btrue melee\b", r"\bswing speed\b"],
    # Not "short-ranged" or "long-ranged", which describe reach.
    "ranged": [r"(?<![-\w])ranged\b", r"\barrows?\b", r"\bbullets?\b", r"\brockets?\b", r"\bguns?\b", r"\bbows?\b"],
    "magic": [r"\bmagic\b", r"\bmana\b", r"\bspells?\b"],
    "summon": [r"\bminions?\b", r"\bsentr(y|ies)\b", r"\bwhips?\b", r"\bsummon(er)? damage\b", r"\bsummons? (tag|knockback)\b"],
    "rogue": [r"\brogue\b", r"\bstealth\b"],
}

# Phrases that mention a class word without being about that class.
IGNORE = [
    r"does not benefit from any damage class",
    r"magic mirror",
    r"magic lantern",
    r"regardless of (damage )?class",
]


def classes_from_text(*texts):
    text = " ".join(t for t in texts if t).lower()
    for phrase in IGNORE:
        text = re.sub(phrase, " ", text)
    return sorted(c for c, terms in CLASS_TERMS.items() if any(re.search(t, text) for t in terms))
