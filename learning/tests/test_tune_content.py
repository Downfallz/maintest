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
    Candidate,
    ContentEngine,
    EngineContentEvaluator,
    Move,
    Search,
    TuneOptions,
    TuneResult,
    apply_moves,
    format_result,
    metrics_of,
    playable,
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
        moves = propose(rng, Search(knobs, content, TuneOptions()), ())
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
        proposed = propose(rng, Search(knobs, content, options), spent)
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

    quiet = json.loads(json.dumps(ATTACK)) | {"id": "spell:quiet:v1"}
    content = Content(
        spells={"spell:attack": ATTACK, "spell:jab": JAB, "spell:quiet": quiet},
        files=dict.fromkeys(("spell:attack", "spell:jab", "spell:quiet"), Path("x.json")),
    )

    metrics = metrics_of(evaluation, "mirror", content)

    assert metrics["spellUsageShare"] == pytest.approx(0.75)
    assert metrics["spellsNeverCast"] == 1


def test_a_run_with_no_landed_cast_reads_as_one_spell_taking_everything() -> None:
    """Zero casts is not balance: scoring it as zero share would make an unplayable catalogue look perfect."""
    raw = evaluation_json(0.5, 0.5)
    raw["spellOutcomes"] = []

    content = Content(spells={"spell:attack": ATTACK}, files={"spell:attack": Path("x.json")})

    metrics = metrics_of(Evaluation.from_json(raw), "mirror", content)

    assert metrics["spellUsageShare"] == 1.0
    assert metrics["spellsNeverCast"] == 1


def test_the_proposal_reads_as_what_moved_and_what_it_bought(tmp_path: Path) -> None:
    knobs = load(tmp_path)
    content = catalogue(tmp_path)
    result = tune_content(FakeEvaluator(), knobs, content, TuneOptions(iterations=4, neighbours=2, seed=3))

    text = format_result(result, knobs.objective)

    assert "What it changed" in text
    assert "How balanced the content is" in text
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
    assert "changed no measurement at all" in format_result(result, knobs.objective)


def test_a_proposal_never_plays_a_catalogue_it_is_already_playing(tmp_path: Path) -> None:
    """A knob pinned at a bound would otherwise keep counting steps and pay for the same catalogue twice."""
    knobs = load(tmp_path)
    content = catalogue(tmp_path)
    rng = np.random.default_rng(4)
    pinned = (move(content, "spell:jab", DAMAGE, 1, steps=-2),)

    for _ in range(20):
        proposed = propose(rng, Search(knobs, content, TuneOptions(max_changes=1)), pinned)
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
    spells = content.spells

    with pytest.raises(EvaluationError, match="The data builder exited with 2"):
        evaluator.evaluate(spells)


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

    assert propose(np.random.default_rng(0), Search(knobs, content, TuneOptions()), ()) is None


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
    content = catalogue(tmp_path)
    evaluator = FakeEvaluator()
    options = TuneOptions(iterations=0)

    with pytest.raises(ValueError, match="at least one iteration"):
        tune_content(evaluator, knobs, content, options)


def test_the_opening_sweep_plays_every_playable_knob_once(tmp_path: Path) -> None:
    """The failure this exists for: a uniform draw left the best move in the catalogue untried."""
    knobs = load(tmp_path)
    content = catalogue(tmp_path)
    evaluator = FakeEvaluator()

    result = tune_content(evaluator, knobs, content, TuneOptions(iterations=1, neighbours=1, seed=0))

    swept = {move.knob.key for candidate in result.candidates for move in candidate.moves}
    assert swept == {"spell:attack/effects/0/amount", "spell:jab/effects/0/amount"}


def test_a_knob_on_a_spell_the_build_lacks_is_never_drawn(tmp_path: Path) -> None:
    """Most of the knobs file can be on turned-off spells; spending draws there buys nothing."""
    knobs = load(tmp_path)
    content = catalogue(tmp_path)
    thin = Content(
        spells={"spell:attack": content.spells["spell:attack"]},
        files={"spell:attack": content.files["spell:attack"]},
    )

    assert [knob.spell for knob in playable(knobs, thin)] == ["spell:attack"]


