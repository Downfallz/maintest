"""The balance knobs: bounds, intent, and what the objective calls balanced (data/balance/README.md)."""

from __future__ import annotations

import json
from pathlib import Path

import pytest

from downfall_learning.knobs import (
    KNOBS_FILE,
    Content,
    Knob,
    Knobs,
    KnobsError,
    Objective,
    SpellKnobs,
    Target,
    cast_value,
    dominance,
    dominates,
    findings,
    load_content,
    load_knobs,
    load_weights,
    new_dominance,
    outclassed,
    read_value,
    twins,
    validate,
    with_value,
)

REPO_ROOT = Path(__file__).resolve().parents[2]
DAMAGE_POINTER = "/effects/0/amount"

ATTACK = {
    "id": "spell:attack:v1",
    "initiative": 1,
    "energyCost": 2,
    "criticalChance": 0.5,
    "targeting": {"origin": "Enemy", "scope": "SingleTarget", "maxTargets": 1},
    "effects": [{"kind": "Damage", "amount": 3}],
}


def spell(**overrides: object) -> dict:
    document = json.loads(json.dumps(ATTACK))
    document.update(overrides)
    return document


def knobs_json(**overrides: object) -> dict:
    document = {
        "version": "knobs:v1",
        "objective": {"seeds": "benchmarks/benchmark-seeds.json", "evaluations": {}, "targets": []},
        "constraints": {},
        "spells": {
            "spell:attack": {
                "name": "Attack",
                "class": "Creature",
                "intent": "The yardstick everything else is priced against.",
                "keep": ["Cheap"],
                "knobs": [{"path": "/effects/0/amount", "min": 1, "max": 5, "step": 1}],
            }
        },
    }
    document.update(overrides)
    return document


def content(**spells: dict) -> Content:
    return Content(spells=spells, files={alias: Path(f"{alias}.json") for alias in spells})


def write_knobs(directory: Path, document: dict) -> Path:
    path = directory / "knobs.json"
    path.write_text(json.dumps(document), encoding="utf-8")
    return path


def test_a_knobs_version_this_package_does_not_know_is_refused(tmp_path: Path) -> None:
    path = write_knobs(tmp_path, knobs_json(version="knobs:v9"))

    with pytest.raises(KnobsError, match="knobs:v9"):
        load_knobs(path)


def test_a_pointer_addresses_a_number_inside_the_effect_list() -> None:
    assert read_value(ATTACK, "/effects/0/amount") == 3
    assert read_value(ATTACK, "/energyCost") == 2


def test_a_pointer_that_addresses_nothing_says_so() -> None:
    with pytest.raises(KnobsError, match="addresses nothing"):
        read_value(ATTACK, "/effects/1/amount")


def test_a_pointer_onto_something_other_than_a_number_is_refused() -> None:
    with pytest.raises(KnobsError, match="not a number"):
        read_value(ATTACK, "/targeting/origin")


def test_writing_a_value_leaves_the_spell_it_was_read_from_alone() -> None:
    changed = with_value(ATTACK, "/effects/0/amount", 5)

    assert changed["effects"][0]["amount"] == 5
    assert ATTACK["effects"][0]["amount"] == 3


def test_a_whole_number_is_written_as_a_whole_number() -> None:
    """The content is authored by hand: a cost of 3.0 in the file would read as a change nobody made."""
    assert with_value(ATTACK, "/energyCost", 3.0)["energyCost"] == 3


def test_a_knob_moves_from_the_value_the_content_carries_not_from_a_grid() -> None:
    critical = Knob(spell="spell:attack", path="/criticalChance", minimum=0.4, maximum=0.8, step=0.05)

    assert critical.moved(0.667, 1) == 0.717
    assert critical.moved(0.667, -1) == 0.617


def test_a_knob_never_leaves_its_bounds() -> None:
    damage = Knob(spell="spell:attack", path="/effects/0/amount", minimum=1, maximum=5, step=1)

    assert damage.moved(5, 3) == 5
    assert damage.moved(1, -3) == 1


