"""Rebuild table/battle-recap.fixture.json, the recorded match the recap's tests read.

    dotnet run --project src/DownfallArena.Cli -- play --seed 1 --trace match.trace.json
    python3 scripts/recap-fixture.py match.trace.json data/dst/game.schema.json \
        table/battle-recap.fixture.json

The trace is filtered as SeatVisibility filters seat player1's feed: the other seat's hidden decisions are
dropped. Frames keep the stats the recap reads, and the board is kept at a few named moments
(docs/tabletop/async-recap-plan.md, stage R2).
"""

import json
import sys
from pathlib import Path

PRIVATE = {
    "IntentSubmitted",
    "SpeedChoiceSubmitted",
    "EvolutionChoiceSubmitted",
    "EvolutionPassed",
    "TieOrderSubmitted",
}
KEPT_ROUNDS = {1, 2, 3, 4, 8, 9, 11, 12, 16, 17}
CREATURE_KEYS = ["id", "owner", "name", "health", "maxHealth", "energy", "totalDefense", "isAlive"]
FRAME_KEYS = ["id", "health", "maxHealth", "energy", "isAlive"]
BOARD_KEYS = [
    "slot",
    "state",
    "roundNumber",
    "phase",
    "subPhase",
    "speedChoices",
    "intents",
    "evolutionChoices",
    "timeline",
    "outcome",
]
DROPPED_EVENT_KEYS = {"matchId", "player", "player1", "player2", "contentHash"}
DROPPED_RESOLUTION_KEYS = {"outcomes", "effectiveTargets", "criticalRoll", "energySpent"}
NOTE = (
    "A recorded 3v3 match (play --seed 1, at-activation), as seat player1 reads it: the seat feed of rounds "
    "1-4, 8-9, 11-12 and 16-17, frames trimmed to the stats the recap reads, and the board at a few named "
    "moments. Rebuilt by scripts/recap-fixture.py."
)


def pick(source: dict, keys: list[str]) -> dict:
    return {key: source[key] for key in keys if key in source}


def trimmed_event(event: dict) -> dict:
    event = {key: value for key, value in event.items() if key not in DROPPED_EVENT_KEYS}
    if "frame" in event:
        frame = event["frame"]
        event["frame"] = {
            "before": [pick(one, FRAME_KEYS) for one in frame["before"]],
            "after": [pick(one, FRAME_KEYS) for one in frame["after"]],
        }
    if "resolution" in event:
        resolution = event["resolution"]
        event["resolution"] = {
            key: value for key, value in resolution.items() if key not in DROPPED_RESOLUTION_KEYS
        }
    return event


def seat_feed(entries: list[dict]) -> list[dict]:
    feed = []
    for entry in entries:
        event = entry["event"]
        if event["kind"] in PRIVATE and event.get("slot") != "Player1":
            continue
        round_number = event.get("roundId", entry["round"])
        if round_number is not None and round_number not in KEPT_ROUNDS:
            continue
        feed.append(
            {
                "sequence": entry["sequence"],
                "round": entry["round"],
                "subPhase": entry["subPhase"],
                "event": trimmed_event(event),
            }
        )
    return feed


def trimmed_board(board: dict) -> dict:
    out = pick(board, BOARD_KEYS)
    out["allies"] = [pick(one, CREATURE_KEYS) for one in board["allies"]]
    out["enemies"] = [pick(one, CREATURE_KEYS) for one in board["enemies"]]
    return out


def named_moments(entries: list[dict]) -> dict[str, int]:
    def first_tempo(round_number: int) -> int:
        return next(
            entry["sequence"]
            for entry in entries
            if entry["event"]["kind"] == "SpeedChoiceSubmitted"
            and entry["event"].get("roundId") == round_number
            and entry["event"].get("slot") == "Player1"
        )

    moments = {f"round{number}Tempo": first_tempo(number) - 1 for number in (1, 2, 4, 9, 12)}
    moments["round4Combat"] = next(
        entry["sequence"]
        for entry in entries
        if entry["event"]["kind"] == "TimelineBuilt" and entry["event"]["roundId"] == 4
    )
    moments["end"] = entries[-1]["sequence"]
    return moments


def main(trace_path: str, schema_path: str, out_path: str) -> None:
    with Path(trace_path).open(encoding="utf-8") as trace_file:
        entries = json.load(trace_file)["entries"]
    with Path(schema_path).open(encoding="utf-8") as schema_file:
        schema = json.load(schema_file)
    feed = seat_feed(entries)
    by_sequence = {entry["sequence"]: entry for entry in entries}
    boards = {
        name: {"sequence": sequence, "board": trimmed_board(by_sequence[sequence]["player1"])}
        for name, sequence in named_moments(entries).items()
    }
    cast = {
        entry["event"]["resolution"]["action"]["spell"]
        for entry in feed
        if entry["event"]["kind"] == "CombatActionResolved"
    }
    catalogue = {
        "cards": [{"id": one["id"], "name": one["name"]} for one in schema["spells"] if one["id"] in cast],
        "packages": [{"id": one["id"], "name": one["name"]} for one in schema["tiers"]],
    }
    fixture = {"note": NOTE, "catalogue": catalogue, "feed": feed, "boards": boards}
    with Path(out_path).open("w", encoding="utf-8") as out_file:
        json.dump(fixture, out_file, separators=(",", ":"))


if __name__ == "__main__":
    main(*sys.argv[1:4])
