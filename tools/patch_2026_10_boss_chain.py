"""One-off edit of quest_steps.json for the Bereft boss chain.

Every chain boss becomes required and drops a Sigil the next boss needs
(see src/BereftCompatibility/Content/BossChain).  This rewires each chapter so
its bosses run in the chain's order, notes the Sigil on each summon step, and
adds the three new chain items: the Fleshbound Effigy and the two Sigil baits.

Run once from the repository root, then run generate_quests.py:
    python tools/patch_2026_10_boss_chain.py
"""

import json
import sys
from pathlib import Path

PATH = Path(__file__).resolve().parent / "quest_steps.json"
data = json.loads(PATH.read_text(encoding="utf-8"))
chapters = {c["name"]: c for c in data["chapters"]}

SIGIL = {
    "KingSlimeDefeated": "King Slime", "GlowmothDefeated": "Glowmoth", "DesertScourgeDefeated": "Desert Scourge",
    "EyeOfCthulhuDefeated": "Eye of Cthulhu", "CrabulonDefeated": "Crabulon", "EvilBossDefeated": "World Evil",
    "HiveMindDefeated": "Hive Mind", "PerforatorsDefeated": "Perforator", "QueenBeeDefeated": "Queen Bee",
    "PutridPinkyDefeated": "Putrid Pinky", "PharaohsCurseDefeated": "Pharaoh's Curse", "SkeletronDefeated": "Skeletron",
    "DeerclopsDefeated": "Deerclops", "SlimeGodDefeated": "Slime God", "ExcavatorDefeated": "Excavator",
    "AdvisorDefeated": "Advisor", "WallOfFleshDefeated": "Wall of Flesh", "QueenSlimeDefeated": "Queen Slime",
    "CryogenDefeated": "Cryogen", "AquaticScourgeDefeated": "Aquatic Scourge",
    "BrimstoneElementalDefeated": "Brimstone Elemental", "TheDestroyerDefeated": "Destroyer", "TheTwinsDefeated": "Twins",
    "SkeletronPrimeDefeated": "Skeletron Prime", "PolarisDefeated": "Polaris", "CalamitasCloneDefeated": "Calamitas Clone",
    "PlanteraDefeated": "Plantera", "LeviathanDefeated": "Leviathan", "AstrumAureusDefeated": "Astrum Aureus",
    "GolemDefeated": "Golem", "PlaguebringerDefeated": "Plaguebringer", "DukeFishronDefeated": "Duke Fishron",
    "EmpressOfLightDefeated": "Empress of Light", "RavagerDefeated": "Ravager", "LuxDefeated": "Lux",
    "LunaticCultistDefeated": "Lunatic Cultist", "AstrumDeusDefeated": "Astrum Deus",
    "SubspaceSerpentDefeated": "Subspace Serpent", "MoonLordDefeated": "Moon Lord",
    "ProfanedGuardiansDefeated": "Profaned Guardian", "DragonfollyDefeated": "Dragonfolly",
    "ProvidenceDefeated": "Providence", "StormWeaverDefeated": "Storm Weaver", "CeaselessVoidDefeated": "Ceaseless Void",
    "SignusDefeated": "Signus", "PolterghastDefeated": "Polterghast", "OldDukeDefeated": "Old Duke",
    "DevourerOfGodsDefeated": "Devourer of Gods", "YharonDefeated": "Yharon", "SupremeCalamitasDefeated": "Supreme Calamitas",
}


def sigils(keys, joiner=" and "):
    return joiner.join(f"{SIGIL[k]} Sigil" for k in keys)