def test_the_sweep_can_be_skipped(tmp_path: Path) -> None:
    knobs = load(tmp_path)
    evaluator = FakeEvaluator()

    tune_content(
        evaluator, knobs, catalogue(tmp_path), TuneOptions(iterations=1, neighbours=1, seed=0, sweep=False)
    )

    assert evaluator.calls == 2


def test_the_random_phase_leans_on_the_knobs_the_sweep_showed_can_move_a_metric(tmp_path: Path) -> None:
    knobs = load(tmp_path)
    content = catalogue(tmp_path)
    search = Search(knobs, content, TuneOptions())
    rng = np.random.default_rng(0)

    drawn = [propose(rng, search, (), favour={"spell:jab/effects/0/amount"}) for _ in range(40)]
    keys = [move.knob.key for moves in drawn if moves for move in moves]

    assert keys.count("spell:jab/effects/0/amount") > keys.count("spell:attack/effects/0/amount")


def test_a_candidate_is_judged_with_the_tiers_of_the_content_it_came_from(tmp_path: Path) -> None:
    """Rebuilding the catalogue without its tiers made every candidate look like it added a dominance."""
    knobs = load(tmp_path)
    base = catalogue(tmp_path)
    tiered = Content(spells=base.spells, files=base.files, tiers={"spell:attack": 0, "spell:jab": 1})

    candidate = apply_moves(tiered.spells, [move(tiered, "spell:jab", DAMAGE, 3, steps=2)])

    assert violations(tiered, candidate, knobs) == []
    assert violations(base, candidate, knobs) == ["spell:jab would become strictly better than spell:attack."]


def outcome(spell_id: str, resolved: int, sides: int, damage: int) -> dict:
    """One row of the engine's spell table, with the fields the tier readings use."""
    return {
        "spell": spell_id,
        "resolved": resolved,
        "sides": sides,
        "damage": damage,
        "damagePerCast": damage / resolved if resolved else 0,
        "score": 0.5,
    }


def two_tiers() -> Content:
    spells = {
        "spell:starter": json.loads(json.dumps(ATTACK)) | {"id": "spell:starter:v1"},
        "spell:filler": json.loads(json.dumps(ATTACK)) | {"id": "spell:filler:v1"},
        "spell:unlocked": json.loads(json.dumps(ATTACK)) | {"id": "spell:unlocked:v1"},
    }
    return Content(
        spells=spells,
        files=dict.fromkeys(spells, Path("x.json")),
        tiers={"spell:starter": 0, "spell:filler": 0, "spell:unlocked": 1},
    )


def test_a_tier_one_spell_monopolises_is_named_even_when_it_is_small_overall() -> None:
    """The whole point of reading per tier: the catalogue-wide share hides a monopolised tier."""
    raw = evaluation_json(0.5, 0.5)
    raw["spellOutcomes"] = [
        outcome("spell:starter:v1", 100, 100, 100),
        outcome("spell:filler:v1", 0, 0, 0),
        outcome("spell:unlocked:v1", 900, 100, 900),
    ]

    metrics = metrics_of(Evaluation.from_json(raw), "mirror", two_tiers())

    assert metrics["spellUsageShare"] == pytest.approx(0.9)
    assert metrics["tierUsageShare"] == pytest.approx(1.0)


def test_damage_per_cast_is_compared_only_inside_a_tier_and_only_between_damaging_spells() -> None:
    raw = evaluation_json(0.5, 0.5)
    raw["spellOutcomes"] = [
        outcome("spell:starter:v1", 100, 100, 100),
        outcome("spell:filler:v1", 100, 100, 300),
        outcome("spell:unlocked:v1", 100, 100, 5000),
    ]

    metrics = metrics_of(Evaluation.from_json(raw), "mirror", two_tiers())

    assert metrics["tierDamageSpread"] == pytest.approx(3.0)


