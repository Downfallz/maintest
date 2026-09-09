import json
from pathlib import Path

import pytest

from conftest import stamp_json
from downfall_learning.report import TrainingLog, TrainingRow
from downfall_learning.stamps import RunStamp


def test_the_file_follows_the_viewer_contract(tmp_path: Path) -> None:
    log = TrainingLog(RunStamp.from_json(stamp_json()), tmp_path / "training.jsonl")

    log.append(TrainingRow(1, 0.9, win_rate=0.4, win_rate_low=0.3, win_rate_high=0.5, matches=400))
    log.append(TrainingRow(2, 0.8, win_rate=0.6, win_rate_low=0.5, win_rate_high=0.7, matches=400))
    log.mark_best(2)

    rows = [json.loads(line) for line in (tmp_path / "training.jsonl").read_text().splitlines()]
    assert [row["iteration"] for row in rows] == [1, 2]
    assert rows[0]["stamp"] == stamp_json()
    assert "stamp" not in rows[1]
    assert rows[1] == {
        "iteration": 2,
        "loss": 0.8,
        "winRate": 0.6,
        "winRateLow": 0.5,
        "winRateHigh": 0.7,
        "matches": 400,
        "best": True,
    }
    assert "best" not in rows[0]
    assert log.best() is not None
    assert log.best().iteration == 2


def test_optional_fields_stay_out_when_unknown(tmp_path: Path) -> None:
    log = TrainingLog(RunStamp.from_json(stamp_json()), tmp_path / "training.jsonl")

    log.append(TrainingRow(1, 0.5, extra={"accuracy": 0.75}))

    row = json.loads((tmp_path / "training.jsonl").read_text())
    assert row == {"iteration": 1, "loss": 0.5, "accuracy": 0.75, "stamp": stamp_json()}


def test_iterations_must_increase() -> None:
    log = TrainingLog(RunStamp.from_json(stamp_json()))
    log.append(TrainingRow(2, 0.5))
    repeated = TrainingRow(2, 0.4)

    with pytest.raises(ValueError, match="does not follow"):
        log.append(repeated)


def test_marking_an_unknown_iteration_is_refused() -> None:
    log = TrainingLog(RunStamp.from_json(stamp_json()))
    log.append(TrainingRow(1, 0.5))

    with pytest.raises(ValueError, match="No iteration 3"):
        log.mark_best(3)


def test_a_log_without_a_path_only_remembers(tmp_path: Path) -> None:
    log = TrainingLog()
    log.append(TrainingRow(1, 0.5))
    log.stamp = RunStamp.from_json(stamp_json())

    assert log.path is None
    assert log.rows[0].loss == 0.5
    assert log.best() is None
    assert list(tmp_path.iterdir()) == []


def test_the_stamp_can_arrive_after_the_first_row(tmp_path: Path) -> None:
    log = TrainingLog(path=tmp_path / "training.jsonl")
    log.append(TrainingRow(1, 0.5))
    assert "stamp" not in json.loads((tmp_path / "training.jsonl").read_text())

    log.stamp = RunStamp.from_json(stamp_json())

    assert json.loads((tmp_path / "training.jsonl").read_text())["stamp"] == stamp_json()