# (boss, previous bosses, summon step or None, boss prerequisites or None,
#  extra prerequisites for the summon step, note for the summon step)
#
# A boss prerequisite of None keeps what the boss already has.  "carry" notes
# go on found items; "recipe" notes say the summon now needs the Sigil.
CHAINS = {
    "PreHardmode": [
        ("KingSlimeDefeated", [], None, None, [], None),
        ("GlowmothDefeated", ["KingSlimeDefeated"], "CraftSuspiciousLookingCandle", None, ["KingSlimeDefeated"], "recipe"),
        ("DesertScourgeDefeated", ["GlowmothDefeated"], "CraftDesertMedallion", None, ["GlowmothDefeated"], "recipe"),
        ("EyeOfCthulhuDefeated", ["DesertScourgeDefeated"], "CraftSuspiciousLookingEye", None, ["DesertScourgeDefeated"], "recipe"),
        ("CrabulonDefeated", ["EyeOfCthulhuDefeated"], "CraftDecapoditaSprout", None, ["EyeOfCthulhuDefeated"], "recipe"),
        ("EvilBossDefeated", ["CrabulonDefeated"], None, ["CrabulonDefeated"], [], None),
        # First fights come from a Hive Tumor or Perforator Cyst; their summons
        # need Aerialite, which the first kill enchants.
        ("HiveMindDefeated", ["EvilBossDefeated"], None, ["EvilBossDefeated"], [], None),
        ("PerforatorsDefeated", ["EvilBossDefeated"], None, ["EvilBossDefeated"], [], None),
        ("QueenBeeDefeated", ["HiveMindDefeated", "PerforatorsDefeated"], "CraftAbeemination", None, None, "recipe-any"),
        ("PutridPinkyDefeated", ["QueenBeeDefeated"], "CraftJarOfPeanuts", None, ["QueenBeeDefeated"], "recipe"),
        ("PharaohsCurseDefeated", ["PutridPinkyDefeated"], None, ["PutridPinkyDefeated"], [], None),
        ("SkeletronDefeated", ["PharaohsCurseDefeated"], None, ["PharaohsCurseDefeated"], [], None),
        ("DeerclopsDefeated", ["SkeletronDefeated"], "CraftDeerThing", None, ["SkeletronDefeated"], "recipe"),
        ("SlimeGodDefeated", ["DeerclopsDefeated"], "CraftOverloadedSludge", None, ["DeerclopsDefeated"], "recipe"),
        ("ExcavatorDefeated", ["SlimeGodDefeated"], None, ["SlimeGodDefeated"], [], None),
        ("AdvisorDefeated", ["ExcavatorDefeated"], None, ["ExcavatorDefeated"], [], None),
        ("WallOfFleshDefeated", [], None, ["CraftFleshboundEffigy"], [], None),
    ],
    "EarlyHardmode": [
        ("QueenSlimeDefeated", ["WallOfFleshDefeated"], "ObtainGelatinCrystal", None, [], "carry"),
        ("CryogenDefeated", ["QueenSlimeDefeated"], "CraftCryoKey", None, ["QueenSlimeDefeated"], "recipe"),
        ("AquaticScourgeDefeated", ["CryogenDefeated"], "CraftSeafood", None, ["CryogenDefeated"], "recipe"),
        ("BrimstoneElementalDefeated", ["AquaticScourgeDefeated"], "CraftCharredIdol", None, ["AquaticScourgeDefeated"], "recipe"),
        ("TheDestroyerDefeated", ["BrimstoneElementalDefeated"], "CraftMechanicalWorm", None, "anvil", "recipe"),
        ("TheTwinsDefeated", ["TheDestroyerDefeated"], "CraftMechanicalEye", None, "anvil", "recipe"),
        ("SkeletronPrimeDefeated", ["TheTwinsDefeated"], "CraftMechanicalSkull", None, "anvil", "recipe"),
        ("PolarisDefeated", ["SkeletronPrimeDefeated"], "CraftFrostedKey", None, ["SkeletronPrimeDefeated"], "recipe"),
        ("CalamitasCloneDefeated", ["PolarisDefeated"], "CraftEyeofDesolation", None, ["PolarisDefeated"], "recipe"),
        ("PlanteraDefeated", ["TheDestroyerDefeated", "TheTwinsDefeated", "SkeletronPrimeDefeated", "CalamitasCloneDefeated"],
         None, ["TheDestroyerDefeated", "TheTwinsDefeated", "SkeletronPrimeDefeated", "CalamitasCloneDefeated"], [], None),
    ],
    "PostPlantera": [
        ("LeviathanDefeated", ["PlanteraDefeated"], None, ["PlanteraDefeated"], [], None),
        ("AstrumAureusDefeated", ["LeviathanDefeated"], "CraftAstralChunk", None, ["LeviathanDefeated"], "recipe"),
        ("GolemDefeated", ["AstrumAureusDefeated"], "ObtainLihzahrdPowerCell", ["ObtainLihzahrdPowerCell", "AstrumAureusDefeated"], [], "carry"),
        ("PlaguebringerDefeated", ["GolemDefeated"], "CraftAbombination", None, ["GolemDefeated"], "recipe"),
        ("DukeFishronDefeated", [], None, ["CraftSigilTruffleWorm"], [], None),
        ("EmpressOfLightDefeated", ["DukeFishronDefeated"], "ObtainPrismaticLacewing", ["ObtainPrismaticLacewing", "DukeFishronDefeated"], [], "carry"),
        ("RavagerDefeated", ["EmpressOfLightDefeated"], "CraftDeathWhistle", None, ["GolemDefeated", "EmpressOfLightDefeated"], "recipe"),
        ("LuxDefeated", ["RavagerDefeated"], "CraftElectromagneticLure", ["CraftElectromagneticLure", "RavagerDefeated"], [], "carry"),
        ("LunaticCultistDefeated", ["LuxDefeated"], None, ["LuxDefeated"], [], None),
        ("AstrumDeusDefeated", ["LunaticCultistDefeated"], "ObtainTitanHeart", ["ObtainTitanHeart", "LunaticCultistDefeated"], [], "carry"),
        ("SubspaceSerpentDefeated", ["AstrumDeusDefeated"], "CraftCatalystBomb", None, ["AstrumDeusDefeated"], "recipe"),
        ("MoonLordDefeated", ["SubspaceSerpentDefeated"], None, ["SubspaceSerpentDefeated"], [], None),
    ],
    "PostMoonLord": [
        ("ProfanedGuardiansDefeated", ["MoonLordDefeated"], "CraftProfanedShard", None, ["MoonLordDefeated"], "recipe"),
        ("DragonfollyDefeated", ["ProfanedGuardiansDefeated"], "CraftExoticPheromones", None, ["ProfanedGuardiansDefeated"], "recipe"),
        ("ProvidenceDefeated", ["DragonfollyDefeated"], "ObtainProfanedCore", ["ObtainProfanedCore", "DragonfollyDefeated"], [], "carry"),
        ("StormWeaverDefeated", ["ProvidenceDefeated"], "CraftMarkOfProvidence", None, [], "recipe"),
        ("CeaselessVoidDefeated", ["ProvidenceDefeated"], None, None, [], None),
        ("SignusDefeated", ["ProvidenceDefeated"], None, None, [], None),
        ("PolterghastDefeated", ["StormWeaverDefeated", "CeaselessVoidDefeated", "SignusDefeated"], "CraftNecroplasmicBeacon", None,
         ["StormWeaverDefeated", "CeaselessVoidDefeated", "SignusDefeated"], "recipe"),
        ("OldDukeDefeated", [], None, ["CraftSigilBloodworm"], [], None),
        ("DevourerOfGodsDefeated", ["OldDukeDefeated"], "CraftCosmicWorm", None, ["OldDukeDefeated"], "recipe"),
    ],
    "Endgame": [
        ("YharonDefeated", ["DevourerOfGodsDefeated"], "CraftYharonEgg", None, [], "recipe"),
        ("SupremeCalamitasDefeated", ["YharonDefeated"], "GatherAshesOfCalamity", None, [], "carry"),
        ("ExoMechsDefeated", ["SupremeCalamitasDefeated"], "CraftAuricQuantumCoolingCell", None, ["SupremeCalamitasDefeated"], "recipe"),
    ],
}

