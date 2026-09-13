"""The progress reporter: what it prints, where it prints it, and when it refuses to guess."""

from __future__ import annotations

import io
import sys

import pytest

from downfall_learning.progress import Progress, humanize


class Clock:
    """A hand-wound clock, so an elapsed time in an assertion is a number and not a flake."""

    def __init__(self) -> None:
        self.now = 0.0

    def __call__(self) -> float:
        return self.now


def reporter(**overrides: object) -> tuple[Progress, io.StringIO, Clock]:
    stream, clock = io.StringIO(), Clock()
    return Progress(label="tune", stream=stream, clock=clock, **overrides), stream, clock  # type: ignore[arg-type]


@pytest.mark.parametrize(
    ("seconds", "text"),
    [(0, "0s"), (9.4, "9s"), (59, "59s"), (60, "1m00s"), (261, "4m21s"), (3600, "1h00m"), (7500, "2h05m")],
)
def test_a_duration_reads_as_at_most_two_units(seconds: float, text: str) -> None:
    assert humanize(seconds) == text


def test_a_step_reports_where_it_is_and_how_much_is_left() -> None:
    progress, stream, clock = reporter(total=4)

    clock.now = 30.0
    progress.step()

    assert stream.getvalue().strip() == "[tune] · 1 of 4 · 30s · ~1m30s left"


def test_the_note_of_a_step_rides_on_the_same_line() -> None:
    progress, stream, clock = reporter(total=2)

    clock.now = 10.0
    progress.step("best 12.95")

    assert stream.getvalue().strip() == "[tune] · 1 of 2 · 10s · ~10s left · best 12.95"


def test_a_run_that_cannot_say_how_long_it_is_reports_only_what_it_knows() -> None:
    """No total means no percentage and no estimate: a count and a clock still say it is alive."""
    progress, stream, clock = reporter()

    clock.now = 5.0
    progress.step()

    assert stream.getvalue().strip() == "[tune] · 1 · 5s"


def test_a_bounded_total_says_so_and_estimates_nothing() -> None:
    """The tuning sweep skips the moves its constraints refuse, so its total is a ceiling.

    An estimate built on a ceiling only ever overstates, which is worse than no estimate: it reads like a
    measurement and is not one.
    """
    progress, stream, clock = reporter(total=100, bounded=True)

    clock.now = 60.0
    progress.step()

    assert stream.getvalue().strip() == "[tune] · 1 of at most 100 · 1m00s"


def test_nothing_is_estimated_before_the_first_step_or_after_the_last() -> None:
    progress, stream, clock = reporter(total=1)

    progress.write()
    clock.now = 12.0
    progress.step()

    assert stream.getvalue().splitlines() == ["[tune] · 0 of 1 · 0s", "[tune] · 1 of 1 · 12s"]


def test_the_closing_line_counts_what_was_done_and_estimates_nothing() -> None:
    progress, stream, clock = reporter(total=9)

    progress.step()
    clock.now = 75.0
    progress.finish("best 5.61")

    assert stream.getvalue().splitlines()[-1] == "[tune] · 1 done · 1m15s · best 5.61"


def test_a_quiet_reporter_still_counts_but_prints_nothing() -> None:
    """--quiet must not change what a run does, only what it says."""
    progress, stream, _ = reporter(total=3, quiet=True)

    progress.step()
    progress.finish()

    assert progress.done == 1
    assert stream.getvalue() == ""


def test_progress_defaults_to_stderr_so_it_never_lands_in_the_report() -> None:
    """stdout is the report a person reads and a script parses; the two must not interleave.

    Read at construction rather than frozen at import, so a harness that replaces `sys.stderr` -- pytest's
    capture, a notebook -- gets the stream it installed and not the one that existed first.
    """
    assert Progress(label="tune").stream is sys.stderr
