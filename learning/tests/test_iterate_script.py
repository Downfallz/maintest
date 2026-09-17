"""The seed checks of scripts/iterate.sh, through its --dry-run, which stops before anything is built or run.

Match i of a dataset recorded from seed s plays seed s + i, so two seeds closer than the match count record
the same matches shifted by their distance: seeds 1, 2 and 3 at 5000 matches were one dataset three times
(journal, 2026-09-16). These run the script itself, because the check lives there and nowhere else.
"""

from __future__ import annotations

import subprocess
from pathlib import Path

import pytest

from conftest import REPO_ROOT

SCRIPT = REPO_ROOT / "scripts" / "iterate.sh"


def iterate(*arguments: str, tmp_path: Path) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        ["bash", str(SCRIPT), "--dry-run", "--run", f"dry-{tmp_path.name}", *arguments],
        capture_output=True,
        text=True,
        check=False,
    )


def test_the_default_seeds_are_spaced_by_the_match_count(tmp_path: Path) -> None:
    result = iterate("--matches", "5000", tmp_path=tmp_path)

    assert result.returncode == 0, result.stderr
    assert "Seeds: 1 5001 10001" in result.stdout


def test_seeds_closer_than_the_match_count_are_refused_before_anything_runs(tmp_path: Path) -> None:
    result = iterate("--matches", "5000", "--seeds", "1 2 3", tmp_path=tmp_path)

    assert result.returncode == 2
    assert "Seeds 1 and 2 are 1 apart" in result.stderr
    assert "share 4999 of their 5000 matches" in result.stderr
    assert '"1 5001 10001"' in result.stderr
    assert not (REPO_ROOT / "runs" / f"dry-{tmp_path.name}").exists()


def test_seeds_one_short_of_the_match_count_are_refused(tmp_path: Path) -> None:
    result = iterate("--matches", "5000", "--seeds", "1,5000", tmp_path=tmp_path)

    assert result.returncode == 2
    assert "share 1 of their 5000 matches" in result.stderr


def test_seeds_exactly_the_match_count_apart_are_accepted(tmp_path: Path) -> None:
    result = iterate("--matches", "5000", "--seeds", "1 5001", tmp_path=tmp_path)

    assert result.returncode == 0, result.stderr
    assert "Seeds: 1 5001" in result.stdout


def test_the_match_count_is_read_in_base_ten(tmp_path: Path) -> None:
    """A leading zero would make bash read 010 as octal 8 where the engine reads decimal 10."""
    result = iterate("--matches", "010", tmp_path=tmp_path)

    assert result.returncode == 0, result.stderr
    assert "Seeds: 1 11 21" in result.stdout
    assert "Matches: 10" in result.stdout


def test_seeds_with_leading_zeros_are_the_same_seeds(tmp_path: Path) -> None:
    result = iterate("--matches", "10", "--seeds", "01 011", tmp_path=tmp_path)

    assert result.returncode == 0, result.stderr
    assert "Seeds: 1 11" in result.stdout


@pytest.mark.parametrize("count", ["0", "ten", "1.5", ""])
def test_a_match_count_that_is_not_a_whole_number_is_refused(tmp_path: Path, count: str) -> None:
    result = iterate("--matches", count, tmp_path=tmp_path)

    assert result.returncode == 2
    assert "not a whole number" in result.stderr


def test_a_repeated_seed_is_refused(tmp_path: Path) -> None:
    result = iterate("--matches", "10", "--seeds", "1 1", tmp_path=tmp_path)

    assert result.returncode == 2
    assert "repeats a seed" in result.stderr
