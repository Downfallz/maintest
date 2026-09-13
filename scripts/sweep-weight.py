#!/usr/bin/env python3
"""Sweep one scoring weight alone, on fixed content, and print what each value plays like.

ADR 0032 measured `initiative` this way and ADR 0028 measured `defense` before it; both were done by hand.
This is the same method written down, because two more weights need it and a third will.

The method matters in one respect that is easy to get wrong: `explore:` and `greedy` wrap the **compiled**
`GreedyAgent`, so a weights file cannot reach them. The sweep therefore patches `ScoringWeights.Default` and
rebuilds the engine at each point, which is exactly what shipping the value does. `heuristic:<file>` does read
a file, so the `exploit` evaluation's attacker is untouched and its defender moves with the sweep — which is
the point of keeping it: it is a reading the objective does not contain.

One sweep at a time, always. Two of these running together patch the same two files and share the same
working directory, so each one restores the other's patch and measures a weight it did not set. The run
refuses to start unless both files are clean in git, which is what that collision looks like from outside.

Usage:
    uv run --project learning python scripts/sweep-weight.py initiative 1.0 1.5 2.1 2.7
"""

from __future__ import annotations

import json
import re
import shutil
import subprocess
import sys
from collections.abc import Sequence
from contextlib import contextmanager
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "learning" / "src"))

from downfall_learning.knobs import load_content, load_knobs
from downfall_learning.search_weights import EngineCommand
from downfall_learning.tune_content import (
    ContentEngine,
    EngineContentEvaluator,
)

ROOT = Path(__file__).resolve().parents[1]
WEIGHTS = ROOT / "src" / "DownfallArena.Application" / "Agents" / "ScoringWeights.cs"
GREEDY_JSON = ROOT / "learning" / "weights" / "greedy.json"
RELEASE_CLI = (
    "dotnet",
    "run",
    "--project",
    "src/DownfallArena.Cli",
    "--configuration",
    "Release",
    "--no-build",
    "--",
)

#: What the table prints, and where each column comes from.
COLUMNS = (
    ("rounds", "mirror", "averageRounds"),
    ("entropy", "mirror", "spellEntropyA"),
    ("p1Share", "mirror", "player1WinShare"),
    ("fizzle", "mirror", "fizzleRateA"),
    ("tierWin", "variety", "tierWinSpread"),
    ("tierUse", "variety", "tierUsageShare"),
    ("never", "variety", "spellsNeverCast"),
    ("barely", "variety", "spellsBarelyCast"),
    ("skill", "skill", "winRateA"),
    ("exploit", "exploit", "winRateA"),
)


@contextmanager
def patched(name: str, value: float):
    """Set one weight in the C# default and in the weights file `cast_value` reads, then put both back."""
    source, weights = WEIGHTS.read_text(), GREEDY_JSON.read_text()
    try:
        field = name.capitalize()
        patched_source, count = re.subn(rf"{field}: [0-9.]+", f"{field}: {value}", source)
        if count != 1:
            raise SystemExit(f"'{field}:' appears {count} times in ScoringWeights.cs, expected once.")
        WEIGHTS.write_text(patched_source)
        document = json.loads(weights)
        document[name] = value
        GREEDY_JSON.write_text(json.dumps(document, indent=2) + "\n")
        build()
        yield
    finally:
        WEIGHTS.write_text(source)
        GREEDY_JSON.write_text(weights)


def require_clean() -> None:
    """Refuse to run over a file another sweep is holding patched, or over an edit of your own."""
    # Two fixed paths given to git, without a shell: nothing here comes from a user other than the one who
    # launched the sweep.
    dirty = subprocess.run(  # NOSONAR
        ["git", "diff", "--name-only", "--", str(WEIGHTS), str(GREEDY_JSON)],
        cwd=ROOT,
        capture_output=True,
        text=True,
        check=True,
    ).stdout.split()
    if dirty:
        raise SystemExit(
            "Refusing to sweep: " + ", ".join(dirty) + " already differs from HEAD.\n"
            "Another sweep is running, or one died patched. Finish or restore it first."
        )


def build() -> None:
    # A fixed build command, without a shell: same reasoning as `require_clean`.
    done = subprocess.run(  # NOSONAR
        ["dotnet", "build", "--configuration", "Release", "--no-restore"],
        cwd=ROOT,
        capture_output=True,
        text=True,
        check=False,
    )
    if done.returncode != 0:
        raise SystemExit("\n".join(done.stdout.splitlines()[-15:]))


def measure(workdir: Path) -> tuple[float, dict[str, dict[str, float]]]:
    """Play the objective's four evaluations on the content as it stands and score them."""
    knobs = load_knobs(ROOT / "data" / "balance" / "knobs.json")
    content = load_content(ROOT / "data")
    host = ContentEngine(engine=_engine(), data=ROOT / "data", workdir=workdir)
    metrics = EngineContentEvaluator(host, knobs.objective, content).evaluate({})
    return sum(knobs.objective.breakdown(metrics).values()), metrics


def _engine():
    return EngineCommand(command=list(RELEASE_CLI), root=ROOT, seeds="benchmarks/benchmark-seeds.json")


def sweeps(arguments: Sequence[str]) -> list[tuple[str, list[float]]]:
    """Split `initiative 1.0 2.0 -- energy 0.2 0.4` into the sweeps it asks for, run one after the other."""
    groups = [group.split() for group in " ".join(arguments).split(" -- ")]
    if any(len(group) < 2 for group in groups):
        raise SystemExit(__doc__)
    return [(group[0], [float(value) for value in group[1:]]) for group in groups]


def main() -> None:
    planned = sweeps(sys.argv[1:])
    require_clean()
    workdir = ROOT / ".sweep"
    for name, values in planned:
        print(
            f"{name:>11} {'objective':>10} " + " ".join(f"{label:>8}" for label, _, _ in COLUMNS), flush=True
        )
        for value in values:
            with patched(name, value):
                score, metrics = measure(workdir)
            cells = " ".join(f"{metrics.get(ev, {}).get(key, float('nan')):8.3f}" for _, ev, key in COLUMNS)
            print(f"{value:>11} {score:>10.2f} {cells}", flush=True)
        print(flush=True)
    shutil.rmtree(workdir, ignore_errors=True)


if __name__ == "__main__":
    main()
