import json
from pathlib import Path

import pandas as pd
import pytest

from conftest import FEATURE_NAMES, write_run
from downfall_learning.artifacts import build_dataset, load_run
from downfall_learning.export import DEFAULT_WEIGHTS, export_wide_csv, read_weights, wide_frame, write_weights


def test_weights_are_written_as_the_engine_reads_them(tmp_path: Path) -> None:
    path = write_weights(tmp_path / "w" / "weights.json", {"kill": 8, "risk": 1.5})

    assert json.loads(path.read_text()) == {"kill": 8.0, "risk": 1.5}
    assert read_weights(path) == {**DEFAULT_WEIGHTS, "kill": 8.0, "risk": 1.5}


def test_the_built_in_weights_file_matches_the_defaults() -> None:
    repo = Path(__file__).resolve().parents[2]

    assert read_weights(repo / "learning" / "weights" / "greedy.json") == dict(DEFAULT_WEIGHTS)


def test_unknown_or_non_finite_weights_are_refused(tmp_path: Path) -> None:
    with pytest.raises(ValueError, match="Unknown weight names: speed"):
        write_weights(tmp_path / "weights.json", {"speed": 1.0})
    with pytest.raises(ValueError, match="finite"):
        write_weights(tmp_path / "weights.json", {"kill": float("nan")})
    (tmp_path / "list.json").write_text("[1, 2]")
    with pytest.raises(ValueError, match="weights object"):
        read_weights(tmp_path / "list.json")
    (tmp_path / "odd.json").write_text('{"tempo": 1}')
    with pytest.raises(ValueError, match="tempo"):
        read_weights(tmp_path / "odd.json")


def test_the_wide_csv_has_one_row_per_step_and_one_column_per_feature(tmp_path: Path) -> None:
    dataset = build_dataset([load_run(write_run(tmp_path / "run", matches=2))])

    frame = wide_frame(dataset)
    path = export_wide_csv(dataset, tmp_path / "out" / "steps.csv")

    assert list(frame.columns) == ["matchId", "kind", *FEATURE_NAMES, "action", "candidates", "return"]
    assert len(frame) == len(dataset)
    read_back = pd.read_csv(path)
    assert len(read_back) == len(dataset)
    assert read_back["candidates"].iloc[0].count("|") == 1
