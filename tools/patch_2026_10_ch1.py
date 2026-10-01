"""One-off edit of quest_steps.json from the first Chapter 1 review.

- Adds pre-Eye of Cthulhu gear and summon steps.
- Turns steps that never needed the Eye into their own starting branches.
- Marks the Aerialite chain optional (nothing later needs it).
- Unlinks the Guide Voodoo Doll from Hellstone (only Skeletron is a soft gate).
- Gives every step and boss a reward, filling gaps with tier-appropriate
  potions and flasks.
"""

import json
from pathlib import Path

PATH = Path(__file__).resolve().parent / "quest_steps.json"
data = json.loads(PATH.read_text(encoding="utf-8"))
chapters = {c["name"]: c for c in data["chapters"]}
pre = chapters["PreHardmode"]
steps = {s["key"]: s for s in pre["steps"]}
bosses = {b["key"]: b for b in pre["bosses"]}


def vanilla(item_id, title, stack=1):
    return {"vanilla_id": item_id, "title": title, "stack": stack}


def cal(namespace, cls, title, stack=1):
    return {"mod": "CalamityMod", "namespace": namespace, "class": cls, "title": title, "stack": stack}


def step(key, kind, after, title, how, item, type_="obtain", stack=1, reward=None, any_of=False, pieces=None):
    s = {"key": key, "type": type_, "kind": kind, "after": after, "any_of": any_of, "title": title, "how": how,
         "stack": stack, "uncertain": False, "reward": reward}
    if pieces:
        s["pieces"] = pieces
    else:
        s.update({k: v for k, v in item.items() if k in ("mod", "namespace", "class", "vanilla_id")})
    return s


# --- Pre-Eye of Cthulhu -----------------------------------------------------

new_steps = [
    step("FarmLens", "Required", [], "Lenses",
         "Demon Eyes drop them at night on the surface.",
         vanilla("Lens", "Lens"), stack=6, reward=vanilla("NightOwlPotion", "Night Owl Potion", 2)),
    step("CraftSuspiciousLookingEye", "Required", ["FarmLens"], "Suspicious Looking Eye",
         "Craft at a Demon or Crimson Altar from 6 Lenses and use it at night. The Eye of Cthulhu can also show up on its own.",
         vanilla("SuspiciousLookingEye", "Suspicious Looking Eye"), reward=vanilla("IronskinPotion", "Ironskin Potion", 2)),
    step("ObtainIronBars", "Optional", [], "Iron Bars",
         "Smelt 3 Iron Ore each at a Furnace. Either this or Lead Bars is enough for your first armor.",
         vanilla("IronBar", "Iron Bar"), stack=12, reward=vanilla("MiningPotion", "Mining Potion", 2)),
    step("ObtainLeadBars", "Optional", [], "Lead Bars",
         "Smelt 3 Lead Ore each at a Furnace. Either this or Iron Bars is enough for your first armor.",
         vanilla("LeadBar", "Lead Bar"), stack=12, reward=vanilla("MiningPotion", "Mining Potion", 2)),
    step("SetIronArmor", "Optional", ["ObtainIronBars"], "Iron Armor",
         "Craft the Iron Helmet, Chainmail and Greaves at an Iron or Lead Anvil.",
         {}, type_="equip_set", pieces=[vanilla("IronHelmet", ""), vanilla("IronChainmail", ""), vanilla("IronGreaves", "")],
         reward=vanilla("IronskinPotion", "Ironskin Potion", 3)),
    step("SetLeadArmor", "Optional", ["ObtainLeadBars"], "Lead Armor",
         "Craft the Lead Helmet, Chainmail and Greaves at an Iron or Lead Anvil.",
         {}, type_="equip_set", pieces=[vanilla("LeadHelmet", ""), vanilla("LeadChainmail", ""), vanilla("LeadGreaves", "")],
         reward=vanilla("IronskinPotion", "Ironskin Potion", 3)),
    step("FarmWulfrumScrap", "Optional", [], "Wulfrum Metal Scrap",
         "Dropped by Wulfrum robots (Drones, Gyrators, Hovercraft) on the surface during the day.",
         cal("CalamityMod.Items.Materials", "WulfrumMetalScrap", ""), stack=15,
         reward=vanilla("LesserHealingPotion", "Lesser Healing Potion", 5)),
    step("CraftWulfrumProsthesis", "Optional", ["FarmWulfrumScrap"], "Wulfrum Prosthesis",
         "Craft at an Anvil from 10 Wulfrum Metal Scrap. An early magic weapon for the Eye.",
         cal("CalamityMod.Items.Weapons.Magic", "WulfrumProsthesis", ""),
         reward=vanilla("ManaRegenerationPotion", "Mana Regeneration Potion", 2)),
    step("CraftWulfrumKnife", "Optional", ["FarmWulfrumScrap"], "Wulfrum Knife",
         "Craft at an Anvil from 10 Wulfrum Metal Scrap. An early rogue weapon for the Eye.",
         cal("CalamityMod.Items.Weapons.Rogue", "WulfrumKnife", ""),
         reward=vanilla("SwiftnessPotion", "Swiftness Potion", 2)),
    step("EquipMarniteRepulsionShield", "Optional", [], "Marnite Repulsion Shield",
         "Craft at an Anvil from Granite and Marble blocks, found in their underground cave biomes.",
         cal("CalamityMod.Items.Accessories", "MarniteRepulsionShield", ""), type_="equip",
         reward=vanilla("ThornsPotion", "Thorns Potion", 2)),
]
pre["steps"] = new_steps + pre["steps"]
steps.update({s["key"]: s for s in new_steps})

