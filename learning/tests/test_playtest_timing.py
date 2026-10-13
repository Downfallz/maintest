"""The time recorded playtests took, per decision and per round, under each combat declaration (ADR 0110)."""

from __future__ import annotations

import json
from datetime import UTC, datetime, timedelta
from pathlib import Path

import pytest

from downfall_learning import cli
from downfall_learning.playtest_timing import (
    AT_ACTIVATION,
    HIDDEN_INTENT,
    TimingError,
    load_session,
    moment,
    read_timing,
)

START = datetime(2026, 10, 13, 20, 0, 0, tzinfo=UTC)


def note(slot: str, round_: int, sub_phase: str, accepted_at: float, elapsed_ms: int | None) -> dict:
    """A Decision note as the host writes it: seven fractional digits and an offset."""
    at = START + timedelta(seconds=accepted_at)
    return {
        "sessionId": "s",
        "matchId": "m",
        "slot": slot,
        "round": round_,
        "subPhase": sub_phase,
        "at": at.strftime("%Y-%m-%dT%H:%M:%S.%f") + "0+00:00",
        "kind": "Decision",
        "elapsedMs": elapsed_ms,
    }


def session(root: Path, name: str, notes: list[dict], declaration: str | None = None, tail: str = "") -> Path:
    """A recorded session directory: the stamp names the declaration only when it is not the default."""
    directory = root / name
    directory.mkdir()
    rules = {"teamSize": 3} | ({"combatDeclaration": declaration} if declaration else {})
    (directory / "manifest.json").write_text(json.dumps({"stamp": {"ruleSet": rules}}), encoding="utf-8")
    lines = "".join(json.dumps(one) + "\n" for one in notes) + tail
    (directory / "notes.jsonl").write_text(lines, encoding="utf-8")
    return directory


def declared_at_once(round_: int = 1) -> list[dict]:
    """Hidden intents on two devices: speeds, then both intents thought at once, then targets in turn."""
    return [
        note("Player1", round_, "Speed", 4, 4000),
        note("Player2", round_, "Speed", 5, 5000),
        note("Player1", round_, "IntentSelection", 15, 10000),
        note("Player2", round_, "IntentSelection", 15, 10000),
        note("Player1", round_, "Activation", 18, 3000),
        note("Player2", round_, "Activation", 21, 3000),
    ]


def test_a_round_is_timed_from_its_first_served_question_to_its_last_answer(tmp_path: Path) -> None:
    played = load_session(session(tmp_path, "a", declared_at_once()))

    timing = read_timing([played], two_devices=True)[HIDDEN_INTENT]

    assert timing["rounds"] == 1
    assert timing["roundSeconds"] == pytest.approx(21.0)


def test_both_seats_thinking_at_once_reads_as_an_overlap_above_one(tmp_path: Path) -> None:
    played = load_session(session(tmp_path, "a", declared_at_once()))

    timing = read_timing([played], two_devices=True)[HIDDEN_INTENT]

    assert timing["overlap"] == pytest.approx(35 / 21)
    assert timing["seatSecondsPerRound"] == pytest.approx(17.5)


def test_sessions_not_said_to_be_on_two_devices_give_no_round_times(tmp_path: Path) -> None:
    """A hotseat page stamps only the seat on screen: its rounds would read serial under either rule."""
    played = load_session(session(tmp_path, "a", declared_at_once()))

    timing = read_timing([played], two_devices=False)[HIDDEN_INTENT]

    assert timing["rounds"] == 0
    assert timing["timedSessions"] == 0
    assert timing["decisions"]["intent"]["count"] == 2


def test_a_session_one_person_played_gives_no_round_times_even_on_two_devices(tmp_path: Path) -> None:
    alone = [one for one in declared_at_once() if one["slot"] == "Player1"]
    played = load_session(session(tmp_path, "a", alone))

    timing = read_timing([played], two_devices=True)[HIDDEN_INTENT]

    assert timing["rounds"] == 0
    assert timing["timedSessions"] == 0