def test_the_knobs_and_the_content_agree(tmp_path: Path) -> None:
    knobs = load_knobs(write_knobs(tmp_path, knobs_json()))

    assert validate(knobs, content(**{"spell:attack": ATTACK})) == []


def test_a_spell_with_no_entry_is_a_spell_nobody_decided_the_intent_of(tmp_path: Path) -> None:
    knobs = load_knobs(write_knobs(tmp_path, knobs_json()))

    problems = validate(knobs, content(**{"spell:attack": ATTACK, "spell:other": spell()}))

    assert problems == ["spell:other: enabled content with no entry in the knobs file."]


def test_an_entry_for_a_spell_the_content_does_not_have_is_reported(tmp_path: Path) -> None:
    knobs = load_knobs(write_knobs(tmp_path, knobs_json()))

    problems = validate(knobs, content())

    assert problems == ["spell:attack: a knobs entry for a spell no alias resolves to."]


def test_bounds_that_exclude_the_authored_value_are_reported(tmp_path: Path) -> None:
    document = knobs_json()
    document["spells"]["spell:attack"]["knobs"][0]["max"] = 2
    knobs = load_knobs(write_knobs(tmp_path, document))

    problems = validate(knobs, content(**{"spell:attack": ATTACK}))

    assert problems == ["spell:attack/effects/0/amount: the content carries 3.0, outside [1.0, 2.0]."]


def test_a_pointer_that_addresses_nothing_is_reported_rather_than_raised(tmp_path: Path) -> None:
    document = knobs_json()
    document["spells"]["spell:attack"]["knobs"][0]["path"] = "/effects/2/amount"
    knobs = load_knobs(write_knobs(tmp_path, document))

    assert "addresses nothing" in validate(knobs, content(**{"spell:attack": ATTACK}))[0]


def test_the_same_number_listed_twice_is_reported(tmp_path: Path) -> None:
    document = knobs_json()
    document["spells"]["spell:attack"]["knobs"].append(
        {"path": "/effects/0/amount", "min": 1, "max": 5, "step": 1}
    )
    knobs = load_knobs(write_knobs(tmp_path, document))

    assert "listed twice" in validate(knobs, content(**{"spell:attack": ATTACK}))[0]


def test_more_damage_for_the_same_price_is_strictly_better() -> None:
    assert dominates(spell(effects=[{"kind": "Damage", "amount": 4}]), ATTACK)


def test_the_same_spell_dominates_nothing() -> None:
    """Dominance needs a strict improvement somewhere, otherwise every spell dominates its own twin."""
    assert not dominates(spell(), ATTACK)


def test_a_better_hit_that_costs_more_is_a_decision_not_a_domination() -> None:
    assert not dominates(spell(energyCost=3, effects=[{"kind": "Damage", "amount": 9}]), ATTACK)


def test_an_extra_effect_for_the_same_price_is_strictly_better() -> None:
    stronger = spell(effects=[{"kind": "Damage", "amount": 3}, {"kind": "Stun", "durationRounds": 1}])

    assert dominates(stronger, ATTACK)


def test_healing_allies_never_dominates_hitting_enemies() -> None:
    heal = spell(targeting={"origin": "Ally", "scope": "SingleTarget", "maxTargets": 1})

    assert not dominates(heal, ATTACK)


def test_reaching_more_enemies_for_the_same_price_is_strictly_better() -> None:
    sweep = spell(targeting={"origin": "Enemy", "scope": "Multi", "maxTargets": 3})

    assert dominates(sweep, ATTACK)


def test_a_slower_unlock_is_not_strictly_better_however_hard_it_hits() -> None:
    """Spell initiative is a real reward, so giving it up is a price like any other."""
    later = spell(initiative=0, effects=[{"kind": "Damage", "amount": 9}])

    assert not dominates(later, ATTACK)


def test_only_the_pairs_a_candidate_adds_count_against_it() -> None:
    before = content(
        **{"spell:attack": ATTACK, "spell:better": spell(effects=[{"kind": "Damage", "amount": 4}])}
    )
    after = content(
        **{
            "spell:attack": ATTACK,
            "spell:better": spell(effects=[{"kind": "Damage", "amount": 4}]),
            "spell:third": spell(effects=[{"kind": "Damage", "amount": 5}]),
        }
    )

    assert len(dominance(before)) == 1
    assert ("spell:third", "spell:attack") in new_dominance(before, after)
    assert ("spell:better", "spell:attack") not in new_dominance(before, after)