NOTES = {
    "recipe": "Bereft: the recipe also needs 1 {sigils}.",
    "recipe-any": "Bereft: the recipe also needs 1 {sigils}.",
    "carry": "Bereft: someone nearby must carry {sigils} when it's used, or the boss won't wake.",
}


def vanilla(item_id, title, stack=1):
    return {"vanilla_id": item_id, "title": title, "stack": stack}


def chain_item(cls, key, after, title, how, reward):
    return {"key": key, "type": "obtain", "kind": "Required", "after": after, "any_of": False, "title": title,
            "how": how, "stack": 1, "uncertain": False, "reward": reward,
            "mod": "BereftCompatibility", "namespace": "BereftCompatibility.Content.BossChain", "class": cls}


NEW_STEPS = {
    "PreHardmode": chain_item(
        "FleshboundEffigy", "CraftFleshboundEffigy",
        ["ObtainGuideVoodooDoll", "EyeOfCthulhuDefeated", "EvilBossDefeated", "SkeletronDefeated", "AdvisorDefeated"],
        "Fleshbound Effigy",
        "Craft at a Demon or Crimson Altar from a Guide Voodoo Doll plus an Eye of Cthulhu, World Evil, Skeletron and "
        "Advisor Sigil. Throw it into Underworld lava. Until the Wall of Flesh falls, a plain Guide Voodoo Doll won't summon it.",
        vanilla("IronskinPotion", "Ironskin Potion", 3)),
    "PostPlantera": chain_item(
        "SigilTruffleWorm", "CraftSigilTruffleWorm", ["ObtainTruffleWorm", "PlaguebringerDefeated"],
        "Sigil Truffle Worm",
        "Craft from a Truffle Worm and a Plaguebringer Sigil. Fish with it in the Ocean to summon Duke Fishron; "
        "until he's beaten, a plain Truffle Worm is just bait.",
        vanilla("SonarPotion", "Sonar Potion", 2)),
    "PostMoonLord": chain_item(
        "SigilBloodworm", "CraftSigilBloodworm", ["ObtainBloodworm", "PolterghastDefeated"],
        "Sigil Bloodworm",
        "Craft from a Bloodworm and a Polterghast Sigil. Fish with it in the Sulphurous Sea to summon the Old Duke; "
        "until he's beaten, plain Bloodworms and Acid Rain won't bring him.",
        vanilla("SonarPotion", "Sonar Potion", 2)),
}

