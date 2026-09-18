"""The paired difference between two evaluations: what their two marginal intervals do not describe."""

from __future__ import annotations

import json
import math
from pathlib import Path

import pytest

from downfall_learning.artifacts import ArtifactError, load_evaluation
from downfall_learning.paired import Z95, compare, format_paired, paired_difference, scores_by_seed

CONTENT = "7e199df428bccfd0ea882e218ef225a5a3872b4122d0763e5583c44f581cd9ff"


def evaluation(
    path: Path, scores: dict[int, float], agent: str = "A", opponent: str = "Greedy", content: str = CONTENT
) -> Path:
    """An evaluation.json with the fields the loader needs and the pairs the paired reading uses."""
    mean = sum(scores.values()) / len(scores)
    report = {
        "agent": agent,
        "wins": 0,
        "winRate": {"mean": mean, "low": 0.0, "high": 1.0},
        "score": {"mean": mean, "low": 0.0, "high": 1.0},
        "averageRemainingHealth": 0.0,
        "spellUsage": {},
        "spellEntropy": 0.0,
        "fizzleRate": 0.0,
        "criticalRate": 0.0,
    }
    path.write_text(
        json.dumps(
            {
                "stamp": {
                    "engineVersion": "test",
                    "contentHash": content,
                    "ruleSet": {},
                    "featureSchema": "features:v5",
                    "player1Agent": agent,
                    "player2Agent": opponent,
                    "baseSeed": 0,
                },
                "agentA": report,
                "agentB": {**report, "agent": opponent},
                "matches": 2 * len(scores),
                "draws": 0,
                "averageRounds": 7.0,
                "roundCapShare": 0.0,
                "pairs": [{"seed": seed, "scoreOfA": score} for seed, score in scores.items()],
            }
        ),
        encoding="utf-8",
    )
    return path


def test_the_difference_is_read_seed_by_seed_and_not_from_the_two_means(tmp_path: Path) -> None:
    """The point of the module: a constant per-seed gap is settled however wide the marginal spread is."""
    first = evaluation(tmp_path / "a.json", {1: 1.0, 2: 0.5, 3: 0.0, 4: 1.0}, agent="A")
    second = evaluation(tmp_path / "b.json", {1: 0.5, 2: 0.0, 3: 0.0, 4: 0.5}, agent="B")

    result = compare(load_evaluation(first), load_evaluation(second))

    assert result.seeds == 4
    assert result.difference == pytest.approx(0.375)
    assert result.agent_a == "A"
    assert result.agent_b == "B"


def test_a_difference_that_never_changes_sign_is_settled(tmp_path: Path) -> None:
    first = evaluation(tmp_path / "a.json", dict.fromkeys(range(1, 21), 1.0))
    second = evaluation(tmp_path / "b.json", dict.fromkeys(range(1, 21), 0.5))

    result = compare(load_evaluation(first), load_evaluation(second))

    assert result.difference == pytest.approx(0.5)
    assert result.standard_error == pytest.approx(0.0)
    assert result.settled
    assert "clear of zero" in format_paired(result)


def test_a_difference_that_averages_to_nothing_is_not_settled(tmp_path: Path) -> None:
    first = evaluation(tmp_path / "a.json", {1: 1.0, 2: 0.0, 3: 1.0, 4: 0.0})
    second = evaluation(tmp_path / "b.json", {1: 0.0, 2: 1.0, 3: 0.0, 4: 1.0})

    result = compare(load_evaluation(first), load_evaluation(second))

    assert result.difference == pytest.approx(0.0)
    assert not result.settled
    assert "does not settle" in format_paired(result)


def test_the_interval_is_the_mean_difference_plus_or_minus_z_standard_errors(tmp_path: Path) -> None:
    scores = {1: 1.0, 2: 1.0, 3: 0.5, 4: 0.0, 5: 0.5}
    first = evaluation(tmp_path / "a.json", scores)
    second = evaluation(tmp_path / "b.json", dict.fromkeys(scores, 0.0))

    result = compare(load_evaluation(first), load_evaluation(second))

    values = list(scores.values())
    mean = sum(values) / len(values)
    variance = sum((value - mean) ** 2 for value in values) / (len(values) - 1)
    assert result.standard_error == pytest.approx(math.sqrt(variance / len(values)))
    assert result.low == pytest.approx(mean - Z95 * result.standard_error)
    assert result.high == pytest.approx(mean + Z95 * result.standard_error)