def test_two_spells_a_match_cannot_tell_apart_are_reported(tmp_path: Path) -> None:
    document = knobs_json(constraints={"noIndistinguishableSpells": {"enabled": True}})
    knobs = load_knobs(write_knobs(tmp_path, document))

    reports = findings(content(**{"spell:attack": ATTACK, "spell:twin": spell(id="spell:twin:v1")}), knobs)

    assert reports == [
        "spell:attack and spell:twin are one spell under two names: the same cost, "
        "Spell initiative, critical chance, targeting and effects."
    ]


def test_a_constraint_that_is_off_reports_nothing(tmp_path: Path) -> None:
    knobs = load_knobs(write_knobs(tmp_path, knobs_json()))

    assert findings(content(**{"spell:attack": ATTACK, "spell:twin": spell()}), knobs) == []


def test_a_metric_inside_its_band_costs_nothing() -> None:
    target = Target(metric="player1WinShare", on="mirror", minimum=0.45, maximum=0.55, scale=0.05, weight=3)

    assert target.penalty(0.5) == 0
    assert target.penalty(0.45) == 0


def test_a_metric_outside_its_band_costs_more_the_further_out_it_is() -> None:
    target = Target(metric="player1WinShare", on="mirror", minimum=0.45, maximum=0.55, scale=0.05, weight=3)

    assert target.penalty(0.6) == pytest.approx(3.0)
    assert target.penalty(0.65) == pytest.approx(12.0)


def test_the_score_adds_up_every_target_the_run_measured() -> None:
    objective = Objective(
        seeds="seeds.json",
        evaluations={},
        targets=(
            Target(metric="player1WinShare", on="mirror", minimum=0.45, maximum=0.55, scale=0.05, weight=3),
            Target(metric="winRateA", on="skill", minimum=0.65, scale=0.1, weight=2),
        ),
    )
    metrics = {"mirror": {"player1WinShare": 0.6}, "skill": {"winRateA": 0.55}}

    assert objective.score(metrics) == pytest.approx(3.0 + 2.0)
    assert objective.missing(metrics) == []


def test_a_target_nobody_measured_is_named_rather_than_scored_as_zero() -> None:
    objective = Objective(
        seeds="seeds.json",
        evaluations={},
        targets=(Target(metric="spellEntropy", on="mirror", minimum=2.5, scale=0.5, weight=1),),
    )

    assert objective.score({"mirror": {}}) == 0
    assert objective.missing({"mirror": {}}) == ["mirror.spellEntropy"]


def test_the_repository_knobs_cover_the_repository_content() -> None:
    """The one test that fails when a spell is added, retuned or cut without saying what it is for.

    Coverage is of the authored catalogue, not of the build: a spell turned off keeps its entry, so that
    turning it back on does not also mean rediscovering what it was for.
    """
    knobs = load_knobs(REPO_ROOT / KNOBS_FILE)
    spells = load_content(REPO_ROOT / "data")

    assert validate(knobs, spells) == []
    assert set(knobs.spells) == set(spells.spells) | spells.disabled


def test_an_entry_for_a_spell_that_is_turned_off_keeps_its_intent(tmp_path: Path) -> None:
    """Disabling content is an authoring act; losing what the spell was for with it would be a cost."""
    knobs = load_knobs(write_knobs(tmp_path, knobs_json()))

    assert validate(knobs, Content(spells={}, files={}, disabled=frozenset({"spell:attack"}))) == []


def test_a_spell_with_no_intent_is_a_spell_nobody_decided_the_point_of(tmp_path: Path) -> None:
    document = knobs_json()
    document["spells"]["spell:attack"]["intent"] = "   "
    knobs = load_knobs(write_knobs(tmp_path, document))

    assert "no intent" in validate(knobs, content(**{"spell:attack": ATTACK}))[0]


