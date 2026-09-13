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


def humanize(seconds: float) -> str:
    """``4m21s``, ``1h04m``, ``12s``. Two units at most: the third never changed a decision."""
    seconds = max(0.0, seconds)
    if seconds < 60:
        return f"{seconds:.0f}s"
    minutes, rest = divmod(int(seconds), 60)
    if minutes < 60:
        return f"{minutes}m{rest:02d}s"
    hours, minutes = divmod(minutes, 60)
    return f"{hours}h{minutes:02d}m"


@dataclass
class Progress:
    """A counter that prints where it is, how fast, and how much longer.

    ``total`` may be an upper bound rather than an exact count -- a tuning sweep skips the moves its own
    constraints refuse, so it plays *at most* two per knob. Pass ``bounded=True`` and the line says "of at
    most", because a percentage that only ever overstates is worse than one that admits what it is.
    """

    label: str
    total: int | None = UNKNOWN_TOTAL
    bounded: bool = False
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
        if self.total is None:
            return str(self.done)
        of = "of at most" if self.bounded else "of"
        return f"{self.done} {of} {self.total}"

    def _remaining(self) -> float | None:
        """Seconds left at the rate so far, or None when there is no total or nothing to divide by.

        Not reported for a bounded total: the run may stop well short of it, so an estimate built on it would
        be an overstatement dressed as a measurement.
        """
        if self.total is None or self.bounded or self.done == 0 or self.done >= self.total:
            return None
        return self.elapsed / self.done * (self.total - self.done)
