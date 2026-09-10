"""The content tuner: legal candidates only, and the best neighbour wins (data/balance/README.md)."""

from __future__ import annotations

import json
from collections.abc import Mapping
from pathlib import Path

import numpy as np
import pytest

from conftest import evaluation_json
from downfall_learning.artifacts import Evaluation
from downfall_learning.knobs import Content, Knob, Knobs, Objective, Target, load_knobs
from downfall_learning.tune_content import (
    Move,
    TuneOptions,
    apply_moves,
    format_result,
    metrics_of,
    propose,
    tune_content,
    violations,
)

ATTACK = {
    "id": "spell:attack:v1",
    "initiative": 1,
    "energyCost": 2,
    "criticalChance": 0.0,
    "targeting": {"origin": "Enemy", "scope": "SingleTarget", "maxTargets": 1},
    "effects": [{"kind": "Damage", "amount": 3}],
}
JAB = {
    "id": "spell:jab:v1",
    "initiative": 1,
    "energyCost": 1,
    "criticalChance": 0.0,
    "targeting": {"origin": "Enemy", "scope": "SingleTarget", "maxTargets": 1},
    "effects": [{"kind": "Damage", "amount": 1}],
}

DAMAGE = "/effects/0/amount"


def knobs_document(**overrides: object) -> dict:
    document = {
        "version": "knobs:v1",
        "objective": {
            "seeds": "benchmarks/benchmark-seeds.json",
            "evaluations": {"mirror": {"p1": "greedy", "p2": "greedy"}},
            "targets": [
                {"metric": "averageRounds", "on": "mirror", "min": 8, "max": 16, "scale": 3, "weight": 1}
            ],
        },
        "constraints": {"noNewStrictDominance": {"enabled": True}},
        "spells": {
            "spell:attack": {
                "name": "Attack",
                "intent": "The yardstick.",
                "knobs": [{"path": DAMAGE, "min": 1, "max": 6, "step": 1}],
            },
            "spell:jab": {
                "name": "Jab",
                "intent": "The cheap one.",
                "knobs": [{"path": DAMAGE, "min": 1, "max": 6, "step": 1}],
            },
        },
    }
    document.update(overrides)
    return document


def load(tmp_path: Path, **overrides: object) -> Knobs:
    path = tmp_path / "knobs.json"
    path.write_text(json.dumps(knobs_document(**overrides)), encoding="utf-8")
    return load_knobs(path)


def catalogue(tmp_path: Path) -> Content:
    return Content(
        spells={"spell:attack": json.loads(json.dumps(ATTACK)), "spell:jab": json.loads(json.dumps(JAB))},
        files={
            "spell:attack": tmp_path / "Spells" / "base" / "attack.v1.json",
            "spell:jab": tmp_path / "Spells" / "base" / "jab.v1.json",
        },
    )


def move(content: Content, alias: str, path: str, after: float, steps: int = 1) -> Move:
    knob = Knob(spell=alias, path=path, minimum=1, maximum=6, step=1)
    before = float(content.spells[alias]["effects"][0]["amount"])
    return Move(knob=knob, steps=steps, before=before, after=after)


class FakeEvaluator:
    """Scores a catalogue without an engine: rounds fall as the total damage of the catalogue rises."""

    def __init__(self) -> None:
        self.calls = 0

    def evaluate(self, spells: Mapping[str, dict]) -> dict[str, dict[str, float]]:
        self.calls += 1
        damage = sum(spell["effects"][0]["amount"] for spell in spells.values())
        return {"mirror": {"averageRounds": 40.0 - 2.0 * damage}}


def test_a_move_changes_the_candidate_and_leaves_the_content_alone(tmp_path: Path) -> None:
    content = catalogue(tmp_path)

    changed = apply_moves(content.spells, [move(content, "spell:attack", DAMAGE, 4)])

    assert changed["spell:attack"]["effects"][0]["amount"] == 4
    assert content.spells["spell:attack"]["effects"][0]["amount"] == 3


