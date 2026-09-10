"""The content tuner: legal candidates only, and the best neighbour wins (data/balance/README.md)."""

from __future__ import annotations

import json
import sys
from collections.abc import Mapping
from dataclasses import replace
from pathlib import Path

import numpy as np
import pytest

from conftest import evaluation_json
from downfall_learning.artifacts import Evaluation
from downfall_learning.knobs import Content, Knob, Knobs, Objective, Target, load_knobs
from downfall_learning.search_weights import EngineCommand, EvaluationError
from downfall_learning.tune_content import (
    ContentEngine,
    EngineContentEvaluator,
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
    """Starting from a proposal that already spent the budget, the only legal move is on the same knob."""
    knobs = load(tmp_path)
    content = catalogue(tmp_path)
    rng = np.random.default_rng(1)
    options = TuneOptions(max_changes=1)
    spent: tuple[Move, ...] = (move(content, "spell:jab", DAMAGE, 2),)

    for _ in range(20):
        proposed = propose(rng, knobs, content, spent, options)
        assert proposed is not None
        assert {item.knob.key for item in proposed} <= {"spell:jab/effects/0/amount"}


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

    written = result.apply()

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


def test_a_proposal_never_plays_a_catalogue_it_is_already_playing(tmp_path: Path) -> None:
    """A knob pinned at a bound would otherwise keep counting steps and pay for the same catalogue twice."""
    knobs = load(tmp_path)
    content = catalogue(tmp_path)
    rng = np.random.default_rng(4)
    pinned = (move(content, "spell:jab", DAMAGE, 1, steps=-2),)

    for _ in range(20):
        proposed = propose(rng, knobs, content, pinned, TuneOptions(max_changes=1))
        if proposed is None:
            break
        values = {item.knob.key: item.after for item in proposed}
        assert values != {item.knob.key: item.after for item in pinned}
        assert all(abs(item.steps) <= 5 for item in proposed)


def test_a_move_that_stays_inside_every_band_is_not_dead_content(tmp_path: Path) -> None:
    """Every candidate on target scores zero; reading that as 'changed nothing' would libel the search."""

    class InsideTheBand:
        def evaluate(self, spells: Mapping[str, dict]) -> dict[str, dict[str, float]]:
            damage = sum(spell["effects"][0]["amount"] for spell in spells.values())
            return {"mirror": {"averageRounds": 10.0 + 0.5 * damage}}

    knobs = load(tmp_path)
    result = tune_content(
        InsideTheBand(), knobs, catalogue(tmp_path), TuneOptions(iterations=4, neighbours=2, seed=6)
    )

    assert result.candidates
    assert all(candidate.score == 0 for candidate in result.candidates)
    assert result.inert == ()


FAKE_BUILDER = """
import json
import shutil
import sys
from pathlib import Path

data = Path(sys.argv[1])
out = Path(sys.argv[2])
out.mkdir(parents=True, exist_ok=True)
spells = sorted(str(path.relative_to(data)) for path in (data / "Spells").rglob("*.json"))
amounts = {
    json.loads((data / name).read_text())["id"]: json.loads((data / name).read_text())["effects"][0]["amount"]
    for name in spells
}
(out / "game.schema.json").write_text(json.dumps({"spells": spells, "amounts": amounts}))
shutil.copy(out / "game.schema.json", out / "seen.json")
"""

FAKE_ENGINE = """
import json
import sys
from pathlib import Path

options = dict(zip(sys.argv[2::2], sys.argv[3::2]))
schema = json.loads(Path(options["--schema"]).read_text())
log = Path(__file__).with_name("seen.jsonl")
with log.open("a") as file:
    file.write(json.dumps(schema["amounts"]) + "\\n")
evaluation = json.loads(Path(__file__).with_name("template.json").read_text())
evaluation["averageRounds"] = 40.0 - 2.0 * sum(schema["amounts"].values())
Path(options["--out"]).write_text(json.dumps(evaluation))
"""


def content_tree(root: Path) -> Content:
    """A two-spell content directory on disk, as the data builder would read it."""
    spells = {"spell:attack": json.loads(json.dumps(ATTACK)), "spell:jab": json.loads(json.dumps(JAB))}
    files = {}
    for alias, document in spells.items():
        path = root / "Spells" / "base" / f"{alias.split(':')[1]}.v1.json"
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(document, indent=2) + "\n", encoding="utf-8")
        files[alias] = path
    (root / "aliases.json").write_text(json.dumps({a: s["id"] for a, s in spells.items()}), encoding="utf-8")
    return Content(spells=spells, files=files)


