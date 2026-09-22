"""The balance knobs: bounds, intent, and what the objective calls balanced (data/balance/README.md)."""

from __future__ import annotations

import json
from dataclasses import replace
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
    panel,
    read_value,
    twins,
    unbounded,
    validate,
    with_value,
)

REPO_ROOT = Path(__file__).resolve().parents[2]
DAMAGE_POINTER = "/effects/0/amount"

# The `initiative` is deliberately still here. Until ADR 0059 it was a spell's acquisition bonus and every
# reading below compared it; now nothing does, and a file left over from before must not be read as content.
# `test_an_initiative_a_stale_file_carries_decides_nothing` is what holds that.
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
    critical = Knob(target="spell:attack", path="/criticalChance", minimum=0.4, maximum=0.8, step=0.05)

    assert critical.moved(0.667, 1) == 0.717
    assert critical.moved(0.667, -1) == 0.617


def test_a_knob_never_leaves_its_bounds() -> None:
    damage = Knob(target="spell:attack", path="/effects/0/amount", minimum=1, maximum=5, step=1)

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


def test_an_initiative_a_stale_file_carries_decides_nothing() -> None:
    """It was a reward a spell gave up, so the comparison priced it. A package pays it now (ADR 0059).

    Both halves of the rule, on one pair: a spell that gives the field up is judged on what is left, and two
    spells that differ only by it are the same spell. A file written before the change still parses, so the
    field can still arrive -- it just decides nothing when it does.
    """
    harder = spell(initiative=0, effects=[{"kind": "Damage", "amount": 9}])

    assert dominates(harder, ATTACK)
    assert twins(content(**{"spell:attack": ATTACK, "spell:twin": spell(initiative=3)})) == [
        frozenset({"spell:attack", "spell:twin"})
    ]


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
        "critical chance, targeting and effects."
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

    assert validate(knobs, spells, root=REPO_ROOT) == []
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


def test_an_evaluation_naming_a_weights_file_that_is_not_there_is_reported(tmp_path: Path) -> None:
    """Otherwise the engine fails one candidate at a time, an hour into a search that had already started."""
    document = knobs_json(
        objective={
            "seeds": "seeds.json",
            "evaluations": {"mirror": {"p1": "heuristic:weights/gone.json", "p2": "greedy"}},
            "targets": [{"metric": "drawRate", "on": "mirror", "max": 0.05, "scale": 0.05, "weight": 1}],
        }
    )
    knobs = load_knobs(write_knobs(tmp_path, document))

    problems = validate(knobs, content(**{"spell:attack": ATTACK}), root=tmp_path)

    assert problems == ["objective: evaluation 'mirror' p1 reads 'weights/gone.json', which is not a file."]


def test_an_evaluation_naming_a_weights_file_that_is_there_is_accepted(tmp_path: Path) -> None:
    (tmp_path / "weights").mkdir()
    (tmp_path / "weights" / "found.json").write_text("{}", encoding="utf-8")
    document = knobs_json(
        objective={
            "seeds": "seeds.json",
            "evaluations": {
                "mirror": {"p1": "heuristic:weights/found.json", "p2": "explore:0.2"},
                "skill": {"p1": "greedy", "p2": "random"},
            },
            "targets": [{"metric": "drawRate", "on": "mirror", "max": 0.05, "scale": 0.05, "weight": 1}],
        }
    )
    knobs = load_knobs(write_knobs(tmp_path, document))

    assert validate(knobs, content(**{"spell:attack": ATTACK}), root=tmp_path) == []


def test_every_agent_of_a_panel_is_checked_and_an_empty_panel_is_refused(tmp_path: Path) -> None:
    """ADR 0052: agent A may be a panel, and a missing file anywhere in it fails the same way."""
    (tmp_path / "weights").mkdir()
    (tmp_path / "weights" / "found.json").write_text("{}", encoding="utf-8")
    document = knobs_json(
        objective={
            "seeds": "seeds.json",
            "evaluations": {
                "exploit": {
                    "p1": ["heuristic:weights/found.json", "heuristic:weights/gone.json"],
                    "p2": "greedy",
                },
                "empty": {"p1": [], "p2": "greedy"},
            },
            "targets": [{"metric": "drawRate", "on": "exploit", "max": 0.05, "scale": 0.05, "weight": 1}],
        }
    )
    knobs = load_knobs(write_knobs(tmp_path, document))

    problems = validate(knobs, content(**{"spell:attack": ATTACK}), root=tmp_path)

    assert problems == [
        "objective: evaluation 'empty' p1 names no agent at all.",
        "objective: evaluation 'exploit' p1 reads 'weights/gone.json', which is not a file.",
    ]


def test_a_panel_on_agent_b_is_refused(tmp_path: Path) -> None:
    """Only agent A is read as a panel, so a list on `p2` would reach the engine as one unknown agent."""
    document = knobs_json(
        objective={
            "seeds": "seeds.json",
            "evaluations": {"exploit": {"p1": "greedy", "p2": ["greedy", "random"]}},
            "targets": [{"metric": "drawRate", "on": "exploit", "max": 0.05, "scale": 0.05, "weight": 1}],
        }
    )
    knobs = load_knobs(write_knobs(tmp_path, document))

    problems = validate(knobs, content(**{"spell:attack": ATTACK}), root=tmp_path)

    assert problems == [
        "objective: evaluation 'exploit' p2 is a list, and only agent A is read as a panel "
        "(ADR 0052): the opponent is what a panel is measured against."
    ]