def test_a_candidate_that_would_dominate_another_spell_is_refused(tmp_path: Path) -> None:
    """The cheap jab hitting as hard as the dear attack leaves nothing to choose between them."""
    knobs = load(tmp_path)
    content = catalogue(tmp_path)

    candidate = apply_moves(content.spells, [move(content, "spell:jab", DAMAGE, 3, steps=2)])

    assert violations(content, candidate, knobs) == [
        "spell:jab would become strictly better than spell:attack."
    ]


def test_a_candidate_that_keeps_every_spell_worth_choosing_passes(tmp_path: Path) -> None:
    knobs = load(tmp_path)
    content = catalogue(tmp_path)

    candidate = apply_moves(content.spells, [move(content, "spell:jab", DAMAGE, 2)])

    assert violations(content, candidate, knobs) == []


def test_the_starting_kit_may_not_stop_being_a_choice(tmp_path: Path) -> None:
    knobs = load(
        tmp_path,
        constraints={"startingKitOffersAChoice": {"enabled": True, "spells": ["spell:attack", "spell:jab"]}},
    )
    content = catalogue(tmp_path)

    candidate = apply_moves(content.spells, [move(content, "spell:jab", DAMAGE, 3, steps=2)])

    assert "starting kit" in violations(content, candidate, knobs)[0]


def test_a_proposal_is_legal_before_the_engine_ever_sees_it(tmp_path: Path) -> None:
    knobs = load(tmp_path)
    content = catalogue(tmp_path)
    rng = np.random.default_rng(0)

    for _ in range(20):
        moves = propose(rng, knobs, content, (), TuneOptions())
        assert moves is not None
        assert violations(content, apply_moves(content.spells, moves), knobs) == []


def test_a_proposal_never_moves_more_knobs_than_it_is_allowed(tmp_path: Path) -> None:
    knobs = load(tmp_path)
    content = catalogue(tmp_path)
    rng = np.random.default_rng(1)
    options = TuneOptions(max_changes=1)

    current: tuple[Move, ...] = ()
    for _ in range(10):
        proposed = propose(rng, knobs, content, current, options)
        if proposed is None:
            break
        current = proposed
        assert len({item.knob.key for item in current}) <= 1


def test_the_search_keeps_the_neighbour_that_scores_best(tmp_path: Path) -> None:
    knobs = load(tmp_path)
    content = catalogue(tmp_path)

    result = tune_content(FakeEvaluator(), knobs, content, TuneOptions(iterations=6, neighbours=3, seed=3))

    assert result.improved
    assert result.best.score < result.initial.score
    assert result.best.moves


def test_a_search_that_finds_nothing_says_so_rather_than_proposing_noise(tmp_path: Path) -> None:
    """The band already holds, so every legal neighbour is worse and the content as it stands wins."""
    knobs = load(
        tmp_path,
        objective={
            "seeds": "seeds.json",
            "evaluations": {"mirror": {"p1": "greedy", "p2": "greedy"}},
            "targets": [
                {"metric": "averageRounds", "on": "mirror", "min": 30, "max": 32, "scale": 1, "weight": 1}
            ],
        },
    )
    content = catalogue(tmp_path)

    result = tune_content(FakeEvaluator(), knobs, content, TuneOptions(iterations=3, neighbours=2, seed=0))

    assert not result.improved
    assert result.best.moves == ()


def test_every_candidate_the_search_played_is_kept(tmp_path: Path) -> None:
    knobs = load(tmp_path)
    evaluator = FakeEvaluator()

    options = TuneOptions(iterations=4, neighbours=2, seed=2)
    result = tune_content(evaluator, knobs, catalogue(tmp_path), options)

    assert len(result.candidates) + 1 == evaluator.calls