def test_a_spell_too_few_sides_declared_is_left_out_of_the_win_spread() -> None:
    """Its win share on a handful of sides is noise, and the engine leaves it out of its table too."""
    raw = evaluation_json(0.5, 0.5)
    loud = outcome("spell:starter:v1", 100, 100, 100) | {"score": 0.9}
    quiet = outcome("spell:filler:v1", 5, 2, 5) | {"score": 0.1}
    raw["spellOutcomes"] = [loud, quiet, outcome("spell:unlocked:v1", 100, 100, 100)]

    metrics = metrics_of(Evaluation.from_json(raw), "mirror", two_tiers())

    assert "tierWinSpread" not in metrics


def test_a_tier_nobody_cast_is_left_to_the_never_cast_count() -> None:
    raw = evaluation_json(0.5, 0.5)
    raw["spellOutcomes"] = [outcome("spell:unlocked:v1", 100, 100, 100)]

    metrics = metrics_of(Evaluation.from_json(raw), "mirror", two_tiers())

    assert metrics["tierUsageShare"] == pytest.approx(1.0)
    assert metrics["spellsNeverCast"] == 2


def test_an_attack_the_defence_absorbs_widens_the_spread_rather_than_leaving_it() -> None:
    """Reading "is this a damaging spell" from the result would reward making one useless.

    Lower an attack until every hit lands on armour and it deals zero. If that dropped it from the
    comparison, the tier would look *more* even than before and the tuner would be paid to break spells.
    """
    content = two_tiers()
    even = evaluation_json(0.5, 0.5)
    even["spellOutcomes"] = [
        outcome("spell:starter:v1", 100, 100, 200),
        outcome("spell:filler:v1", 100, 100, 100),
        outcome("spell:unlocked:v1", 100, 100, 100),
    ]
    absorbed = evaluation_json(0.5, 0.5)
    absorbed["spellOutcomes"] = [
        outcome("spell:starter:v1", 100, 100, 200),
        outcome("spell:filler:v1", 100, 100, 0),
        outcome("spell:unlocked:v1", 100, 100, 100),
    ]

    before = metrics_of(Evaluation.from_json(even), "mirror", content)["tierDamageSpread"]
    after = metrics_of(Evaluation.from_json(absorbed), "mirror", content)["tierDamageSpread"]

    assert before == pytest.approx(2.0)
    assert after > before


def test_a_heal_is_still_left_out_of_the_damage_spread() -> None:
    """It has no Damage effect at all, which is a different thing from an attack that landed nothing."""
    content = two_tiers()
    content.spells["spell:filler"]["effects"] = [{"kind": "Heal", "amount": 3}]
    raw = evaluation_json(0.5, 0.5)
    raw["spellOutcomes"] = [
        outcome("spell:starter:v1", 100, 100, 200),
        outcome("spell:filler:v1", 100, 100, 0),
        outcome("spell:unlocked:v1", 100, 100, 100),
    ]

    assert "tierDamageSpread" not in metrics_of(Evaluation.from_json(raw), "mirror", content)


def _report(before: float, after: float) -> str:
    """One target, measured twice, as the report reads it.

    The search itself is tested above; what these cover is the words it comes back as.
    """
    objective = Objective(
        seeds="seeds.json",
        evaluations={"mirror": {}},
        targets=(Target(metric="spellUsageShare", on="mirror", maximum=0.25, scale=0.1, weight=1),),
    )
    knob = Knob(spell="spell:pummel", path="/criticalChance", minimum=0.4, maximum=0.8, step=0.05)
    metrics = {"mirror": {"spellUsageShare": before}}, {"mirror": {"spellUsageShare": after}}
    initial = Candidate(
        iteration=0,
        moves=(),
        score=objective.score(metrics[0]),
        breakdown=objective.breakdown(metrics[0]),
        metrics=metrics[0],
    )
    best = Candidate(
        iteration=1,
        moves=(Move(knob=knob, steps=1, before=0.667, after=0.717),),
        score=objective.score(metrics[1]),
        breakdown=objective.breakdown(metrics[1]),
        metrics=metrics[1],
    )
    result = TuneResult(best=best, initial=initial, candidates=(best,), spells={}, files={})
    return format_result(result, objective)


