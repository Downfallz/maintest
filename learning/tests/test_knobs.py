"""The balance knobs: bounds, intent, and what the objective calls balanced (data/balance/README.md)."""

from __future__ import annotations

import json
from pathlib import Path

import pytest

from downfall_learning.knobs import (
    KNOBS_FILE,
    Content,
    Knob,
    KnobsError,
    Objective,
    Target,
    dominance,
    dominates,
    findings,
    load_content,
    load_knobs,
    new_dominance,
    read_value,
    validate,
    with_value,
)

REPO_ROOT = Path(__file__).resolve().parents[2]

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
        "spell:twin and spell:attack are one spell under two names: the same cost, "
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
    """The one test that fails when a spell is added, retuned or cut without saying what it is for."""
    knobs = load_knobs(REPO_ROOT / KNOBS_FILE)
    spells = load_content(REPO_ROOT / "data")

    assert validate(knobs, spells) == []
    assert len(spells) == len(knobs.spells)