def test_the_proposal_is_written_as_the_content_tree_it_came_from(tmp_path: Path) -> None:
    knobs = load(tmp_path)
    content = catalogue(tmp_path)
    result = tune_content(FakeEvaluator(), knobs, content, TuneOptions(iterations=4, neighbours=2, seed=3))

    result.write(tmp_path / "out", tmp_path)

    written = json.loads((tmp_path / "out" / "tune.json").read_text())
    assert written["best"]["moves"]
    changed = {move["spell"] for move in written["best"]["moves"]}
    for alias in changed:
        name = Path(content.files[alias]).name
        assert (tmp_path / "out" / "content" / "Spells" / "base" / name).exists()


def test_applying_a_proposal_writes_the_numbers_into_the_content(tmp_path: Path) -> None:
    knobs = load(tmp_path)
    content = catalogue(tmp_path)
    for path in content.files.values():
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(content.spells[next(a for a, p in content.files.items() if p == path)]))
    result = tune_content(FakeEvaluator(), knobs, content, TuneOptions(iterations=4, neighbours=2, seed=3))

    written = result.apply(tmp_path)

    assert written
    for path in written:
        alias = next(a for a, p in content.files.items() if p == path)
        assert json.loads(path.read_text()) == result.spells[alias]


def test_the_share_of_the_spell_leaned_on_and_the_spells_never_cast_come_from_the_outcomes() -> None:
    raw = evaluation_json(0.5, 0.5)
    raw["spellOutcomes"] = [
        {"spell": "spell:attack:v1", "resolved": 30},
        {"spell": "spell:jab:v1", "resolved": 10},
    ]
    evaluation = Evaluation.from_json(raw)

    metrics = metrics_of(evaluation, "mirror", ("spell:attack:v1", "spell:jab:v1", "spell:quiet:v1"))

    assert metrics["spellUsageShare"] == pytest.approx(0.75)
    assert metrics["spellsNeverCast"] == 1


def test_a_run_with_no_landed_cast_reads_as_one_spell_taking_everything() -> None:
    """Zero casts is not balance: scoring it as zero share would make an unplayable catalogue look perfect."""
    raw = evaluation_json(0.5, 0.5)
    raw["spellOutcomes"] = []

    metrics = metrics_of(Evaluation.from_json(raw), "mirror", ("spell:attack:v1",))

    assert metrics["spellUsageShare"] == 1.0
    assert metrics["spellsNeverCast"] == 1


def test_the_proposal_reads_as_what_moved_and_what_it_bought(tmp_path: Path) -> None:
    knobs = load(tmp_path)
    content = catalogue(tmp_path)
    result = tune_content(FakeEvaluator(), knobs, content, TuneOptions(iterations=4, neighbours=2, seed=3))

    text = format_result(result, knobs.objective)

    assert "Score" in text
    assert "mirror.averageRounds" in text
    assert "->" in text


def test_a_metric_the_objective_names_and_nobody_measured_is_listed() -> None:
    objective = Objective(
        seeds="seeds.json",
        evaluations={"mirror": {}},
        targets=(Target(metric="spellUsageShare", on="mirror", maximum=0.25, scale=0.05, weight=2),),
    )

    assert objective.missing({"mirror": {"averageRounds": 12.0}}) == ["mirror.spellUsageShare"]


def test_a_move_that_changes_no_metric_is_named_as_dead_content(tmp_path: Path) -> None:
    """Most of a run on this catalogue lands on spells nobody casts; the report has to say so."""

    class OneSpellMatters:
        calls = 0

        def evaluate(self, spells: Mapping[str, dict]) -> dict[str, dict[str, float]]:
            self.calls += 1
            return {"mirror": {"averageRounds": 40.0 - 2.0 * spells["spell:attack"]["effects"][0]["amount"]}}

    knobs = load(tmp_path)
    result = tune_content(
        OneSpellMatters(), knobs, catalogue(tmp_path), TuneOptions(iterations=6, neighbours=3, seed=5)
    )

    assert result.inert
    assert all(move.knob.spell == "spell:jab" for candidate in result.inert for move in candidate.moves)
    assert "changed no metric at all" in format_result(result, knobs.objective)