def test_the_report_says_which_measurement_the_gain_came_from() -> None:
    """The table prints the penalty a proposal ends on, so a target that fell from 24 to 2 and one that was
    always 2 read the same. A whole run explained by one measurement is a run to be suspicious of."""
    text = _report(0.9, 0.5)

    assert "Where the" in text
    assert "the share of every landed cast taken by the one spell cast most" in text


def test_the_report_says_what_a_gain_cost_elsewhere() -> None:
    text = _report(0.5, 0.9)

    assert "And what that cost" in text


def test_what_is_still_wrong_says_the_number_and_the_number_it_should_be() -> None:
    text = _report(0.9, 0.5)

    assert "reads 0.500, and it should be at most 0.25" in text


def test_a_target_inside_its_range_is_named_as_one_the_score_stops_watching() -> None:
    """A band costs the same anywhere inside it, so a change that worsens a passing target is invisible."""
    text = _report(0.9, 0.1)

    assert "What the score is not watching" in text
    assert "mirror.spellUsageShare" in text


def test_a_move_reads_as_the_content_names_it_rather_than_as_a_pointer() -> None:
    text = _report(0.9, 0.5)

    assert "Pummel — critical chance: 0.667 -> 0.717" in text


def test_two_targets_on_one_measurement_are_told_apart_by_the_run_they_came_from() -> None:
    """`mirror` and `skill` play the same seeds, so the same metric read twice would be one row said twice."""
    objective = Objective(
        seeds="seeds.json",
        evaluations={"mirror": {}, "skill": {}},
        targets=(
            Target(metric="winRateA", on="mirror", maximum=0.55, scale=0.05, weight=1),
            Target(metric="winRateA", on="skill", minimum=0.65, scale=0.1, weight=1),
        ),
    )
    metrics = {"mirror": {"winRateA": 0.9}, "skill": {"winRateA": 0.2}}
    candidate = Candidate(
        iteration=0,
        moves=(),
        score=objective.score(metrics),
        breakdown=objective.breakdown(metrics),
        metrics=metrics,
    )
    result = TuneResult(best=candidate, initial=candidate, candidates=(candidate,), spells={}, files={})

    text = format_result(result, objective)

    assert "(mirror)" in text
    assert "(skill)" in text


def one_tier(**resolved: int) -> tuple[Content, dict]:
    """Spells at one depth, with the casts each landed, as `metrics_of` reads the pair."""
    spells = {alias: json.loads(json.dumps(ATTACK)) | {"id": f"{alias}:v1"} for alias in resolved}
    content = Content(
        spells=spells,
        files=dict.fromkeys(spells, Path("x.json")),
        tiers=dict.fromkeys(spells, 1),
    )
    raw = evaluation_json(0.5, 0.5)
    raw["spellOutcomes"] = [
        outcome(f"{alias}:v1", landed, landed, landed) for alias, landed in resolved.items()
    ]
    return content, raw


def test_a_spell_taking_almost_none_of_its_tier_is_counted_even_though_it_is_not_a_zero() -> None:
    """The hole `spellsNeverCast` leaves: `pummel` took 6 of 5283 landed casts and read as no change."""
    content, raw = one_tier(**{"spell:big": 1000, "spell:small": 5})

    metrics = metrics_of(Evaluation.from_json(raw), "mirror", content)

    assert metrics["spellsNeverCast"] == 0
    assert metrics["spellsBarelyCast"] == 1


def test_a_spell_holding_its_own_in_its_tier_is_not_counted() -> None:
    content, raw = one_tier(**{"spell:big": 60, "spell:small": 40})

    assert metrics_of(Evaluation.from_json(raw), "mirror", content)["spellsBarelyCast"] == 0