# Fixes found while checking the source.
FIXES = {
    "PreHardmode": {
        "CraftAbeemination": {"after": ["HiveMindDefeated", "PerforatorsDefeated"], "any_of": True},
    },
    "PostPlantera": {
        "FarmMeldBlob": {"after": ["AstrumDeusDefeated"],
                         "how": "Astrum Deus drops 16-24 Meld Blobs (20-32 in Expert)."},
    },
}


def fail(message):
    print(f"error: {message}", file=sys.stderr)
    sys.exit(1)


if any(s["key"] == "CraftFleshboundEffigy" for s in chapters["PreHardmode"]["steps"]):
    fail("already applied")

for name, chain in CHAINS.items():
    chapter = chapters[name]
    if name in NEW_STEPS:
        chapter["steps"].append(NEW_STEPS[name])
    bosses = {b["key"]: b for b in chapter["bosses"]}
    steps = {s["key"]: s for s in chapter["steps"]}

    for key, fix in FIXES.get(name, {}).items():
        if key not in steps:
            fail(f"{name}: no step {key}")
        steps[key].update(fix)

    for boss_key, needs, via, boss_after, via_add, note in chain:
        if boss_key not in bosses:
            fail(f"{name}: no boss {boss_key}")
        boss = bosses[boss_key]
        if boss["kind"] != "Gate":
            boss["kind"] = "Required"
        if boss_after is not None:
            boss["after"] = list(boss_after)
            boss["any_of"] = False

        if via is None:
            continue
        if via not in steps:
            fail(f"{name}: no step {via}")
        step = steps[via]
        step["kind"] = "Required"
        if via_add == "anvil":
            step["after"] = needs + ["ObtainMythrilAnvil"]
            step["any_of"] = False
            step["how"] += " Either a Mythril or an Orichalcum Anvil works."
        elif via_add:
            step["after"] += [a for a in via_add if a not in step["after"]]
        if note and needs:
            text = NOTES[note].format(sigils=sigils(needs, " or " if note == "recipe-any" else " and "))
            if text not in step["how"]:
                step["how"] += " " + text

BOSS_TEXT = Path(__file__).resolve().parent / "boss_text.json"
boss_text = json.loads(BOSS_TEXT.read_text(encoding="utf-8"))
BOSS_HOW = {
    "GlowmothDefeated": "Bereft: Silk Cocoons can't be broken unless you carry a King Slime Sigil.",
    "HiveMindDefeated": "Bereft: Hive Tumors can't be hurt unless you carry a World Evil Sigil. Only one of the Hive Mind or the Perforators is needed.",
    "PerforatorsDefeated": "Bereft: Perforator Cysts can't be hurt unless you carry a World Evil Sigil. Only one of the Hive Mind or the Perforators is needed.",
    "PharaohsCurseDefeated": "Bereft: someone nearby must carry a Putrid Pinky Sigil.",
    "ExcavatorDefeated": "Bereft: someone nearby must carry a Slime God Sigil.",
    "AdvisorDefeated": "Bereft: its four tethered Constructs can't be hurt unless you carry an Excavator Sigil.",
    "LuxDefeated": "Bereft: the Chaos Spirit only becomes Lux if someone nearby carries a Ravager Sigil.",
    "AstrumDeusDefeated": "Bereft: someone at the Astral Beacon must carry a Lunatic Cultist Sigil.",
    "ProvidenceDefeated": "Bereft: someone nearby must carry a Dragonfolly Sigil.",
    "PolterghastDefeated": "Bereft: killing Phantom Spirits won't summon it until all three Sentinels are down.",
    "SupremeCalamitasDefeated": "Bereft: someone at the altar must carry a Yharon Sigil.",
}
for key, extra in BOSS_HOW.items():
    if key not in boss_text:
        fail(f"no boss text for {key}")
    if extra not in boss_text[key][1]:
        boss_text[key][1] = (boss_text[key][1] + " " + extra).strip()
boss_text["OldDukeDefeated"][1] = ("Fish in the Sulphurous Sea with a Sigil Bloodworm. Until he's beaten, plain Bloodworms "
                                   "and Acid Rain won't bring him.")
# Match both files' existing format: one-space indent, LF, no final newline.
with BOSS_TEXT.open("w", encoding="utf-8", newline="\n") as f:
    f.write(json.dumps(boss_text, indent=1, ensure_ascii=False))

with PATH.open("w", encoding="utf-8", newline="\n") as f:
    f.write(json.dumps(data, indent=1, ensure_ascii=False))
print("boss chain applied")
