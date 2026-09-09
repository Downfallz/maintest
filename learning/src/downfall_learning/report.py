"""``training.jsonl``: one line per iteration, the contract the viewer reads (docs/learning/artifacts.md)."""

from __future__ import annotations

import json
from collections.abc import Mapping
from dataclasses import dataclass, field, replace
from pathlib import Path
from typing import Any

from downfall_learning.stamps import RunStamp

TRAINING_FILE = "training.jsonl"


@dataclass(frozen=True)
class TrainingRow:
    iteration: int
    loss: float
    win_rate: float | None = None
    win_rate_low: float | None = None
    win_rate_high: float | None = None
    matches: int | None = None
    best: bool = False
    extra: Mapping[str, float] = field(default_factory=dict)

    def to_json(self, stamp: RunStamp | None = None) -> dict[str, Any]:
        row: dict[str, Any] = {"iteration": self.iteration, "loss": self.loss}
        if self.win_rate is not None:
            row["winRate"] = self.win_rate
        if self.win_rate_low is not None and self.win_rate_high is not None:
            row["winRateLow"] = self.win_rate_low
            row["winRateHigh"] = self.win_rate_high
        if self.matches is not None:
            row["matches"] = self.matches
        if self.best:
            row["best"] = True
        row.update(self.extra)
        if stamp is not None:
            row["stamp"] = stamp.to_json()
        return row


class TrainingLog:
    """Collects the rows of one training and keeps ``training.jsonl`` current after every append.

    The file is rewritten whole each time (it is small), so an interrupted training still leaves a readable
    file, and ``mark_best`` can move the ``best`` flag once the run knows which iteration it keeps.
    """

    def __init__(self, stamp: RunStamp | None = None, path: Path | None = None) -> None:
        self._stamp = stamp
        self._path = None if path is None else Path(path)
        self._rows: list[TrainingRow] = []

    @property
    def stamp(self) -> RunStamp | None:
        return self._stamp

    @stamp.setter
    def stamp(self, stamp: RunStamp | None) -> None:
        """A search learns its stamp from the first evaluation, after the log exists."""
        self._stamp = stamp
        self._write()

    @property
    def rows(self) -> tuple[TrainingRow, ...]:
        return tuple(self._rows)

    @property
    def path(self) -> Path | None:
        return self._path

    def append(self, row: TrainingRow) -> None:
        if self._rows and row.iteration <= self._rows[-1].iteration:
            raise ValueError(f"Iteration {row.iteration} does not follow {self._rows[-1].iteration}.")
        self._rows.append(row)
        self._write()

    def mark_best(self, iteration: int) -> None:
        if all(row.iteration != iteration for row in self._rows):
            raise ValueError(f"No iteration {iteration} to mark as best.")
        self._rows = [replace(row, best=row.iteration == iteration) for row in self._rows]
        self._write()

    def best(self) -> TrainingRow | None:
        return next((row for row in self._rows if row.best), None)

    def _write(self) -> None:
        if self._path is None:
            return
        self._path.parent.mkdir(parents=True, exist_ok=True)
        lines = [
            json.dumps(row.to_json(self._stamp if index == 0 else None))
            for index, row in enumerate(self._rows)
        ]
        self._path.write_text("".join(line + "\n" for line in lines), encoding="utf-8")
