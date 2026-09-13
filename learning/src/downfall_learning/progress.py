"""Where a long run has got to, one line per unit of work.

A tuning pass plays several hundred evaluations and takes hours; a weight search is not far behind. Both used
to print their first character when they were finished, which makes a run indistinguishable from a hang and
an ETA impossible to form. This says where it is while it is there.

Progress goes to **stderr**, never stdout: stdout is the report a person reads and a script parses, and the
two must not interleave. One line per completed unit rather than a redrawn spinner, so it reads the same in a
terminal, in a log file and in CI.
"""

from __future__ import annotations

import sys
import time
from collections.abc import Callable
from dataclasses import dataclass, field
from typing import TextIO

#: What a run reports when it cannot say how much is left: enough to see it is alive and how fast.
UNKNOWN_TOTAL = None


def silent() -> Progress:
    """A reporter that counts and prints nothing.

    The default every command gets when its caller passes none, so the loops that report can call it
    unconditionally. Guarding each call with ``if progress is not None`` put a branch inside two hill
    climbs for the sake of a caller that does not exist, which is how both of them went over the cognitive
    complexity the quality gate allows.
    """
    return Progress(label="", quiet=True)


def humanize(seconds: float) -> str:
    """``4m21s``, ``1h04m``, ``12s``. Two units at most: the third never changed a decision."""
    span = max(0.0, seconds)
    if span < 60:
        return f"{span:.0f}s"
    total_minutes, remaining_seconds = divmod(int(span), 60)
    if total_minutes < 60:
        return f"{total_minutes}m{remaining_seconds:02d}s"
    hours, remaining_minutes = divmod(total_minutes, 60)
    return f"{hours}h{remaining_minutes:02d}m"


@dataclass
class Progress:
    """A counter that prints where it is, how fast, and how much longer.

    ``total`` starts unknown and may be set once the run knows it: a tuning pass cannot say how many
    candidates its opening will play until the opening has played them, and it would rather count up than
    advertise a ceiling. An earlier version did advertise one and the ceiling was wrong in the direction that
    matters -- the paired moves it forgot could push the count past its own "of at most". Counting up says
    less and cannot be false.
    """

    label: str
    total: int | None = UNKNOWN_TOTAL
    quiet: bool = False
    stream: TextIO = field(default_factory=lambda: sys.stderr)
    clock: Callable[[], float] = time.monotonic
    done: int = 0
    started: float = field(init=False)

    def __post_init__(self) -> None:
        self.started = self.clock()

    @property
    def elapsed(self) -> float:
        return self.clock() - self.started

    def step(self, note: str = "") -> None:
        """Count one unit of work and say so."""
        self.done += 1
        self.write(note)

    def write(self, note: str = "") -> None:
        if self.quiet:
            return
        parts = [f"[{self.label}]", self._counted(), humanize(self.elapsed)]
        if (left := self._remaining()) is not None:
            parts.append(f"~{humanize(left)} left")
        if note:
            parts.append(note)
        print(" · ".join(parts), file=self.stream, flush=True)

    def finish(self, note: str = "") -> None:
        """The closing line: what it did and how long it took, with no estimate left to make."""
        if self.quiet:
            return
        parts = [f"[{self.label}]", f"{self.done} done", humanize(self.elapsed)]
        if note:
            parts.append(note)
        print(" · ".join(parts), file=self.stream, flush=True)

    def _counted(self) -> str:
        return str(self.done) if self.total is None else f"{self.done} of {self.total}"

    def _remaining(self) -> float | None:
        """Seconds left at the rate so far, or None when there is no total or nothing to divide by."""
        if self.total is None or self.done == 0 or self.done >= self.total:
            return None
        return self.elapsed / self.done * (self.total - self.done)