def test_a_panel_reads_as_the_agents_it_names_and_a_bare_spec_as_one(tmp_path: Path) -> None:
    assert panel({"p1": ["greedy", "random"]}, "p1") == ("greedy", "random")
    assert panel({"p1": "greedy"}, "p1") == ("greedy",)
    assert panel({}, "p1") == ("greedy",)


def test_an_agent_spec_is_read_the_way_the_engine_reads_it(tmp_path: Path) -> None:
    """A reading of its own would check files the engine does not and miss files it does.

    `AgentSpec.Parse` matches the kind case-insensitively and strips a trailing `@version`, the weights
    fingerprint a stamp carries. So `Heuristic:` names a file just as `heuristic:` does, and the version is
    not part of the path.
    """
    (tmp_path / "weights").mkdir()
    (tmp_path / "weights" / "found.json").write_text("{}", encoding="utf-8")
    document = knobs_json(
        objective={
            "seeds": "seeds.json",
            "evaluations": {
                "cased": {"p1": "Heuristic:weights/gone.json", "p2": "greedy"},
                "stamped": {"p1": "heuristic:weights/found.json@deadbeef", "p2": "greedy"},
            },
            "targets": [],
        }
    )
    knobs = load_knobs(write_knobs(tmp_path, document))

    problems = validate(knobs, content(**{"spell:attack": ATTACK}), root=tmp_path)

    assert problems == ["objective: evaluation 'cased' p1 reads 'weights/gone.json', which is not a file."]


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


def test_a_critical_chance_orders_two_heals_that_are_otherwise_the_same() -> None:
    """ADR 0033 put a direct heal under the multiplier, so the chance is a number a result can attribute."""
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

    assert dominates(heal, plain)
    assert not dominates(plain, heal)


def test_a_critical_chance_a_match_never_reads_does_not_order_two_spells() -> None:
    """The one shape left out: the multiplier reaches neither a buff nor the rounds it lasts, so on armour
    the chance is a number no result can attribute and comparing it would refuse a candidate over nothing."""
    armour = {
        "id": "spell:armour:v1",
        "initiative": 1,
        "energyCost": 2,
        "criticalChance": 0.5,
        "targeting": {"origin": "Ally", "scope": "SingleTarget", "maxTargets": 1},
        "effects": [{"kind": "DefenseBuff", "amount": 2, "durationRounds": 2}],
    }
    plain = json.loads(json.dumps(armour))
    plain["criticalChance"] = 0.0

    assert not dominates(armour, plain)
    assert not dominates(plain, armour)


def test_two_hits_of_three_are_not_one_hit_of_six() -> None:
    """The engine compares whole effects, so summing them here would refuse a candidate over nothing."""
    twice = spell(effects=[{"kind": "Damage", "amount": 3}, {"kind": "Damage", "amount": 3}])
    once = spell(effects=[{"kind": "Damage", "amount": 6}])

    assert twins(content(**{"spell:twice": twice, "spell:once": once})) == []