def test_the_picks_are_left_out_of_a_round_time(tmp_path: Path) -> None:
    """Both seats pick at once under either rule (ADR 0092), and only every other round."""
    picks = [note("Player1", 1, "Evolution", -30, 30000), note("Player2", 1, "Evolution", -20, 40000)]
    played = load_session(session(tmp_path, "a", picks + declared_at_once()))

    timing = read_timing([played], two_devices=True)[HIDDEN_INTENT]

    assert timing["roundSeconds"] == pytest.approx(21.0)
    assert timing["decisions"]["pick"]["count"] == 2


def test_a_round_with_an_answer_of_unknown_duration_is_left_untimed_rather_than_read_short(
    tmp_path: Path,
) -> None:
    notes = declared_at_once()
    notes[-1]["elapsedMs"] = None
    played = load_session(session(tmp_path, "a", notes))

    timing = read_timing([played], two_devices=True)[HIDDEN_INTENT]

    assert timing["rounds"] == 0
    assert timing["untimedRounds"] == 1
    assert timing["decisions"]["targets at the slot"]["count"] == 1


def test_a_round_one_seat_answered_nothing_in_is_left_untimed(tmp_path: Path) -> None:
    second = [one for one in declared_at_once(round_=2) if one["slot"] == "Player1"]
    played = load_session(session(tmp_path, "a", declared_at_once() + second))

    timing = read_timing([played], two_devices=True)[HIDDEN_INTENT]

    assert (timing["rounds"], timing["untimedRounds"]) == (1, 1)


def test_the_declaration_comes_from_the_stamp_and_an_activation_is_named_by_it(tmp_path: Path) -> None:
    at_the_slot = [note("Player1", 1, "Activation", 9, 8000)]
    sessions = [
        load_session(session(tmp_path, "a", declared_at_once())),
        load_session(session(tmp_path, "b", at_the_slot, declaration=AT_ACTIVATION)),
    ]

    timing = read_timing(sessions, two_devices=False)

    assert "targets at the slot" in timing[HIDDEN_INTENT]["decisions"]
    assert set(timing[AT_ACTIVATION]["decisions"]) == {"action at the slot"}


def test_a_cut_last_line_is_skipped_and_counted_and_the_rest_is_read(tmp_path: Path) -> None:
    played = load_session(session(tmp_path, "a", declared_at_once(), tail='{"kind":"Deci'))

    assert played.unreadable == 1
    assert len(played.decisions) == 6


def test_notes_that_are_not_decisions_are_not_read_as_decisions(tmp_path: Path) -> None:
    comment = {"kind": "Comment", "slot": "Player1", "round": 1, "at": "2026-10-13T20:00:00Z", "text": "hi"}
    played = load_session(session(tmp_path, "a", [comment, *declared_at_once()]))

    assert len(played.decisions) == 6


@pytest.mark.parametrize(
    ("text", "expected"),
    [
        ("2026-10-13T20:00:04.1234567+00:00", START + timedelta(seconds=4, microseconds=123456)),
        ("2026-10-13T20:00:04+00:00", START + timedelta(seconds=4)),
        ("2026-10-13T20:00:04.5Z", START + timedelta(seconds=4, microseconds=500000)),
    ],
)
def test_the_hosts_moments_read_whatever_their_fraction(text: str, expected: datetime) -> None:
    assert moment(text) == expected


def test_a_directory_that_is_not_a_session_is_refused_by_name(tmp_path: Path) -> None:
    with pytest.raises(TimingError, match=r"no notes\.jsonl"):
        load_session(tmp_path)


def test_the_command_prints_both_declarations_and_writes_the_summaries(
    tmp_path: Path, capsys: pytest.CaptureFixture
) -> None:
    a = session(tmp_path, "a", declared_at_once())
    b = session(
        tmp_path, "b", [note("Player1", 1, "Activation", 9, 8000)], declaration=AT_ACTIVATION, tail="{"
    )
    output = tmp_path / "timing.json"

    code = cli.main(["playtest-timing", str(a), str(b), "--two-devices", "-o", str(output)])

    assert code == 0
    printed = capsys.readouterr()
    assert "| A round's combat, both seats | 21.0 s | - |" in printed.out
    assert "1 unreadable line(s)" in printed.err
    assert json.loads(output.read_text(encoding="utf-8"))[AT_ACTIVATION]["sessions"] == 1


def test_the_command_refuses_a_directory_that_is_not_a_session(tmp_path: Path) -> None:
    assert cli.main(["playtest-timing", str(tmp_path)]) == 1
