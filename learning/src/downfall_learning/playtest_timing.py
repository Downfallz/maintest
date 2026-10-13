"""How long recorded playtests took, per decision and per round, under each combat declaration (ADR 0110).

free-activation-plan.md §5 asks whether choosing spells at the slot makes a round faster or slower at the
table. Under hidden intents both players think at once while declaring; at the slot one player thinks while
the other waits, at every slot. The bots cannot answer that, so this reads the sessions people played: each
session's ``notes.jsonl`` (docs/tabletop/playtest-app.md §5.3), whose ``Decision`` notes say when an answer
was accepted and how long its question had been on a screen, and its ``manifest.json``, whose stamp names
the declaration.

**A round is timed only for sessions said to be played on two devices.** Nothing a session records tells two
devices from one hotseat screen, and the difference is the whole measurement: a hotseat page shows one seat at
a time and stamps only the seat on screen, so its rounds read as serial under either rule.
"""

from __future__ import annotations

import json
import statistics
from collections import defaultdict
from collections.abc import Iterable, Sequence
from dataclasses import dataclass, field
from datetime import datetime, timedelta
from pathlib import Path

NOTES = "notes.jsonl"
MANIFEST = "manifest.json"
HIDDEN_INTENT = "HiddenIntent"
AT_ACTIVATION = "AtActivation"
DECLARATIONS = (HIDDEN_INTENT, AT_ACTIVATION)
#: The sub-phases a round's combat asks in. A pick is asked of both seats at once under either rule (ADR 0092)
#: and only every other round, so it would pad the round with time the declaration has nothing to do with; a
#: tie order writes no note (playtest-app.md §5.3).
COMBAT = frozenset({"Speed", "IntentSelection", "Activation"})
#: What a decision is called in the report, by the sub-phase it was asked in. The activation is named by the
#: declaration (:func:`asked`).
NAMES = {"Evolution": "pick", "Speed": "speed", "IntentSelection": "intent"}


class TimingError(ValueError):
    """A session directory the reading cannot use."""


@dataclass(frozen=True)
class Decision:
    """One accepted answer of a person: who, where in the match, when, and how long it was on a screen.

    ``elapsed`` is ``None`` when the host could not say the question was ever on a screen (a tap that beat the
    page's acknowledgement): the answer still closes its round, but its thinking is unknown.
    """

    slot: str
    round: int
    sub_phase: str
    accepted: datetime
    elapsed: timedelta | None

    @property
    def served(self) -> datetime | None:
        return None if self.elapsed is None else self.accepted - self.elapsed


@dataclass
class Session:
    """One recorded table: its declaration, the decisions its people made, and the lines it could not read."""

    name: str
    declaration: str
    decisions: list[Decision] = field(default_factory=list)
    unreadable: int = 0

    @property
    def persons(self) -> set[str]:
        return {decision.slot for decision in self.decisions}

    def rounds(self) -> Iterable[list[Decision]]:
        """Each round's combat decisions, in round order."""
        by_round: dict[int, list[Decision]] = defaultdict(list)
        for decision in self.decisions:
            if decision.sub_phase in COMBAT:
                by_round[decision.round].append(decision)
        return [by_round[number] for number in sorted(by_round)]


def moment(text: str) -> datetime:
    """The host's UTC moment. .NET writes seven fractional digits, one more than Python 3.11 reads."""
    head, dot, rest = text.replace("Z", "+00:00").partition(".")
    if not dot:
        return datetime.fromisoformat(head)
    digits = len(rest) - len(rest.lstrip("0123456789"))
    return datetime.fromisoformat(f"{head}.{rest[:digits][:6].ljust(6, '0')}{rest[digits:]}")


def load_session(directory: Path) -> Session:
    """A session directory as the host recorded it, or as an export unzipped under ``runs/``."""
    notes, manifest = directory / NOTES, directory / MANIFEST
    if not notes.is_file() or not manifest.is_file():
        raise TimingError(f"{directory}: no {NOTES} and {MANIFEST} here; is it a recorded session?")
    rules = json.loads(manifest.read_text(encoding="utf-8")).get("stamp", {}).get("ruleSet", {})
    # The stamp names the declaration only when it is not the default (ADR 0110, PR 2).
    session = Session(directory.name, rules.get("combatDeclaration", HIDDEN_INTENT))
    for line in notes.read_text(encoding="utf-8").splitlines():
        if not line.strip():
            continue
        try:
            note = json.loads(line)
        except json.JSONDecodeError:
            # A host stopped mid-write leaves its last line cut; the lines before it are still the session.
            session.unreadable += 1
            continue
        if note.get("kind") != "Decision" or note.get("round") is None:
            continue
        elapsed = note.get("elapsedMs")
        session.decisions.append(
            Decision(
                slot=str(note["slot"]),
                round=int(note["round"]),
                sub_phase=str(note.get("subPhase")),
                accepted=moment(note["at"]),
                elapsed=None if elapsed is None else timedelta(milliseconds=int(elapsed)),
            )
        )
    return session