def test_bounds_the_wrong_way_round_are_reported(tmp_path: Path) -> None:
    document = knobs_json()
    document["spells"]["spell:attack"]["knobs"][0] = {"path": DAMAGE_POINTER, "min": 5, "max": 1, "step": 1}
    knobs = load_knobs(write_knobs(tmp_path, document))

    assert any(
        "wrong way round" in problem for problem in validate(knobs, content(**{"spell:attack": ATTACK}))
    )


def test_a_step_that_moves_nothing_is_reported(tmp_path: Path) -> None:
    document = knobs_json()
    document["spells"]["spell:attack"]["knobs"][0]["step"] = 0
    knobs = load_knobs(write_knobs(tmp_path, document))

    assert any("moves nothing" in problem for problem in validate(knobs, content(**{"spell:attack": ATTACK})))


def test_a_target_reading_an_evaluation_nobody_plays_is_reported(tmp_path: Path) -> None:
    """The score would quietly be short a term, and only a ten-minute search would have said so."""
    document = knobs_json(
        objective={
            "seeds": "seeds.json",
            "evaluations": {"mirror": {}},
            "targets": [{"metric": "drawRate", "on": "skill", "max": 0.05, "scale": 0.05, "weight": 1}],
        }
    )
    knobs = load_knobs(write_knobs(tmp_path, document))

    problems = validate(knobs, content(**{"spell:attack": ATTACK}))

    assert problems == [
        "objective: target 'skill.drawRate' reads an evaluation the objective does not declare."
    ]


def test_a_starting_kit_naming_a_spell_that_does_not_exist_is_reported(tmp_path: Path) -> None:
    document = knobs_json(
        constraints={"startingKitOffersAChoice": {"enabled": True, "spells": ["spell:ghost"]}}
    )
    knobs = load_knobs(write_knobs(tmp_path, document))

    problems = validate(knobs, content(**{"spell:attack": ATTACK}))

    assert problems == ["startingKitOffersAChoice: 'spell:ghost' is not a spell any alias resolves to."]


def test_a_pointer_without_a_leading_slash_is_not_a_pointer() -> None:
    with pytest.raises(KnobsError, match="not a JSON pointer"):
        read_value(ATTACK, "energyCost")


def test_a_knobs_file_that_is_not_there_says_so(tmp_path: Path) -> None:
    with pytest.raises(KnobsError, match="does not exist"):
        load_knobs(tmp_path / "absent.json")


def test_a_knobs_file_that_is_not_json_says_so(tmp_path: Path) -> None:
    path = tmp_path / "knobs.json"
    path.write_text("{ not json", encoding="utf-8")

    with pytest.raises(KnobsError, match="not valid JSON"):
        load_knobs(path)


def test_writing_through_a_pointer_that_addresses_nothing_is_refused() -> None:
    """It has to fail the way reading does, or the CLI prints a traceback instead of one line."""
    with pytest.raises(KnobsError, match="addresses nothing"):
        with_value(ATTACK, "/effects/5/amount", 3)


def test_two_targets_on_the_same_metric_of_two_evaluations_both_count() -> None:
    objective = Objective(
        seeds="seeds.json",
        evaluations={"mirror": {}, "skill": {}},
        targets=(
            Target(metric="drawRate", on="mirror", maximum=0.05, scale=0.05, weight=1),
            Target(metric="drawRate", on="skill", maximum=0.05, scale=0.05, weight=1),
        ),
    )
    metrics = {"mirror": {"drawRate": 0.15}, "skill": {"drawRate": 0.15}}

    assert sorted(objective.breakdown(metrics)) == ["mirror.drawRate", "skill.drawRate"]
    assert objective.score(metrics) == pytest.approx(8.0)


def test_a_critical_chance_a_match_never_reads_does_not_order_two_spells() -> None:
    """The multiplier applies to damage only, so on a heal it is a number no result can attribute."""
    heal = {
        "id": "spell:heal:v1",
        "initiative": 1,
        "energyCost": 2,
        "criticalChance": 0.5,
        "targeting": {"origin": "Ally", "scope": "SingleTarget", "maxTargets": 1},
        "effects": [{"kind": "Heal", "amount": 3}],
    }
    plain = json.loads(json.dumps(heal))
    plain["criticalChance"] = 0.0

    assert not dominates(heal, plain)
    assert not dominates(plain, heal)