def test_the_share_is_read_against_the_tier_and_not_the_catalogue() -> None:
    """A tier is the set a player chooses between: rare overall can still be the right pick where offered."""
    content, raw = one_tier(**{"spell:big": 1000, "spell:small": 5})
    content.tiers["spell:big"] = 0

    assert metrics_of(Evaluation.from_json(raw), "mirror", content)["spellsBarelyCast"] == 0


def test_a_spell_in_a_tier_nobody_cast_is_not_counted_as_barely_cast() -> None:
    """Skipped rather than counted, the same way the tier readings skip a tier they cannot speak about."""
    content, raw = one_tier(**{"spell:cast": 10, "spell:quiet": 0})
    content.tiers["spell:quiet"] = 2

    metrics = metrics_of(Evaluation.from_json(raw), "mirror", content)

    assert metrics["spellsNeverCast"] == 1
    assert metrics["spellsBarelyCast"] == 0


CRITICAL = "/criticalChance"


class ThresholdEvaluator:
    """A catalogue that only responds when two of its numbers move together.

    The shape `poison_slash` has: raising the bleed amount alone changes nothing a metric can see, raising
    its duration alone changes nothing, and raising both crosses into a different game.
    """

    def __init__(self) -> None:
        self.calls = 0

    def evaluate(self, spells: Mapping[str, dict]) -> dict[str, dict[str, float]]:
        self.calls += 1
        spell = spells["spell:combo"]
        both = spell["effects"][0]["amount"] >= 2 and spell["criticalChance"] >= 0.6
        return {"mirror": {"averageRounds": 12.0 if both else 40.0}}


def combo_pair(tmp_path: Path) -> tuple[Knobs, Content]:
    document = knobs_document(
        spells={
            "spell:combo": {
                "name": "Combo",
                "intent": "Two numbers that only matter together.",
                "knobs": [
                    {"path": DAMAGE, "min": 1, "max": 3, "step": 1},
                    {"path": CRITICAL, "min": 0.4, "max": 0.8, "step": 0.1},
                ],
            }
        }
    )
    path = tmp_path / "knobs.json"
    path.write_text(json.dumps(document), encoding="utf-8")
    spell = json.loads(json.dumps(JAB)) | {"id": "spell:combo:v1", "criticalChance": 0.5}
    content = Content(spells={"spell:combo": spell}, files={"spell:combo": tmp_path / "combo.json"})
    return load_knobs(path), content


def test_two_knobs_of_one_spell_are_tried_together_when_neither_moves_anything_alone(
    tmp_path: Path,
) -> None:
    """The sweep is the difference between unlikely and impossible for one knob; this is it for a pair."""
    knobs, content = combo_pair(tmp_path)

    result = tune_content(
        ThresholdEvaluator(), knobs, content, TuneOptions(iterations=1, neighbours=1, seed=0)
    )

    assert result.improved
    assert {move.knob.path for move in result.best.moves} == {DAMAGE, CRITICAL}


class DamageEvaluator:
    """Responds to the damage knob alone, so the pairs gate has something to decline to fire on."""

    def __init__(self) -> None:
        self.calls = 0

    def evaluate(self, spells: Mapping[str, dict]) -> dict[str, dict[str, float]]:
        self.calls += 1
        return {"mirror": {"averageRounds": 40.0 - spells["spell:combo"]["effects"][0]["amount"]}}


def test_a_spell_that_already_responds_to_one_step_costs_no_paired_evaluation(tmp_path: Path) -> None:
    """A catalogue that is answering pays nothing for this: the pairs are only for the spells that are not.

    The spell has two knobs either way, so a pair *could* be built in both runs; the gate is what stops one.
    """
    knobs, content = combo_pair(tmp_path)
    responsive, stuck = DamageEvaluator(), ThresholdEvaluator()
    budget = TuneOptions(iterations=1, neighbours=1, seed=0)

    tune_content(responsive, knobs, content, budget)
    tune_content(stuck, knobs, content, budget)

    assert responsive.calls < stuck.calls


