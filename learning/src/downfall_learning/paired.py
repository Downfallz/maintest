"""The difference between two evaluations, read seed by seed rather than from their two intervals.

Every evaluation plays the same fixed benchmark seeds, so two agents' scores rise and fall together: a seed
that is hard for one is hard for the other. ``search_weights.format_search`` already says what follows from
that, and says it in both directions -- "Two intervals overlapping is not a test of the difference between
them, and two intervals clear of each other is not proof of one either". A marginal interval describes where
one agent's score sits; neither describes the *difference*, which is the quantity almost every question in
this project is actually about.

The difference does have an interval, and the data for it is already written. ``evaluate`` emits ``pairs``:
one entry per seed, carrying ``scoreOfA``, with a seed's two mirrored matches already averaged into it by
``SeedPair``. Subtracting those per-seed scores between two files and taking the mean's interval is the
paired reading, and it is *tighter* than the marginals suggest exactly because the two are correlated -- the
shared seed difficulty cancels instead of adding.

This module exists because nine evaluations were once read as marginal intervals with the paired data sitting
in the same files (journal, 2026-09-18). The numbers were there; nothing computed them.

What it refuses, and why:

- **Different seed sets.** There is no pairing to do, and silently intersecting them would answer a question
  nobody asked on a subset nobody chose.
- **Different content.** A score against one catalogue and a score against another are not comparable at all,
  and the content hash is in both stamps.
- **Different rules.** The content hash covers the catalogue and not the rules it is played under, so a round
  cap or a critical multiplier that moved would open a gap that has nothing to do with the agents.
- **Fewer than two seeds.** One observation estimates no variance, so the difference has no interval and
  settles nothing. Reporting it with a degenerate interval would turn one seed into certainty, which is the
  thing this module exists to stop (ADR 0049).

A different *engine build* is reported rather than refused: ``ci-150`` reproduced ``ci-149`` to sixteen
decimal places on a different one, so a rebuild is not on its own a reason to discard a comparison -- it is a
reason to say so, because it need not be.

What it cannot check for you: whether the comparison means anything. Two files of ``X vs Greedy`` and
``Y vs Greedy`` differ by the agent, which is the usual question; ``X vs Greedy`` against ``X vs Random``
differs by the opponent, which is a different one. Both agents and both opponents are named in the output so
a reader can see which was asked.
"""

from __future__ import annotations

import json
import math
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from downfall_learning.artifacts import ArtifactError, Evaluation, load_evaluation

#: The 97.5th percentile of the normal distribution, the same constant the engine's ``PairedStatistics`` uses
#: for its own intervals, so a marginal interval and a paired one here are read at the same level.
Z95 = 1.959964

PAIRED_FILE = "paired.json"


def scores_by_seed(evaluation: Evaluation) -> dict[int, float]:
    """Agent A's score on each seed, from the ``pairs`` an evaluation already writes."""
    pairs = evaluation.raw.get("pairs")
    if not pairs:
        raise ArtifactError(
            "This evaluation carries no 'pairs', so there is nothing to pair on. Every evaluation the engine "
            "writes has them; a file without them was hand-made or truncated."
        )
    by_seed: dict[int, float] = {}
    for pair in pairs:
        try:
            seed = int(pair["seed"])
            score = float(pair["scoreOfA"])
        except (KeyError, TypeError, ValueError) as error:
            raise ArtifactError(f"A pair needs a numeric 'seed' and 'scoreOfA': {error}.") from None
        if seed in by_seed:
            raise ArtifactError(f"Seed {seed} appears twice in one evaluation; its pairs are not a set.")
        by_seed[seed] = score
    return by_seed


@dataclass(frozen=True)
class PairedDifference:
    """The mean per-seed difference between two evaluations, with the interval of that mean."""

    agent_a: str
    agent_b: str
    opponent_a: str
    opponent_b: str
    engine_a: str
    engine_b: str
    seeds: int
    mean_a: float
    mean_b: float
    difference: float
    standard_error: float

    @property
    def low(self) -> float:
        return self.difference - Z95 * self.standard_error

    @property
    def high(self) -> float:
        return self.difference + Z95 * self.standard_error

    @property
    def settled(self) -> bool:
        """Whether the interval of the difference is clear of zero, which is the whole question."""
        return self.low > 0 or self.high < 0

    def to_json(self) -> dict[str, Any]:
        return {
            "agentA": self.agent_a,
            "agentB": self.agent_b,
            "opponentOfA": self.opponent_a,
            "opponentOfB": self.opponent_b,
            "engineOfA": self.engine_a,
            "engineOfB": self.engine_b,
            "seeds": self.seeds,
            "scoreA": self.mean_a,
            "scoreB": self.mean_b,
            "difference": self.difference,
            "standardError": self.standard_error,
            "low": self.low,
            "high": self.high,
            "settled": self.settled,
        }