@pytest.fixture
def engine_evaluator(tmp_path: Path) -> tuple[EngineContentEvaluator, Content, Path]:
    builder = tmp_path / "fake_builder.py"
    builder.write_text(FAKE_BUILDER, encoding="utf-8")
    engine = tmp_path / "fake_engine.py"
    engine.write_text(FAKE_ENGINE, encoding="utf-8")
    (tmp_path / "template.json").write_text(json.dumps(evaluation_json(0.5, 0.5)), encoding="utf-8")
    data = tmp_path / "data"
    data.mkdir()
    content = content_tree(data)
    objective = Objective(
        seeds="seeds.json",
        evaluations={"mirror": {"p1": "greedy", "p2": "greedy"}},
        targets=(Target(metric="averageRounds", on="mirror", minimum=8, maximum=16, scale=3, weight=1),),
    )
    host = ContentEngine(
        engine=replace(EngineCommand(root=tmp_path), command=(sys.executable, str(engine))),
        data=data,
        workdir=tmp_path / "work",
        builder=(sys.executable, str(builder)),
    )
    return EngineContentEvaluator(host, objective, content), content, data


def test_playing_a_candidate_never_writes_to_the_content(
    engine_evaluator: tuple[EngineContentEvaluator, Content, Path],
) -> None:
    evaluator, content, data = engine_evaluator
    before = {path: path.read_text() for path in content.files.values()}

    evaluator.evaluate(apply_moves(content.spells, [move(content, "spell:attack", DAMAGE, 5, steps=2)]))

    assert {path: path.read_text() for path in content.files.values()} == before
    assert not (data / "dst").exists()


def test_a_candidate_never_inherits_the_numbers_of_the_one_before_it(
    engine_evaluator: tuple[EngineContentEvaluator, Content, Path],
) -> None:
    """Every spell is rewritten from the original, so a move is not still there on the next candidate."""
    evaluator, content, data = engine_evaluator

    evaluator.evaluate(apply_moves(content.spells, [move(content, "spell:attack", DAMAGE, 5, steps=2)]))
    evaluator.evaluate(apply_moves(content.spells, [move(content, "spell:jab", DAMAGE, 2)]))

    played = [json.loads(line) for line in (data.parent / "seen.jsonl").read_text().splitlines()]
    assert played[0] == {"spell:attack:v1": 5, "spell:jab:v1": 1}
    assert played[1] == {"spell:attack:v1": 3, "spell:jab:v1": 2}


def test_the_metrics_come_back_under_the_name_of_the_evaluation_that_produced_them(
    engine_evaluator: tuple[EngineContentEvaluator, Content, Path],
) -> None:
    evaluator, content, _ = engine_evaluator

    metrics = evaluator.evaluate(content.spells)

    assert set(metrics) == {"mirror"}
    assert metrics["mirror"]["averageRounds"] == pytest.approx(32.0)
    assert evaluator.calls == 1


def test_an_engine_that_fails_says_which_step_failed(tmp_path: Path) -> None:
    data = tmp_path / "data"
    data.mkdir()
    content = content_tree(data)
    host = ContentEngine(
        engine=replace(EngineCommand(root=tmp_path), command=(sys.executable, "-c", "raise SystemExit(3)")),
        data=data,
        workdir=tmp_path / "work",
        builder=(sys.executable, "-c", "raise SystemExit(2)"),
    )
    evaluator = EngineContentEvaluator(host, Objective(seeds="s", evaluations={}, targets=()), content)

    with pytest.raises(EvaluationError, match="The data builder exited with 2"):
        evaluator.evaluate(content.spells)


def test_a_search_with_no_room_left_gives_up_rather_than_proposing_nothing(tmp_path: Path) -> None:
    """Every legal neighbour of a one-knob space pinned at both bounds is the catalogue it already is."""
    knobs = load(
        tmp_path,
        spells={
            "spell:attack": {
                "name": "Attack",
                "intent": "The yardstick.",
                "knobs": [{"path": DAMAGE, "min": 3, "max": 3, "step": 1}],
            }
        },
    )
    content = Content(
        spells={"spell:attack": json.loads(json.dumps(ATTACK))},
        files={"spell:attack": tmp_path / "attack.v1.json"},
    )

    assert propose(np.random.default_rng(0), knobs, content, (), TuneOptions()) is None


def test_a_search_that_never_finds_a_neighbour_still_reports_the_content_it_played(tmp_path: Path) -> None:
    knobs = load(
        tmp_path,
        spells={
            "spell:attack": {
                "name": "Attack",
                "intent": "The yardstick.",
                "knobs": [{"path": DAMAGE, "min": 3, "max": 3, "step": 1}],
            }
        },
    )
    content = Content(
        spells={"spell:attack": json.loads(json.dumps(ATTACK))},
        files={"spell:attack": tmp_path / "attack.v1.json"},
    )

    result = tune_content(FakeEvaluator(), knobs, content, TuneOptions(iterations=3, neighbours=2))

    assert result.candidates == ()
    assert not result.improved


def test_a_budget_that_plays_nothing_is_refused(tmp_path: Path) -> None:
    knobs = load(tmp_path)

    with pytest.raises(ValueError, match="at least one iteration"):
        tune_content(FakeEvaluator(), knobs, catalogue(tmp_path), TuneOptions(iterations=0))
