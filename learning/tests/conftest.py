"""Synthetic runs and evaluations shaped like the engine's artifacts (docs/learning/artifacts.md)."""

from __future__ import annotations

import json
import sys
from collections.abc import Callable, Mapping
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import numpy as np
import pytest

REPO_ROOT = Path(__file__).resolve().parents[2]
SAMPLE_RUN = REPO_ROOT / "viewer" / "samples" / "random-vs-random"
SAMPLE_EVALUATION = REPO_ROOT / "viewer" / "samples" / "evaluation.json"

SCHEMA_ID = "features:v1+0123456789ab"
FEATURE_NAMES = (
    "round_fraction",
    "phase",
    "own0_alive",
    "own0_health_fraction",
    "enemy0_alive",
    "enemy0_health_fraction",
)
SPELL_A = "intent:0:spell:a:v1"
SPELL_B = "intent:0:spell:b:v1"
UNLOCK = "evolve:0:spell:c:v1"


def stamp_json(**overrides: Any) -> dict[str, Any]:
    stamp: dict[str, Any] = {
        "engineVersion": "abcdef123456",
        "contentHash": "c" * 64,
        "ruleSet": {
            "teamSize": 3,
            "energyPerRound": 2,
            "evolutionPicksPerRound": 2,
            "roundCap": 30,
            "criticalMultiplier": 2.0,
        },
        "featureSchema": SCHEMA_ID,
        "player1Agent": "Greedy",
        "player2Agent": "Greedy",
        "baseSeed": 1,
    }
    stamp.update(overrides)
    return stamp


def rule_prefer_a_when_healthier(observation: np.ndarray) -> str:
    return SPELL_A if observation[3] > observation[5] else SPELL_B