def test_two_hits_of_three_are_not_one_hit_of_six() -> None:
    """The engine compares whole effects, so summing them here would refuse a candidate over nothing."""
    twice = spell(effects=[{"kind": "Damage", "amount": 3}, {"kind": "Damage", "amount": 3}])
    once = spell(effects=[{"kind": "Damage", "amount": 6}])

    assert twins(content(**{"spell:twice": twice, "spell:once": once})) == []


def test_a_critical_chance_on_a_spell_that_deals_no_damage_is_refused(tmp_path: Path) -> None:
    """The multiplier reaches Damage and nothing else, so the knob could only waste the search's budget."""
    heal = {
        "id": "spell:heal:v1",
        "initiative": 1,
        "energyCost": 2,
        "criticalChance": 0.5,
        "targeting": {"origin": "Ally", "scope": "SingleTarget", "maxTargets": 1},
        "effects": [{"kind": "Heal", "amount": 3}],
    }
    document = knobs_json(
        spells={
            "spell:heal": {
                "name": "Heal",
                "intent": "The heal.",
                "knobs": [{"path": "/criticalChance", "min": 0.0, "max": 0.6, "step": 0.05}],
            }
        }
    )
    knobs = load_knobs(write_knobs(tmp_path, document))

    problems = validate(knobs, content(**{"spell:heal": heal}))

    assert problems == [
        "spell:heal/criticalChance: the critical multiplier applies to damage only, and this spell "
        "deals none, so this knob cannot move anything."
    ]


def test_a_critical_chance_on_a_spell_that_deals_damage_is_a_knob_like_any_other(tmp_path: Path) -> None:
    document = knobs_json(
        spells={
            "spell:attack": {
                "name": "Attack",
                "intent": "The yardstick.",
                "knobs": [{"path": "/criticalChance", "min": 0.0, "max": 0.6, "step": 0.05}],
            }
        }
    )
    knobs = load_knobs(write_knobs(tmp_path, document))

    assert validate(knobs, content(**{"spell:attack": ATTACK})) == []


def tiered(**tiers: int) -> Content:
    """Two spells with the same numbers as ATTACK, at the tiers the test names."""
    spells = {alias: spell(id=f"{alias}:v1") for alias in tiers}
    return Content(spells=spells, files=dict.fromkeys(tiers, Path("x.json")), tiers=dict(tiers))


def test_a_deeper_spell_outclassing_a_shallower_one_is_progression(tmp_path: Path) -> None:
    """That is what a talent tree is for: the pick and its prerequisites are what paid for the difference."""
    content = tiered(**{"spell:starter": 0, "spell:unlocked": 1})
    content.spells["spell:unlocked"]["effects"] = [{"kind": "Damage", "amount": 5}]

    assert dominates(content.spells["spell:unlocked"], content.spells["spell:starter"])
    assert content.progression("spell:unlocked", "spell:starter")
    assert dominance(content) == []


def test_two_spells_offered_at_the_same_depth_must_be_a_choice(tmp_path: Path) -> None:
    content = tiered(**{"spell:left": 1, "spell:right": 1})
    content.spells["spell:left"]["effects"] = [{"kind": "Damage", "amount": 5}]

    assert dominance(content) == [("spell:left", "spell:right")]


def test_a_pick_that_buys_a_downgrade_is_reported(tmp_path: Path) -> None:
    """A shallower spell outclassing a deeper one is worse than a tie: the pick costs and gives less."""
    content = tiered(**{"spell:starter": 0, "spell:unlocked": 2})
    content.spells["spell:starter"]["effects"] = [{"kind": "Damage", "amount": 5}]

    assert dominance(content) == [("spell:starter", "spell:unlocked")]