def asked(decision: Decision, declaration: str) -> str:
    """What the decision was: at the slot an activation is an action, under hidden intents its targets."""
    if decision.sub_phase == "Activation":
        return "action at the slot" if declaration == AT_ACTIVATION else "targets at the slot"
    return NAMES.get(decision.sub_phase, decision.sub_phase)


@dataclass
class Reading:
    """Everything read under one declaration, over its sessions."""

    sessions: int = 0
    timed_sessions: int = 0
    thinking: dict[str, list[float]] = field(default_factory=lambda: defaultdict(list))
    round_seconds: list[float] = field(default_factory=list)
    seat_seconds: list[float] = field(default_factory=list)
    overlap: list[float] = field(default_factory=list)
    untimed_rounds: int = 0

    def add(self, session: Session, two_devices: bool) -> None:
        """A session's decisions, and its rounds when it was played by two people on two devices."""
        self.sessions += 1
        for decision in session.decisions:
            if decision.elapsed is not None:
                self.thinking[asked(decision, session.declaration)].append(decision.elapsed.total_seconds())
        if not two_devices or len(session.persons) < 2:
            return
        self.timed_sessions += 1
        for decisions in session.rounds():
            self._round(decisions)

    def _round(self, decisions: Sequence[Decision]) -> None:
        """One round's combat, both seats: its time, each seat's thinking, and their overlap.

        A round one seat answered nothing in is not a round of two people thinking; one with an answer of
        unknown duration has no known start or thinking sum. Both are counted as untimed rather than read
        short.
        """
        seats = {decision.slot for decision in decisions}
        served = [decision.served for decision in decisions]
        if len(seats) < 2 or any(start is None for start in served):
            self.untimed_rounds += 1
            return
        wall = (max(d.accepted for d in decisions) - min(s for s in served if s is not None)).total_seconds()
        if wall <= 0:
            self.untimed_rounds += 1
            return
        thought = {
            seat: sum(d.elapsed.total_seconds() for d in decisions if d.slot == seat and d.elapsed)
            for seat in seats
        }
        self.round_seconds.append(wall)
        self.seat_seconds.extend(thought.values())
        self.overlap.append(sum(thought.values()) / wall)

    def summary(self) -> dict:
        return {
            "sessions": self.sessions,
            "timedSessions": self.timed_sessions,
            "decisions": {
                kind: {
                    "count": len(seconds),
                    "median": statistics.median(seconds),
                    "mean": statistics.fmean(seconds),
                }
                for kind, seconds in sorted(self.thinking.items())
            },
            "rounds": len(self.round_seconds),
            "untimedRounds": self.untimed_rounds,
            "roundSeconds": _median(self.round_seconds),
            "seatSecondsPerRound": _median(self.seat_seconds),
            "overlap": _median(self.overlap),
        }


def _median(values: Sequence[float]) -> float | None:
    return statistics.median(values) if values else None


def read_timing(sessions: Iterable[Session], two_devices: bool) -> dict[str, dict]:
    """Every session read under its own declaration, both declarations always present."""
    readings = {declaration: Reading() for declaration in DECLARATIONS}
    for session in sessions:
        readings.setdefault(session.declaration, Reading()).add(session, two_devices)
    return {declaration: reading.summary() for declaration, reading in readings.items()}


def format_timing(summaries: dict[str, dict]) -> str:
    """One column a declaration: the decisions' thinking time, then the rounds played on two devices."""
    a, b = (summaries[name] for name in DECLARATIONS)

    def seconds(value: float | None) -> str:
        return "-" if value is None else f"{value:.1f} s"

    def ratio(value: float | None) -> str:
        return "-" if value is None else f"{value:.2f}"

    def decision(reading: dict, kind: str) -> str:
        found = reading["decisions"].get(kind)
        return (
            "-"
            if not found
            else f"{found['median']:.1f} s median, {found['mean']:.1f} s mean ({found['count']})"
        )

    kinds = sorted(set(a["decisions"]) | set(b["decisions"]))
    rows = [
        (
            "Sessions (timed by round)",
            f"{a['sessions']} ({a['timedSessions']})",
            f"{b['sessions']} ({b['timedSessions']})",
        ),
        *((f"Thinking per {kind}", decision(a, kind), decision(b, kind)) for kind in kinds),
        (
            "Rounds timed (left untimed)",
            f"{a['rounds']} ({a['untimedRounds']})",
            f"{b['rounds']} ({b['untimedRounds']})",
        ),
        ("A round's combat, both seats", seconds(a["roundSeconds"]), seconds(b["roundSeconds"])),
        ("One seat's thinking in it", seconds(a["seatSecondsPerRound"]), seconds(b["seatSecondsPerRound"])),
        ("Overlap (both seats' thinking over the round)", ratio(a["overlap"]), ratio(b["overlap"])),
    ]
    return "\n".join(
        [
            "| | hidden intents | at the slot |",
            "| --- | --- | --- |",
            *(f"| {name} | {left} | {right} |" for name, left, right in rows),
            "",
            "Round times are medians over the combat (speed to the last slot, picks left out) of the rounds "
            "both people answered in, from sessions played on two devices.",
        ]
    )