def compare(first: Evaluation, second: Evaluation) -> PairedDifference:
    """The paired difference of agent A's score between two evaluations of the same game and seeds."""
    if first.stamp.content_hash != second.stamp.content_hash:
        raise ArtifactError(
            "These evaluations played different content "
            f"({first.stamp.content_hash[:12]} and {second.stamp.content_hash[:12]}), so their scores are "
            "not comparable at all, paired or otherwise."
        )

    # The content hash covers the catalogue and not the rules it is played under. A round cap or a critical
    # multiplier that moved changes the game itself, and the gap it opens has nothing to do with the agents --
    # which is exactly the difference this module exists to attribute to them.
    if dict(first.stamp.rule_set) != dict(second.stamp.rule_set):
        changed = sorted(
            key
            for key in dict(first.stamp.rule_set).keys() | dict(second.stamp.rule_set).keys()
            if dict(first.stamp.rule_set).get(key) != dict(second.stamp.rule_set).get(key)
        )
        raise ArtifactError(
            f"These evaluations played different rules ({', '.join(changed)}), so a difference between them "
            "is about the game as much as about the agents."
        )

    left, right = scores_by_seed(first), scores_by_seed(second)
    if left.keys() != right.keys():
        only_left, only_right = sorted(left.keys() - right.keys()), sorted(right.keys() - left.keys())
        raise ArtifactError(
            f"These evaluations played different seeds ({len(left)} and {len(right)}, "
            f"{len(left.keys() & right.keys())} shared), so there is no pairing to do. "
            f"Only in the first: {only_left[:5]}. Only in the second: {only_right[:5]}."
        )

    # One observation cannot estimate how much it would have moved, so there is no interval to report and no
    # question to settle. Reporting the difference with a degenerate interval would turn one seed into
    # certainty, which is the thing this module exists to stop (ADR 0049).
    if len(left) < 2:
        raise ArtifactError(
            f"A paired reading needs at least two seeds; these carry {len(left)}. One observation estimates "
            "no variance, so the difference has no interval and settles nothing (ADR 0049)."
        )

    seeds = sorted(left)
    differences = [left[seed] - right[seed] for seed in seeds]
    count = len(differences)
    mean = sum(differences) / count
    variance = sum((value - mean) ** 2 for value in differences) / (count - 1)
    return PairedDifference(
        agent_a=first.agent_a.agent,
        agent_b=second.agent_a.agent,
        opponent_a=first.agent_b.agent,
        opponent_b=second.agent_b.agent,
        engine_a=first.stamp.engine_version,
        engine_b=second.stamp.engine_version,
        seeds=count,
        mean_a=sum(left[seed] for seed in seeds) / count,
        mean_b=sum(right[seed] for seed in seeds) / count,
        difference=mean,
        standard_error=math.sqrt(variance / count),
    )


def format_paired(result: PairedDifference) -> str:
    """What the comparison measured, and plainly whether it settles anything."""
    lines = [
        f"{result.agent_a}",
        f"  against {result.opponent_a}, scored {result.mean_a:.4f}",
        f"{result.agent_b}",
        f"  against {result.opponent_b}, scored {result.mean_b:.4f}",
        "",
        f"Paired over {result.seeds} seed(s), the first minus the second:",
        f"  difference {result.difference:+.4f}, anywhere from {result.low:+.4f} to {result.high:+.4f}.",
        "",
    ]
    if result.settled:
        direction = "above" if result.difference > 0 else "below"
        lines.append(
            f"  The interval is clear of zero, so the first scores measurably {direction} the second."
        )
    else:
        lines.append("  The interval covers zero, so this does not settle a difference in either direction.")
    if result.opponent_a != result.opponent_b:
        lines.extend(
            [
                "",
                "  The two played different opponents, so this difference is about the opponent as much as",
                "  about the agent. Read it as such, or compare against one opponent.",
            ]
        )
    # Reported rather than refused: two builds of the engine can play a seed identically -- ci-150 reproduced
    # ci-149 to sixteen decimal places on a different one -- so a rebuild is not on its own a reason to throw
    # a comparison away. It is a reason to say so, because it need not be.
    if result.engine_a != result.engine_b:
        lines.extend(
            [
                "",
                f"  The two were played by different engine builds ({result.engine_a} and "
                f"{result.engine_b}).",
                "  Rebuilding need not change how a seed plays, and often does not, but it can.",
            ]
        )
    return "\n".join(lines)


def paired_difference(first: Path, second: Path, output: Path | None = None) -> PairedDifference:
    """Compares two evaluation files, writing ``paired.json`` beside the output path when one is given."""
    result = compare(load_evaluation(Path(first)), load_evaluation(Path(second)))
    if output is not None:
        destination = Path(output)
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_text(json.dumps(result.to_json(), indent=2) + "\n", encoding="utf-8")
    return result