def test_a_spell_no_tree_teaches_keeps_being_compared(tmp_path: Path) -> None:
    """An unknown depth is not read as progression: nothing says the pair was paid for."""
    content = tiered(**{"spell:known": 1})
    content.spells["spell:orphan"] = spell(id="spell:orphan:v1", effects=[{"kind": "Damage", "amount": 1}])

    assert dominance(content) == [("spell:known", "spell:orphan")]


def test_the_repository_tiers_come_from_the_tree_the_creature_is_on() -> None:
    spells = load_content(REPO_ROOT / "data")

    assert spells.tiers["spell:heavy_strike"] == 0
    assert spells.tiers["spell:lightning_bolt"] == 1


WEIGHTS = {
    "damage": 1.0,
    "heal": 0.8,
    "stun": 3.0,
    "bleed": 0.8,
    "defense": 0.5,
    "energy": 0.2,
    "initiative": 0.5,
}


def boxed(alias: str, tier: int, document: dict, *knobs: dict) -> tuple[Content, Knobs]:
    """One spell, its tier and its bounds, as the pair `unreachable` reads."""
    content = Content(
        spells={alias: document},
        files={alias: Path("x.json")},
        tiers={alias: tier},
    )
    entry = SpellKnobs(
        alias=alias,
        name=alias,
        creature_class="Creature",
        intent="Something.",
        keep=(),
        note=None,
        knobs=tuple(Knob(spell=alias, **knob) for knob in knobs),
    )
    knobs_file = Knobs(
        version="knobs:v1",
        objective=Objective(seeds="", evaluations={}, targets=()),
        constraints={},
        spells={alias: entry},
    )
    return content, knobs_file


def test_a_critical_chance_prices_a_hit_the_way_the_resolution_rules_roll_it() -> None:
    """Expected damage is amount x (1 + chance), because a critical doubles and nothing else does."""
    hit = {"criticalChance": 0.5, "effects": [{"kind": "Damage", "amount": 4}]}

    assert cast_value(hit, WEIGHTS) == pytest.approx(6.0)


def test_a_critical_chance_does_not_reach_a_lasting_effect() -> None:
    """`ResolutionRules` multiplies `Damage` and passes every condition through untouched."""
    bleeding = {
        "criticalChance": 1.0,
        "effects": [{"kind": "Bleed", "amountPerRound": 2, "durationRounds": 3}],
    }

    assert cast_value(bleeding, WEIGHTS) == pytest.approx(0.8 * 2 * 3)


def test_a_permanent_condition_is_priced_over_the_rounds_the_scorer_gives_it() -> None:
    """Pinned on `Stun`, which `ActionScorer.ConditionScore` really does price as weight x rounds."""
    permanent = {"effects": [{"kind": "Stun", "permanent": True}]}
    two_rounds = {"effects": [{"kind": "Stun", "durationRounds": 2}]}

    assert cast_value(permanent, WEIGHTS) == pytest.approx(3.0 * 3)
    assert cast_value(two_rounds, WEIGHTS) == pytest.approx(3.0 * 2)


def test_a_spell_whose_whole_box_sits_under_a_rival_is_reported() -> None:
    """The `pummel` case: no move inside its bounds reaches what the spell beside it already carries."""
    content, knobs = boxed(
        "spell:small",
        1,
        spell(id="spell:small:v1", effects=[{"kind": "Damage", "amount": 2}]),
        {"path": DAMAGE_POINTER, "minimum": 1, "maximum": 3, "step": 1},
    )
    content.spells["spell:big"] = spell(id="spell:big:v1", effects=[{"kind": "Damage", "amount": 9}])
    content.tiers["spell:big"] = 1

    assert [report for report in outclassed(content, knobs, WEIGHTS) if "spell:small" in report]


def test_a_spell_whose_box_reaches_past_its_rival_is_not_reported() -> None:
    """The `poison_slash` case: the bounds already contain an answer, so there is nothing to report."""
    content, knobs = boxed(
        "spell:small",
        1,
        spell(id="spell:small:v1", effects=[{"kind": "Damage", "amount": 2}]),
        {"path": DAMAGE_POINTER, "minimum": 1, "maximum": 9, "step": 1},
    )
    content.spells["spell:big"] = spell(id="spell:big:v1", effects=[{"kind": "Damage", "amount": 3}])
    content.tiers["spell:big"] = 1

    assert outclassed(content, knobs, WEIGHTS) == []