def write_run(
    directory: Path,
    matches: int = 12,
    steps_per_episode: int = 3,
    seed: int = 0,
    rule: Callable[[np.ndarray], str] = rule_prefer_a_when_healthier,
    stamp: Mapping[str, Any] | None = None,
    with_evolution: bool = True,
) -> Path:
    """Writes a run whose intents follow ``rule`` and whose returns follow the health margin of the episode.

    Every step of an episode shares the episode's observation, so a value regression per action key can
    recover the return from the observation: agent A's return is ``0.9 x (own health - enemy health)`` when
    it took spell A and the opposite when it took spell B, which the rule makes positive either way.
    """
    rng = np.random.default_rng(seed)
    stamp = dict(stamp or stamp_json())
    directory.mkdir(parents=True, exist_ok=True)
    steps: list[dict[str, Any]] = []
    episodes: list[dict[str, Any]] = []
    for match in range(matches):
        match_id = f"00000000-0000-0000-0000-{match:012d}"
        observation = rng.uniform(0.0, 1.0, size=len(FEATURE_NAMES))
        observation[2] = 1.0
        observation[4] = 1.0
        for slot in ("Player1", "Player2"):
            own = observation if slot == "Player1" else _mirror(observation)
            action = rule(own)
            margin = own[3] - own[5]
            return_value = 0.9 * margin if action == SPELL_A else -0.9 * margin
            step = _StepWriter(match_id, slot, stamp["featureSchema"], own)
            if with_evolution:
                steps.append(step.write("Evolution", "Evolve", (UNLOCK, "pass"), UNLOCK))
            for _ in range(steps_per_episode):
                steps.append(step.write("Intent", "Intent", (SPELL_A, SPELL_B), action))
            episodes.append(
                {
                    "matchId": match_id,
                    "seed": match + 1,
                    "slot": slot,
                    "steps": steps_per_episode + int(with_evolution),
                    "rounds": 7,
                    "outcome": {"winner": "Player1", "reason": "Elimination", "isDraw": False},
                    "remainingHealth": 10,
                    "enemyRemainingHealth": 0,
                    "return": round(float(return_value), 6),
                }
            )
    manifest = {
        "stamp": stamp,
        "createdAt": "2026-09-08T18:30:00+00:00",
        "schemaId": stamp["featureSchema"],
        "schemaVersion": stamp["featureSchema"].split("+")[0],
        "featureNames": list(FEATURE_NAMES),
        "matches": matches,
        "steps": len(steps),
        "episodes": len(episodes),
        "traces": False,
    }
    (directory / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    (directory / "steps.jsonl").write_text(
        "".join(json.dumps(step) + "\n" for step in steps), encoding="utf-8"
    )
    (directory / "episodes.jsonl").write_text(
        "".join(json.dumps(episode) + "\n" for episode in episodes), encoding="utf-8"
    )
    return directory


def _mirror(observation: np.ndarray) -> np.ndarray:
    mirrored = observation.copy()
    mirrored[2:4], mirrored[4:6] = observation[4:6], observation[2:4]
    return mirrored


@dataclass(frozen=True)
class _StepWriter:
    match_id: str
    slot: str
    schema_id: str
    observation: np.ndarray

    def write(self, sub_phase: str, kind: str, candidates: tuple[str, ...], action: str) -> dict[str, Any]:
        features = [round(float(value), 4) for value in self.observation]
        return {
            "matchId": self.match_id,
            "slot": self.slot,
            "round": 1,
            "subPhase": sub_phase,
            "kind": kind,
            "observation": {"schemaId": self.schema_id, "features": features},
            "candidates": list(candidates),
            "action": action,
            "code": {"kind": kind, "actingSlot": 0, "spellIndex": 0, "speed": -1, "targetMask": 0},
        }


def evaluation_json(
    score: float, win_rate: float, matches: int = 400, stamp: Mapping[str, Any] | None = None
) -> dict[str, Any]:
    """An ``evaluation.json`` with the fields the search reads, agent A scoring ``score``."""

    def report(agent: str, mean: float, rate: float) -> dict[str, Any]:
        return {
            "agent": agent,
            "wins": round(rate * matches),
            "winRate": {"mean": rate, "low": max(0.0, rate - 0.05), "high": min(1.0, rate + 0.05)},
            "score": {"mean": mean, "low": max(0.0, mean - 0.05), "high": min(1.0, mean + 0.05)},
            "averageRemainingHealth": 12.5,
            "spellUsage": {"spell:a:v1": 10},
            "spellEntropy": 0.0,
            "actions": 20,
            "fizzles": 1,
            "criticals": 1,
            "fizzleRate": 0.05,
            "criticalRate": 0.05,
        }

    return {
        "stamp": dict(stamp or stamp_json(player1Agent="Heuristic:w.json@deadbeef", player2Agent="Greedy")),
        "agentA": report("Heuristic:w.json@deadbeef", score, win_rate),
        "agentB": report("Greedy", 1.0 - score, 1.0 - win_rate),
        "matches": matches,
        "draws": 0,
        "drawRate": 0.0,
        "averageRounds": 12.0,
        "roundCapShare": 0.0,
        "pairs": [],
    }


FAKE_ENGINE = """
import json
import sys
from pathlib import Path

TARGET = {"damage": 1.0, "kill": 8.0, "heal": 0.8, "stun": 3.0, "bleed": 0.8, "buff": 0.5, "energy": 0.2}
TARGET["risk"] = 1.0


def main() -> int:
    arguments = sys.argv[1:]
    if not arguments or arguments[0] != "evaluate":
        print("unknown command", file=sys.stderr)
        return 2
    options = dict(zip(arguments[1::2], arguments[2::2]))
    weights = json.loads(Path(options["--p1"].split(":", 1)[1]).read_text())
    if weights.get("damage", 1.0) < 0:
        print("negative damage", file=sys.stderr)
        return 1
    distance = sum((weights.get(name, target) - target) ** 2 for name, target in TARGET.items())
    score = max(0.0, min(1.0, 1.0 - 0.02 * distance))
    evaluation = json.loads(Path(__file__).with_name("template.json").read_text())
    for name in ("score", "winRate"):
        low, high = max(0.0, score - 0.05), min(1.0, score + 0.05)
        evaluation["agentA"][name] = {"mean": score, "low": low, "high": high}
    Path(options["--out"]).write_text(json.dumps(evaluation))
    return 0


if __name__ == "__main__":
    sys.exit(main())
"""


@pytest.fixture
def fake_engine(tmp_path: Path) -> list[str]:
    """A command that behaves like the engine's ``evaluate``: the closer to a target, the higher the score."""
    script = tmp_path / "fake_engine.py"
    script.write_text(FAKE_ENGINE, encoding="utf-8")
    (tmp_path / "template.json").write_text(json.dumps(evaluation_json(0.5, 0.5)), encoding="utf-8")
    return [sys.executable, str(script)]


@pytest.fixture
def run_directory(tmp_path: Path) -> Path:
    return write_run(tmp_path / "run")