def test_a_critical_chance_on_a_spell_that_neither_damages_nor_heals_is_refused(tmp_path: Path) -> None:
    """The multiplier reaches Damage and nothing else, so the knob could only waste the search's budget."""
    guard = {
        "id": "spell:guard:v1",
        "initiative": 1,
        "energyCost": 2,
        "criticalChance": 0.5,
        "targeting": {"origin": "Ally", "scope": "SingleTarget", "maxTargets": 1},
        "effects": [{"kind": "DefenseBuff", "amount": 2, "durationRounds": 2}],
    }
    document = knobs_json(
        spells={
            "spell:guard": {
                "name": "Guard",
                "intent": "The armour.",
                "knobs": [{"path": "/criticalChance", "min": 0.0, "max": 0.6, "step": 0.05}],
            }
        }
    )
    knobs = load_knobs(write_knobs(tmp_path, document))

    problems = validate(knobs, content(**{"spell:guard": guard}))

    assert problems == [
        "spell:guard/criticalChance: the critical multiplier reaches a target's damage and direct heal, "
        "and this spell does neither, so this knob cannot move anything."
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


def test_a_critical_chance_on_a_spell_that_only_heals_is_a_knob_like_any_other(tmp_path: Path) -> None:
    """ADR 0033: the multiplier reaches a direct heal, so a healer carries the same dial an attacker does."""
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

    assert validate(knobs, content(**{"spell:heal": heal})) == []


def test_a_critical_chance_is_priced_into_a_direct_heal_but_not_a_regeneration(tmp_path: Path) -> None:
    """The same boundary the engine draws: what lands on health now takes the roll, what lasts does not."""
    weights = {"heal": 1.0}
    instant = {"criticalChance": 0.5, "effects": [{"kind": "Heal", "amount": 4}]}
    lasting = {
        "criticalChance": 0.5,
        "effects": [{"kind": "Regeneration", "amountPerRound": 2, "durationRounds": 2}],
    }

    assert cast_value(instant, weights) == pytest.approx(4 * 1.5)
    assert cast_value(lasting, weights) == pytest.approx(4.0)


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


def test_a_spell_sits_below_what_it_requires_even_in_the_same_node() -> None:
    """ADR 0034: a class node holds its opener and both spells behind it, so the node alone is not a tier."""
    spells = load_content(REPO_ROOT / "data")

    assert spells.tiers["spell:summon_minions"] == 2
    assert spells.tiers["spell:revenant_guards"] == 3
    assert spells.tiers["spell:crazed_specter"] == 3


def test_a_spell_sits_at_the_level_of_the_package_that_teaches_it(tmp_path: Path) -> None:
    """The reading, without the repository: a pick buys a package, and its level is what the climb cost."""
    _write_packages(
        tmp_path,
        ["spell:opener", "spell:behind"],
        [
            {"id": "tier:open:v1", "level": 1, "spells": ["spell:opener"]},
            {"id": "tier:deep:v1", "level": 2, "prerequisites": ["tier:open:v1"], "spells": ["spell:behind"]},
        ],
    )

    content = load_content(tmp_path)

    assert content.tiers["spell:opener"] == 1
    assert content.tiers["spell:behind"] == 2
    assert content.packages["spell:opener"] == ("tier:open:v1",)
    assert content.packages["spell:behind"] == ("tier:deep:v1",)


def test_a_starting_spell_sits_at_level_zero_and_belongs_to_no_package(tmp_path: Path) -> None:
    """It is had before anything is chosen, so no pick paid for it and no package sells it."""
    _write_packages(
        tmp_path,
        ["spell:kit", "spell:bought"],
        [{"id": "tier:open:v1", "level": 1, "spells": ["spell:bought"]}],
        starting=["spell:kit"],
    )

    content = load_content(tmp_path)

    assert content.tiers["spell:kit"] == 0
    assert content.packages.get("spell:kit") is None
    assert content.tiers["spell:bought"] == 1


def test_a_spell_two_packages_teach_takes_the_cheaper_level_and_is_grouped_under_both(tmp_path: Path) -> None:
    """Its level is how soon a creature can have it; both packages sell it, so both are read on it."""
    _write_packages(
        tmp_path,
        ["spell:shared"],
        [
            {"id": "tier:cheap:v1", "level": 1, "spells": ["spell:shared"]},
            {"id": "tier:dear:v1", "level": 3, "spells": ["spell:shared"]},
        ],
    )

    content = load_content(tmp_path)

    assert content.tiers["spell:shared"] == 1
    assert content.packages["spell:shared"] == ("tier:cheap:v1", "tier:dear:v1")


def test_a_disabled_package_teaches_nothing(tmp_path: Path) -> None:
    """It left the build (ADR 0015), so nothing it named is acquired and nothing is grouped under it."""
    _write_packages(
        tmp_path,
        ["spell:orphan"],
        [{"id": "tier:off:v1", "level": 1, "spells": ["spell:orphan"], "enabled": False}],
    )

    content = load_content(tmp_path)

    assert content.packages.get("spell:orphan") is None
    assert content.tiers.get("spell:orphan") is None


def test_the_repository_levels_come_from_the_packages(tmp_path: Path) -> None:
    """The shipped catalogue, read through the files a pick actually buys."""
    content = load_content(REPO_ROOT / "data")

    assert content.packages["spell:lightning_bolt"] == ("tier:occultist:v1",)
    assert content.packages["spell:pummel"] == ("tier:brute:v1",)
    assert content.tiers["spell:lightning_bolt"] == content.tiers["spell:pummel"] == 1
    assert content.packages.get("spell:heavy_strike") is None, "a starting spell no package sells"


def test_an_enabled_package_is_read_under_its_unversioned_alias(tmp_path: Path) -> None:
    """A knobs entry names a package the way it names a spell, so a version cut does not orphan the entry."""
    _write_packages(
        tmp_path,
        ["spell:opener"],
        [
            {"id": "tier:open:v1", "level": 1, "spells": ["spell:opener"], "initiativeBonus": 2},
            {"id": "tier:off:v1", "level": 1, "spells": ["spell:opener"], "enabled": False},
        ],
    )

    content = load_content(tmp_path)

    assert set(content.package_documents) == {"tier:open"}
    assert content.package_documents["tier:open"]["initiativeBonus"] == 2
    assert content.file_of("tier:open") == tmp_path / "Tiers" / "open.json"


def test_an_alias_decides_which_version_of_a_package_a_knob_moves(tmp_path: Path) -> None:
    """What the studio writes when it cuts a package's next version: the older file is superseded."""
    _write_packages(tmp_path, ["spell:opener"], [OPEN_PACKAGE])
    (tmp_path / "Tiers" / "open.v2.json").write_text(
        json.dumps({"id": "tier:open:v2", "level": 1, "spells": ["spell:opener"], "initiativeBonus": 4}),
        encoding="utf-8",
    )
    aliases = json.loads((tmp_path / "aliases.json").read_text()) | {"tier:open": "tier:open:v2"}
    (tmp_path / "aliases.json").write_text(json.dumps(aliases), encoding="utf-8")

    content = load_content(tmp_path)

    assert content.package_documents["tier:open"]["id"] == "tier:open:v2"
    assert content.ambiguous_packages == ()


def test_two_enabled_versions_and_no_alias_are_reported_rather_than_picked(tmp_path: Path) -> None:
    _write_packages(tmp_path, ["spell:opener"], [OPEN_PACKAGE])
    (tmp_path / "Tiers" / "open.v2.json").write_text(
        json.dumps({"id": "tier:open:v2", "level": 1, "spells": ["spell:opener"]}), encoding="utf-8"
    )

    content = load_content(tmp_path)

    problems = validate(load_knobs(write_knobs(tmp_path, knobs_json())), content)

    assert "tier:open" not in content.package_documents
    assert any("tier:open: 2 enabled versions" in problem and "no alias" in problem for problem in problems)


def test_an_enabled_package_with_no_entry_is_a_number_nobody_decided_the_intent_of(tmp_path: Path) -> None:
    knobs = load_knobs(write_knobs(tmp_path, knobs_json()))

    problems = validate(knobs, packaged(initiativeBonus=2))

    assert "tier:open: an enabled package with no entry in the knobs file." in problems


def test_a_package_knob_on_anything_but_its_initiative_bonus_is_refused(tmp_path: Path) -> None:
    """Its level, prerequisites and spells are the progression: moving them would be redesigning it."""
    entry = package_entry(knobs=[{"path": "/level", "min": 1, "max": 3, "step": 1}])
    knobs = load_knobs(write_knobs(tmp_path, knobs_json(packages={"tier:open": entry})))

    problems = validate(knobs, packaged(initiativeBonus=2))

    assert any("tier:open/level" in problem and "identity" in problem for problem in problems)


def test_a_package_bonus_outside_its_own_bounds_is_reported(tmp_path: Path) -> None:
    knobs = load_knobs(write_knobs(tmp_path, knobs_json(packages={"tier:open": package_entry()})))

    problems = validate(knobs, packaged(initiativeBonus=9))

    assert "tier:open/initiativeBonus: the content carries 9.0, outside [0.0, 4.0]." in problems


def test_a_package_knob_is_one_of_the_knobs_a_search_can_move(tmp_path: Path) -> None:
    knobs = load_knobs(write_knobs(tmp_path, knobs_json(packages={"tier:open": package_entry()})))

    assert "tier:open/initiativeBonus" in {knob.key for knob in knobs}
    assert validate(knobs, packaged(initiativeBonus=2)) == []


def test_the_repository_knobs_cover_every_package_the_repository_sells() -> None:
    content = load_content(REPO_ROOT / "data")
    knobs = load_knobs(REPO_ROOT / "data" / "balance" / "knobs.json")

    assert set(knobs.packages) == set(content.package_documents)
    assert len(content.package_documents) == 21


OPEN_PACKAGE = {"id": "tier:open:v1", "level": 1, "spells": ["spell:opener"]}


def package_entry(**overrides: object) -> dict:
    return {
        "name": "Open",
        "intent": "The opener.",
        "knobs": [{"path": "/initiativeBonus", "min": 0, "max": 4, "step": 1}],
    } | overrides


def packaged(**document: object) -> Content:
    """The one-spell test catalogue with one package selling that spell."""
    base = content(**{"spell:attack": ATTACK})
    package = {"id": "tier:open:v1", "level": 1, "spells": ["spell:attack:v1"]} | document
    return replace(
        base,
        package_documents={"tier:open": package},
        package_files={"tier:open": Path("Tiers/open.v1.json")},
    )


def _write_packages(
    root: Path, spells: list[str], packages: list[dict], starting: list[str] | None = None
) -> None:
    """A data directory of spells, the packages that teach them, and one creature to hold a starting kit."""
    (root / "Spells").mkdir(parents=True)
    (root / "Tiers").mkdir(parents=True)
    (root / "Creatures").mkdir(parents=True)
    aliases = {}
    for alias in spells:
        identifier = f"{alias}:v1"
        aliases[alias] = identifier
        (root / "Spells" / f"{alias.split(':')[1]}.json").write_text(
            json.dumps({**spell(id=identifier), "enabled": True}), encoding="utf-8"
        )
    (root / "aliases.json").write_text(json.dumps(aliases), encoding="utf-8")
    for package in packages:
        (root / "Tiers" / f"{str(package['id']).split(':')[1]}.json").write_text(
            json.dumps(package), encoding="utf-8"
        )
    (root / "Creatures" / "one.json").write_text(
        json.dumps({"id": "creature:one:v1", "startingSpellIds": starting or []}), encoding="utf-8"
    )


def _write_tree(root: Path, entries: list[dict]) -> None:
    """A data directory holding one talent tree of one node, and the spells that node offers."""
    (root / "TalentTrees").mkdir(parents=True)
    (root / "Spells").mkdir(parents=True)
    aliases = {}
    for entry in entries:
        identifier = str(entry["id"])
        alias = identifier.rsplit(":", 1)[0]
        aliases[alias] = identifier
        (root / "Spells" / f"{alias.split(':')[1]}.json").write_text(
            json.dumps({**spell(id=identifier), "enabled": True}), encoding="utf-8"
        )
    (root / "aliases.json").write_text(json.dumps(aliases), encoding="utf-8")
    (root / "TalentTrees" / "tree.json").write_text(
        json.dumps({"id": "talent-tree:test:v1", "root": {"name": "Root", "spells": entries}}),
        encoding="utf-8",
    )


WEIGHTS = {
    "damage": 1.0,
    "heal": 0.8,
    "stun": 3.0,
    "bleed": 0.8,
    "defense": 0.5,
    "energy": 0.2,
    "initiative": 0.5,
}


ALLY = {"origin": "Ally", "scope": "Multi", "maxTargets": 3}


def test_a_harmful_effect_aimed_at_a_friend_is_a_price_and_not_a_gift() -> None:
    """`noxious_cure` slows the team it heals. The targeting origin is the only thing that says whether a
    stun or a debuff is the point of the spell or what it costs."""
    heal = spell(criticalChance=0, targeting=ALLY, effects=[{"kind": "Heal", "amount": 4}])
    heal_and_slow = spell(
        criticalChance=0,
        targeting=ALLY,
        effects=[
            {"kind": "Heal", "amount": 4},
            {"kind": "InitiativeDebuff", "amount": 2, "durationRounds": 1},
        ],
    )

    assert cast_value(heal_and_slow, WEIGHTS) < cast_value(heal, WEIGHTS)
    assert cast_value(heal_and_slow, WEIGHTS) == pytest.approx(((0.8 * 4) - (0.5 * 2 * 1)) * 3)


def test_a_drain_and_a_shred_are_harmful_and_priced_by_the_weight_of_what_they_move() -> None:
    """ADR 0035 prices the two new kinds with the weights that already exist: the shred like the buff it
    mirrors, and the drain like the gain. On a friend both are a price, the same as any other harmful kind."""
    draining = spell(criticalChance=0, effects=[{"kind": "EnergyDrain", "amount": 2}])
    shredding = spell(criticalChance=0, effects=[{"kind": "DefenseDebuff", "amount": 2, "durationRounds": 3}])
    healing_and_shredding = spell(
        criticalChance=0,
        targeting=ALLY,
        effects=[
            {"kind": "Heal", "amount": 4},
            {"kind": "DefenseDebuff", "amount": 2, "durationRounds": 1},
        ],
    )

    assert cast_value(draining, WEIGHTS) == pytest.approx(0.2 * 2)
    assert cast_value(shredding, WEIGHTS) == pytest.approx(0.5 * 2 * 3)
    assert cast_value(healing_and_shredding, WEIGHTS) == pytest.approx(((0.8 * 4) - (0.5 * 2 * 1)) * 3)


def _authored_kinds() -> list[str]:
    """Every effect kind `data/README.md` tells an author they may write, read out of its own table.

    The table is the contract the content is authored against, so it is the list to check against rather
    than a copy kept here -- a copy would need the same update the code needs, which is the failure this
    guards.
    """
    rows = (REPO_ROOT / "data" / "README.md").read_text(encoding="utf-8").splitlines()
    kinds = []
    for row in rows:
        if row.startswith("| `") and "`" in row[3:]:
            kind = row[3:].split("`", 1)[0]
            if kind not in {"kind", "stacking"}:
                kinds.append(kind)
    return kinds


@pytest.mark.parametrize("kind", _authored_kinds())
def test_every_authored_effect_kind_is_priced_by_cast_value(kind: str) -> None:
    """Both pricing tables end in `.get(kind, 0.0)`, so a kind nobody adds there is worth **zero** and every
    knob note, `check-knobs` finding and `tune-content` score silently misvalues the spell that uses it. The
    C# side has two reflection guards for exactly this (`FeatureSchemaTests`, the scorer's duration sweep);
    this is the one on this side."""
    hit = {"kind": "Damage", "amount": 3}
    effect = {"kind": kind, "amount": 3, "amountPerRound": 3, "durationRounds": 2}

    on_target = cast_value({"criticalChance": 0, "effects": [effect]}, WEIGHTS)
    on_caster = cast_value({"criticalChance": 0, "effects": [hit], "casterEffects": [effect]}, WEIGHTS)
    plain = cast_value({"criticalChance": 0, "effects": [hit]}, WEIGHTS)

    assert on_target != 0, f"'{kind}' prices zero as a target effect"
    assert on_caster != plain, f"'{kind}' prices zero as a caster effect (ADR 0031)"


def test_an_initiative_buff_is_priced_like_the_debuff_it_mirrors() -> None:
    """One price for one point whether it is given or taken (ADR 0032, ADR 0036). Both spells reach three, so
    the only thing left between them is the direction, and the reading is the same either way."""
    enemies = {"origin": "Enemy", "scope": "Multi", "maxTargets": 3}
    hasting = spell(
        criticalChance=0,
        targeting=ALLY,
        effects=[{"kind": "InitiativeBuff", "amount": 2, "durationRounds": 1}],
    )
    slowing = spell(
        criticalChance=0,
        targeting=enemies,
        effects=[{"kind": "InitiativeDebuff", "amount": 2, "durationRounds": 1}],
    )

    assert cast_value(hasting, WEIGHTS) == pytest.approx(0.5 * 2 * 1 * 3)
    assert cast_value(slowing, WEIGHTS) == pytest.approx(cast_value(hasting, WEIGHTS))


def test_a_critical_chance_does_not_reach_a_drain_or_a_shred() -> None:
    """Neither is health on a target now, so neither takes the roll (ADR 0033, ADR 0035)."""
    draining = {"criticalChance": 1.0, "effects": [{"kind": "EnergyDrain", "amount": 2}]}
    shredding = {
        "criticalChance": 1.0,
        "effects": [{"kind": "DefenseDebuff", "amount": 2, "durationRounds": 1}],
    }

    assert cast_value(draining, WEIGHTS) == pytest.approx(0.2 * 2)
    assert cast_value(shredding, WEIGHTS) == pytest.approx(0.5 * 2 * 1)


def test_a_harmful_effect_aimed_at_an_enemy_is_still_the_point_of_the_spell() -> None:
    """The other side of the rule: on an enemy the same debuff is what the spell is for."""
    hit = spell(criticalChance=0, effects=[{"kind": "Damage", "amount": 3}])
    hit_and_slow = spell(
        criticalChance=0,
        effects=[
            {"kind": "Damage", "amount": 3},
            {"kind": "InitiativeDebuff", "amount": 2, "durationRounds": 1},
        ],
    )

    assert cast_value(hit_and_slow, WEIGHTS) > cast_value(hit, WEIGHTS)
    assert dominates(hit_and_slow, hit)


def test_slowing_the_allies_you_heal_does_not_dominate_healing_them_cleanly() -> None:
    """Read unsigned, the debuff was an extra effect for free and this pair read as a strict domination --
    a cost mistaken for a gift, which `noNewStrictDominance` would have refused a candidate over."""
    clean = spell(criticalChance=0, targeting=ALLY, effects=[{"kind": "Heal", "amount": 4}])
    noxious = spell(
        criticalChance=0,
        targeting=ALLY,
        effects=[
            {"kind": "Heal", "amount": 4},
            {"kind": "InitiativeDebuff", "amount": 2, "durationRounds": 1},
        ],
    )

    assert not dominates(noxious, clean)
    assert dominates(clean, noxious)


def test_the_ceiling_of_a_debuff_on_a_friend_is_the_smallest_it_may_be() -> None:
    """The corner best for the spell is the floor of a price, whichever list the price sits in: sent to its
    maximum the ceiling counts the largest cost and understates what the spell can reach."""
    content, knobs = boxed(
        "spell:cure",
        1,
        spell(
            id="spell:cure:v1",
            criticalChance=0,
            targeting=ALLY,
            effects=[
                {"kind": "Heal", "amount": 4},
                {"kind": "InitiativeDebuff", "amount": 2, "durationRounds": 1},
            ],
        ),
        {"path": "/effects/1/amount", "minimum": 1, "maximum": 3, "step": 1},
    )
    content.spells["spell:rival"] = spell(
        criticalChance=0, targeting=ALLY, effects=[{"kind": "Heal", "amount": 3}]
    )
    content.tiers["spell:rival"] = 1

    # The rival carries 7.20. The cure reaches 8.10 with the smallest debuff its bounds allow and only 5.10
    # with the largest, so this passes if and only if the ceiling is read at the floor of the price.
    assert outclassed(content, knobs, WEIGHTS) == []


def test_an_effect_on_the_caster_is_worth_what_it_does_to_the_caster() -> None:
    """ADR 0031. A heal on whoever cast the spell is a gift and counts for; a recoil is a price and counts
    against. Read once per cast and never multiplied by the critical chance."""
    plain = spell()
    draining = spell(casterEffects=[{"kind": "Heal", "amount": 2}])
    recoiling = spell(casterEffects=[{"kind": "Damage", "amount": 2}])

    assert cast_value(draining, WEIGHTS) == pytest.approx(cast_value(plain, WEIGHTS) + (0.8 * 2))
    assert cast_value(recoiling, WEIGHTS) == pytest.approx(cast_value(plain, WEIGHTS) - 2)


def test_two_spells_that_differ_only_on_their_caster_are_not_twins() -> None:
    """A match tells them apart, and the engine's `ContentAudit.Signature` reads both halves. This has to
    agree with it: under `noIndistinguishableSpells` a false pair is a finding, and `tune-content` refuses a
    candidate for adding one."""
    pairs = twins(
        content(
            **{
                "spell:plain": spell(),
                "spell:draining": spell(casterEffects=[{"kind": "Heal", "amount": 2}]),
            }
        )
    )

    assert pairs == []


def test_two_caster_effects_of_one_kind_are_priced_one_at_a_time() -> None:
    """Grouping them first multiplies the sums: two bleeds of 1 over 2 rounds and 3 over 4 are 1x2 + 3x4,
    not (1+3) x (2+4)."""
    plain = spell()
    bleeding = spell(
        casterEffects=[
            {"kind": "Bleed", "amountPerRound": 1, "durationRounds": 2},
            {"kind": "Bleed", "amountPerRound": 3, "durationRounds": 4},
        ]
    )

    assert cast_value(bleeding, WEIGHTS) == pytest.approx(
        cast_value(plain, WEIGHTS) - (0.8 * ((1 * 2) + (3 * 4)))
    )


def test_a_spell_that_pays_a_price_on_its_caster_does_not_dominate_one_that_does_not() -> None:
    """Every other axis alike, the recoil is a cost the other spell never pays. Read as an unsigned extra
    effect it would have read as the better spell."""
    recoiling = spell(casterEffects=[{"kind": "Damage", "amount": 2}])

    assert not dominates(recoiling, ATTACK)
    assert dominates(ATTACK, recoiling)


def test_a_spell_that_also_rewards_its_caster_is_strictly_better() -> None:
    assert dominates(spell(casterEffects=[{"kind": "Heal", "amount": 2}]), ATTACK)


def test_healing_the_caster_is_not_the_same_spell_as_healing_the_target() -> None:
    """The two are different axes: neither dominates the other for carrying more of its own."""
    on_caster = spell(casterEffects=[{"kind": "Heal", "amount": 2}])
    on_target = spell(effects=[{"kind": "Damage", "amount": 3}, {"kind": "Heal", "amount": 2}])

    assert not dominates(on_caster, on_target)
    assert not dominates(on_target, on_caster)


def test_the_ceiling_of_a_price_on_the_caster_is_the_smallest_it_may_be() -> None:
    """A harmful caster effect is subtracted, so the corner that is best for the spell is its minimum. Sent
    to its maximum the ceiling would be understated, which is how this check invents a finding."""
    content, knobs = boxed(
        "spell:recoil",
        1,
        spell(id="spell:recoil:v1", casterEffects=[{"kind": "Damage", "amount": 3}]),
        {"path": "/casterEffects/0/amount", "minimum": 1, "maximum": 5, "step": 1},
    )
    content.spells["spell:rival"] = spell(id="spell:rival:v1", effects=[{"kind": "Damage", "amount": 3}])
    content.tiers["spell:rival"] = 1

    # The rival is the same hit with no recoil, so the box reaches a recoil of 1 and stops one short of it.
    assert [report for report in outclassed(content, knobs, WEIGHTS) if "spell:recoil" in report]


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
        knobs=tuple(Knob(target=alias, **knob) for knob in knobs),
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


def test_a_costlier_rival_is_read_over_the_rounds_it_takes_to_pay_for() -> None:
    """Energy carries between rounds, so a spell at three energy comes up twice in three rounds. Read a cast
    at a time, `enraged_charge` at 12.60 reported `protective_slam` as never a choice; read a round, it is
    8.40 against the slam's 7.33 and there is nothing to report."""
    content, knobs = boxed(
        "spell:cheap",
        2,
        spell(id="spell:cheap:v1", energyCost=2, effects=[{"kind": "Damage", "amount": 4}]),
        {"path": DAMAGE_POINTER, "minimum": 1, "maximum": 5, "step": 1},
    )
    content.spells["spell:dear"] = spell(
        id="spell:dear:v1", energyCost=3, effects=[{"kind": "Damage", "amount": 7}]
    )
    content.tiers["spell:dear"] = 2

    assert outclassed(content, knobs, WEIGHTS) == []


def test_a_cheaper_rival_is_still_the_bar_when_it_wins_a_round_at_a_time() -> None:
    """Dividing rather than skipping costlier rivals is what keeps the case this check was written for:
    `pummel` at one energy really is outclassed by `lightning_bolt` at two, 5.15 a round against 6.47."""
    content, knobs = boxed(
        "spell:cheap",
        1,
        spell(id="spell:cheap:v1", energyCost=1, effects=[{"kind": "Damage", "amount": 2}]),
        {"path": DAMAGE_POINTER, "minimum": 1, "maximum": 3, "step": 1},
    )
    content.spells["spell:dear"] = spell(
        id="spell:dear:v1", energyCost=2, effects=[{"kind": "Damage", "amount": 6}]
    )
    content.tiers["spell:dear"] = 1

    assert [report for report in outclassed(content, knobs, WEIGHTS) if "spell:cheap" in report]


def test_a_defensive_spell_whose_box_sits_under_a_defensive_rival_is_reported() -> None:
    """What this reading misses about a defensive spell -- the kill it denies, the threat it is priced
    against -- it misses on both sides of a defensive pair, so the comparison holds there. Skipping them
    outright left `momentum` and `summon_minions` invisible at 0 casts in 400 matches."""
    content, knobs = boxed(
        "spell:trickle",
        1,
        spell(id="spell:trickle:v1", criticalChance=0, effects=[{"kind": "EnergyGain", "amount": 1}]),
        {"path": "/effects/0/amount", "minimum": 1, "maximum": 2, "step": 1},
    )
    content.spells["spell:ward"] = spell(
        id="spell:ward:v1",
        criticalChance=0,
        effects=[{"kind": "DefenseBuff", "amount": 2, "durationRounds": 3}],
    )
    content.tiers["spell:ward"] = 1

    assert [report for report in outclassed(content, knobs, WEIGHTS) if "spell:trickle" in report]


def test_a_defensive_spell_is_not_measured_against_an_attack() -> None:
    """A heal and an attack share no unit, so the miss does not cancel across that line: every healer in the
    catalogue would be reported, and `wait` is *meant* to stay worse than acting."""
    content, knobs = boxed(
        "spell:trickle",
        1,
        spell(id="spell:trickle:v1", criticalChance=0, effects=[{"kind": "EnergyGain", "amount": 1}]),
        {"path": "/effects/0/amount", "minimum": 1, "maximum": 2, "step": 1},
    )
    content.spells["spell:big"] = spell(id="spell:big:v1", effects=[{"kind": "Damage", "amount": 9}])
    content.tiers["spell:big"] = 1

    assert outclassed(content, knobs, WEIGHTS) == []


def test_a_cast_is_priced_for_every_target_it_reaches() -> None:
    """`ActionScorer` sums a resolution over its targets, so a sweep is worth several of the same hit.

    Read as one target, `meteor` priced at a third of what it plays and was the strongest spell in the
    catalogue with every check green.
    """
    single = spell(targeting={"origin": "Enemy", "scope": "SingleTarget", "maxTargets": 1})
    sweep = spell(targeting={"origin": "Enemy", "scope": "Multi", "maxTargets": 3})

    assert cast_value(sweep, WEIGHTS) == pytest.approx(3 * cast_value(single, WEIGHTS))


def test_a_sweep_is_not_the_bar_a_single_target_spell_has_to_match() -> None:
    """A single-target spell cannot reach what a sweep does without ceasing to be single-target, which is
    `dominates`'s axis and not this one. Without the rule this reports `protective_slam` as never a choice on
    content that casts it 354 times in 400 matches."""
    content, knobs = boxed(
        "spell:single",
        2,
        spell(id="spell:single:v1", effects=[{"kind": "Damage", "amount": 4}]),
        {"path": DAMAGE_POINTER, "minimum": 1, "maximum": 5, "step": 1},
    )
    content.spells["spell:sweep"] = spell(
        id="spell:sweep:v1",
        effects=[{"kind": "Damage", "amount": 4}],
        targeting={"origin": "Enemy", "scope": "Multi", "maxTargets": 3},
    )
    content.tiers["spell:sweep"] = 2

    assert outclassed(content, knobs, WEIGHTS) == []


def test_a_permanent_effect_that_costs_nothing_is_reported_as_unbounded() -> None:
    """`full_plate`: free, never expires, stacks on every re-cast. No single-cast reading can see the stack,
    so a bigger permanent horizon would not find it -- the brake is the price."""
    content = Content(
        spells={
            "spell:free": spell(
                energyCost=0, effects=[{"kind": "DefenseBuff", "amount": 1, "permanent": True}]
            )
        },
        files={"spell:free": Path("x.json")},
        tiers={"spell:free": 2},
    )

    assert [report for report in unbounded(content) if "spell:free" in report]


def test_a_permanent_effect_with_a_price_on_it_is_not_reported() -> None:
    """The two energy a round pays is the brake, so `guard` and `thundering_seal` are not this finding."""
    content = Content(
        spells={
            "spell:paid": spell(
                energyCost=1, effects=[{"kind": "DefenseBuff", "amount": 1, "permanent": True}]
            )
        },
        files={"spell:paid": Path("x.json")},
        tiers={"spell:paid": 2},
    )

    assert unbounded(content) == []


def test_a_free_permanent_effect_that_cannot_stack_is_not_reported() -> None:
    """Permanence alone is not a stack: under `Refresh` a re-cast restarts the condition that is already
    there and under `Ignore` it is refused, so the creature carries one of them however often it casts."""
    for policy in ("Refresh", "Ignore"):
        content = Content(
            spells={
                "spell:bounded": spell(
                    energyCost=0,
                    effects=[{"kind": "DefenseBuff", "amount": 1, "permanent": True, "stacking": policy}],
                )
            },
            files={"spell:bounded": Path("x.json")},
            tiers={"spell:bounded": 2},
        )

        assert unbounded(content) == [], policy


def test_a_free_spell_that_lasts_no_time_is_not_reported() -> None:
    """`wait` is free and re-castable and bounded anyway: what it gives is spent."""
    content = Content(
        spells={"spell:wait": spell(energyCost=0, effects=[{"kind": "EnergyGain", "amount": 2}])},
        files={"spell:wait": Path("x.json")},
        tiers={"spell:wait": 0},
    )

    assert unbounded(content) == []


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
        "initiative",
        "pressure",
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