def test_being_outclassed_by_something_deeper_in_the_tree_is_not_reported() -> None:
    """Reaching a deeper node cost picks, so being beaten there is what the tree is for."""
    content, knobs = boxed(
        "spell:small",
        0,
        spell(id="spell:small:v1", effects=[{"kind": "Damage", "amount": 2}]),
        {"path": DAMAGE_POINTER, "minimum": 1, "maximum": 3, "step": 1},
    )
    content.spells["spell:deep"] = spell(id="spell:deep:v1", effects=[{"kind": "Damage", "amount": 9}])
    content.tiers["spell:deep"] = 1

    assert outclassed(content, knobs, WEIGHTS) == []


def test_a_spell_that_deals_no_damage_is_left_out_of_the_comparison() -> None:
    """A heal and an attack share no unit, the same reason `tierDamageSpread` skips one -- and this reading
    cannot see the kill a heal denies (ADR 0022), so it would report every healer in the catalogue."""
    content, knobs = boxed(
        "spell:heal",
        1,
        spell(id="spell:heal:v1", criticalChance=0, effects=[{"kind": "Heal", "amount": 3}]),
        {"path": "/effects/0/amount", "minimum": 1, "maximum": 3, "step": 1},
    )
    content.spells["spell:big"] = spell(id="spell:big:v1", effects=[{"kind": "Damage", "amount": 9}])
    content.tiers["spell:big"] = 1

    assert outclassed(content, knobs, WEIGHTS) == []


def test_without_the_agent_weights_the_reading_is_skipped_rather_than_guessed() -> None:
    content, knobs = boxed(
        "spell:small",
        1,
        spell(id="spell:small:v1", effects=[{"kind": "Damage", "amount": 1}]),
        {"path": DAMAGE_POINTER, "minimum": 1, "maximum": 2, "step": 1},
    )
    content.spells["spell:big"] = spell(id="spell:big:v1", effects=[{"kind": "Damage", "amount": 9}])
    content.tiers["spell:big"] = 1

    assert outclassed(content, knobs, {}) == []


def test_the_repository_weights_are_the_nine_the_agents_score_with() -> None:
    """Read rather than restated, so this reading cannot drift from `ScoringWeights.Default`."""
    assert set(load_weights()) == {
        "damage",
        "kill",
        "heal",
        "stun",
        "bleed",
        "defense",
        "energy",
        "risk",
        "initiative",
    }


def test_a_rival_that_deals_no_damage_is_not_the_bar_anything_is_measured_against() -> None:
    """Both sides of the comparison, not only the subject: a heal is no more a yardstick than it is a case."""
    content, knobs = boxed(
        "spell:small",
        1,
        spell(id="spell:small:v1", criticalChance=0, effects=[{"kind": "Damage", "amount": 2}]),
        {"path": DAMAGE_POINTER, "minimum": 1, "maximum": 3, "step": 1},
    )
    content.spells["spell:healer"] = spell(
        id="spell:healer:v1", criticalChance=0, effects=[{"kind": "Heal", "amount": 99}]
    )
    content.tiers["spell:healer"] = 1

    assert outclassed(content, knobs, WEIGHTS) == []


def test_weights_that_cannot_be_read_are_no_weights_at_all(tmp_path: Path) -> None:
    """Missing or broken, the reading that needs them is skipped rather than run on invented numbers."""
    broken = tmp_path / "broken.json"
    broken.write_text("{not json", encoding="utf-8")

    assert load_weights(tmp_path / "absent.json") == {}
    assert load_weights(broken) == {}


def test_a_damage_weight_of_zero_prices_every_hit_at_nothing() -> None:
    """The damage weight is read like the other eight; it only looks like a unit because it is 1.0 today."""
    hit = {"criticalChance": 0.5, "effects": [{"kind": "Damage", "amount": 4}]}

    assert cast_value(hit, WEIGHTS | {"damage": 0.0}) == pytest.approx(0.0)