bosses["EyeOfCthulhuDefeated"]["after"] = ["CraftSuspiciousLookingEye"]

# Steps that never needed the Eye become their own starting branches.
for key in ("SetSnowRuffianArmor", "FarmFragmentOfNature", "FarmFragmentOfEarth", "FarmFragmentOfTide",
            "FarmFragmentOfOtherworld", "FarmStormlionMandible", "CraftFrigidBar", "ObtainPrismShard",
            "CraftAbeemination", "CraftDeerThing"):
    steps[key]["after"] = []
steps["SetWulfrumArmor"]["after"] = ["FarmWulfrumScrap"]

# Nothing later needs Aerialite; it only feeds optional gear and the Pinky
# summon.
for key in ("FarmFragmentOfOtherworld", "MineAerialiteOre", "CraftAerialiteBar"):
    steps[key]["kind"] = "Optional"

# Hellstone stays required (the Eye of Desolation needs it in Chapter 2), but
# the Wall of Flesh only needs the doll.
steps["ObtainGuideVoodooDoll"]["after"] = ["SkeletronDefeated"]
steps["CraftHellstoneBars"]["how"] += " Required for Chapter 2."

# --- Rewards ----------------------------------------------------------------

def pool(*items):
    return [vanilla(i, t, n) for i, t, n in items]


UTILITY_PRE = pool(
    ("IronskinPotion", "Ironskin Potion", 2), ("SwiftnessPotion", "Swiftness Potion", 2),
    ("RegenerationPotion", "Regeneration Potion", 2), ("ShinePotion", "Shine Potion", 2),
    ("NightOwlPotion", "Night Owl Potion", 2), ("HunterPotion", "Hunter Potion", 2),
    ("ArcheryPotion", "Archery Potion", 2), ("BattlePotion", "Battle Potion", 2),
    ("BuilderPotion", "Builder Potion", 2), ("MagicPowerPotion", "Magic Power Potion", 2),
    ("SummoningPotion", "Summoning Potion", 2), ("ThornsPotion", "Thorns Potion", 2),
)
COMBAT_HM = pool(
    ("EndurancePotion", "Endurance Potion", 2), ("LifeforcePotion", "Lifeforce Potion", 2),
    ("InfernoPotion", "Inferno Potion", 2), ("RagePotion", "Rage Potion", 2),
    ("WrathPotion", "Wrath Potion", 2), ("TitanPotion", "Titan Potion", 2),
    ("HeartreachPotion", "Heartreach Potion", 2), ("FlaskofCursedFlames", "Flask of Cursed Flames", 1),
    ("FlaskofIchor", "Flask of Ichor", 1), ("FlaskofFire", "Flask of Fire", 1),
)
COMBAT_PLANTERA = COMBAT_HM + pool(("FlaskofVenom", "Flask of Venom", 1), ("FlaskofNanites", "Flask of Nanites", 1))

TIERS = {
    # chapter: (step filler pool, step healing, boss reward)
    "PreHardmode": (UTILITY_PRE, vanilla("LesserHealingPotion", "Lesser Healing Potion", 5),
                    vanilla("HealingPotion", "Healing Potion", 5)),
    "EarlyHardmode": (COMBAT_HM + UTILITY_PRE, vanilla("HealingPotion", "Healing Potion", 5),
                      vanilla("GreaterHealingPotion", "Greater Healing Potion", 5)),
    "PostPlantera": (COMBAT_PLANTERA, vanilla("GreaterHealingPotion", "Greater Healing Potion", 3),
                     vanilla("GreaterHealingPotion", "Greater Healing Potion", 8)),
    "PostMoonLord": (COMBAT_PLANTERA, vanilla("SuperHealingPotion", "Super Healing Potion", 3),
                     vanilla("SuperHealingPotion", "Super Healing Potion", 5)),
    "Endgame": (COMBAT_PLANTERA, vanilla("SuperHealingPotion", "Super Healing Potion", 5),
                vanilla("SuperHealingPotion", "Super Healing Potion", 10)),
}

MINING = [vanilla("MiningPotion", "Mining Potion", 2), vanilla("SpelunkerPotion", "Spelunker Potion", 2)]
seen_bosses = set()

for name, chapter in chapters.items():
    filler, healing, boss_reward = TIERS[name]
    i = 0
    for s in chapter["steps"]:
        if s.get("reward"):
            continue
        text = (s["title"] + " " + s["how"]).lower()
        if "ore" in s["title"].lower() or "mine " in text:
            s["reward"] = MINING[i % 2]
        elif "fish" in text:
            s["reward"] = vanilla("FishingPotion", "Fishing Potion", 2)
        elif i % 4 == 3:
            s["reward"] = healing
        else:
            s["reward"] = filler[i % len(filler)]
        i += 1

    for b in chapter["bosses"]:
        # Gate bosses appear in two chapters; reward them where they are beaten.
        if b["key"] in seen_bosses:
            continue
        seen_bosses.add(b["key"])
        b["reward"] = boss_reward

PATH.write_text(json.dumps(data, indent=1, ensure_ascii=False), encoding="utf-8")
print("patched")