def test_the_pairs_are_skipped_when_the_run_asks_for_it(tmp_path: Path) -> None:
    """`--no-pairs`: the budget is the caller's, and this is the part of it that has no upper bound."""
    knobs, content = combo_pair(tmp_path)

    result = tune_content(
        ThresholdEvaluator(), knobs, content, TuneOptions(iterations=1, neighbours=1, seed=0, pairs=False)
    )

    assert not result.improved


def test_a_paired_move_that_breaks_a_constraint_is_never_played(tmp_path: Path) -> None:
    """Refused before the engine sees it, the same as every other candidate the search builds.

    The twin makes every move that raises the combo strictly better than a spell of the same price, so the
    one pair this catalogue has is illegal and the threshold behind it is never reached.
    """
    knobs, content = combo_pair(tmp_path)
    content.spells["spell:twin"] = json.loads(json.dumps(content.spells["spell:combo"])) | {
        "id": "spell:twin:v1"
    }

    result = tune_content(
        ThresholdEvaluator(), knobs, content, TuneOptions(iterations=1, neighbours=1, seed=0)
    )

    assert not result.improved


def test_a_pair_is_not_built_when_the_run_may_only_change_one_knob(tmp_path: Path) -> None:
    """A two-knob proposal is over a budget of one, and the budget is the caller's, not the opening's."""
    knobs, content = combo_pair(tmp_path)

    result = tune_content(
        ThresholdEvaluator(),
        knobs,
        content,
        TuneOptions(iterations=1, neighbours=1, seed=0, max_changes=1),
    )

    assert not result.improved
    assert all(len(candidate.moves) <= 1 for candidate in result.candidates)


class PlateauEvaluator:
    """A catalogue whose good point is two steps of one knob past the pair that reaches it.

    The shape `poison_slash` has on this branch: one step of each knob moves the reading and scores *worse*
    than the content it came from, and the point worth keeping is one further step of the second knob alone.
    """

    def __init__(self) -> None:
        self.calls = 0

    def evaluate(self, spells: Mapping[str, dict]) -> dict[str, dict[str, float]]:
        self.calls += 1
        spell = spells["spell:combo"]
        reached = (spell["effects"][0]["amount"], round(spell["criticalChance"], 3))
        rounds = {(2, 0.6): 50.0, (2, 0.7): 12.0}.get(reached, 40.0)
        return {"mirror": {"averageRounds": rounds}}


def test_a_pair_whose_first_step_moves_a_reading_without_improving_it_is_stepped_further(
    tmp_path: Path,
) -> None:
    """One step of each knob bridges one flat step; a plateau needs the pair walked past that."""
    knobs, content = combo_pair(tmp_path)

    result = tune_content(PlateauEvaluator(), knobs, content, TuneOptions(iterations=1, neighbours=1, seed=0))

    assert result.improved
    assert {(move.knob.path, move.steps) for move in result.best.moves} == {(DAMAGE, 1), (CRITICAL, 2)}


def test_a_depth_of_one_is_the_opening_move_alone(tmp_path: Path) -> None:
    """What the search did before deepening existed, and the seam the test above is measured against."""
    knobs, content = combo_pair(tmp_path)

    result = tune_content(
        PlateauEvaluator(), knobs, content, TuneOptions(iterations=1, neighbours=1, seed=0, pair_depth=1)
    )

    assert not result.improved


def test_a_pair_that_moves_nothing_at_all_is_not_stepped_further(tmp_path: Path) -> None:
    """A longer step into dead content costs an evaluation to learn the same thing again."""
    knobs, content = combo_pair(tmp_path)

    class Deaf:
        def __init__(self) -> None:
            self.calls = 0

        def evaluate(self, spells: Mapping[str, dict]) -> dict[str, dict[str, float]]:
            self.calls += 1
            return {"mirror": {"averageRounds": 40.0}}

    deaf = Deaf()
    tune_content(deaf, knobs, content, TuneOptions(iterations=1, neighbours=1, seed=0))

    # One baseline, the legal single steps, one opening pair, one climbing neighbour -- and nothing deeper.
    assert deaf.calls == 1 + len(playable(knobs, content)) * 2 - 1 + 1 + 1