def test_different_seeds_are_refused_rather_than_intersected(tmp_path: Path) -> None:
    """Intersecting would answer a question nobody asked on a subset nobody chose."""
    first = evaluation(tmp_path / "a.json", {1: 1.0, 2: 1.0, 3: 1.0})
    second = evaluation(tmp_path / "b.json", {1: 0.0, 2: 0.0, 9: 0.0})

    with pytest.raises(ArtifactError, match="different seeds"):
        compare(load_evaluation(first), load_evaluation(second))


def test_different_content_is_refused(tmp_path: Path) -> None:
    first = evaluation(tmp_path / "a.json", {1: 1.0, 2: 1.0})
    second = evaluation(tmp_path / "b.json", {1: 0.0, 2: 0.0}, content="0" * 64)

    with pytest.raises(ArtifactError, match="different content"):
        compare(load_evaluation(first), load_evaluation(second))


def test_an_evaluation_without_pairs_says_so(tmp_path: Path) -> None:
    path = evaluation(tmp_path / "a.json", {1: 1.0})
    data = json.loads(path.read_text())
    del data["pairs"]
    path.write_text(json.dumps(data), encoding="utf-8")

    with pytest.raises(ArtifactError, match="no 'pairs'"):
        scores_by_seed(load_evaluation(path))


def test_two_opponents_are_named_so_the_reader_sees_what_was_compared(tmp_path: Path) -> None:
    """The module cannot know whether a comparison is meaningful; it can refuse to hide which one was made."""
    first = evaluation(tmp_path / "a.json", {1: 1.0, 2: 1.0}, agent="X", opponent="Greedy")
    second = evaluation(tmp_path / "b.json", {1: 0.0, 2: 0.0}, agent="X", opponent="Random")

    report = format_paired(compare(load_evaluation(first), load_evaluation(second)))

    assert "different opponents" in report


def test_the_result_is_written_where_it_is_asked_for(tmp_path: Path) -> None:
    first = evaluation(tmp_path / "a.json", {1: 1.0, 2: 1.0})
    second = evaluation(tmp_path / "b.json", {1: 0.5, 2: 0.5})

    result = paired_difference(first, second, tmp_path / "out" / "paired.json")

    written = json.loads((tmp_path / "out" / "paired.json").read_text())
    assert written["difference"] == pytest.approx(result.difference)
    assert written["settled"] is True


def test_one_seed_is_refused_rather_than_called_certain(tmp_path: Path) -> None:
    """The module exists to stop one observation becoming a result; it must not do it itself (ADR 0049)."""
    first = evaluation(tmp_path / "a.json", {1: 1.0})
    second = evaluation(tmp_path / "b.json", {1: 0.0})

    with pytest.raises(ArtifactError, match="at least two seeds"):
        compare(load_evaluation(first), load_evaluation(second))


def test_different_rules_are_refused(tmp_path: Path) -> None:
    """The content hash covers the catalogue, not the rules: a moved round cap opens a gap of its own."""
    first = evaluation(tmp_path / "a.json", {1: 1.0, 2: 1.0})
    second = evaluation(tmp_path / "b.json", {1: 0.0, 2: 0.0})
    data = json.loads(second.read_text())
    data["stamp"]["ruleSet"] = {"roundCap": 30}
    second.write_text(json.dumps(data), encoding="utf-8")

    with pytest.raises(ArtifactError, match=r"different rules.*roundCap"):
        compare(load_evaluation(first), load_evaluation(second))


def test_a_different_engine_build_is_reported_and_not_refused(tmp_path: Path) -> None:
    """A rebuild need not change how a seed plays -- ci-150 reproduced ci-149 exactly on another build."""
    first = evaluation(tmp_path / "a.json", {1: 1.0, 2: 1.0})
    second = evaluation(tmp_path / "b.json", {1: 0.0, 2: 0.0})
    data = json.loads(second.read_text())
    data["stamp"]["engineVersion"] = "another"
    second.write_text(json.dumps(data), encoding="utf-8")

    report = format_paired(compare(load_evaluation(first), load_evaluation(second)))

    assert "different engine builds" in report
