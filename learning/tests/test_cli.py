import json
from pathlib import Path

import pytest

from conftest import evaluation_json, stamp_json, write_run
from downfall_learning import cli
from downfall_learning.policy import Policy


def test_train_clone_writes_a_policy_and_its_training_log(
    tmp_path: Path, capsys: pytest.CaptureFixture
) -> None:
    run = write_run(tmp_path / "run", matches=20)

    code = cli.main(
        ["train-clone", str(run), "-o", str(tmp_path / "model"), "--epochs", "3", "--kinds", "Intent"]
    )

    assert code == 0
    policy = Policy.load(tmp_path / "model" / "policy.json")
    assert policy.kind == "clone"
    assert len(policy.action_keys) == 2
    assert len((tmp_path / "model" / "training.jsonl").read_text().splitlines()) == 3
    assert "Policy (clone, 2 actions)" in capsys.readouterr().out


def test_train_value_writes_a_policy(tmp_path: Path) -> None:
    run = write_run(tmp_path / "run", matches=20)

    code = cli.main(["train-value", str(run), "-o", str(tmp_path / "model")])

    assert code == 0
    assert Policy.load(tmp_path / "model" / "policy.json").kind == "value"


def test_search_weights_drives_the_engine_command(
    tmp_path: Path, fake_engine: list[str], capsys: pytest.CaptureFixture
) -> None:
    initial = tmp_path / "initial.json"
    initial.write_text(json.dumps({"kill": 3.0}))

    code = cli.main(
        [
            "search-weights",
            "-o",
            str(tmp_path / "search"),
            "--iterations",
            "2",
            "--population",
            "4",
            "--initial",
            str(initial),
            "--repo",
            str(tmp_path),
            "--engine",
            *fake_engine,
        ]
    )

    assert code == 0
    assert (tmp_path / "search" / "weights.json").is_file()
    assert (tmp_path / "search" / "search.json").is_file()
    assert (tmp_path / "search" / "evaluation.json").is_file()
    printed = capsys.readouterr().out
    assert "The search played 9 evaluation(s)" in printed
    # The line that decides whether to believe the run: a search always reports a best at least as good as
    # its initial, because it keeps the best of what it drew.
    assert "Is it actually better" in printed


def test_export_csv_writes_the_projection(tmp_path: Path, capsys: pytest.CaptureFixture) -> None:
    run = write_run(tmp_path / "run", matches=3)

    code = cli.main(["export-csv", str(run), "-o", str(tmp_path / "steps.csv")])

    assert code == 0
    assert (tmp_path / "steps.csv").read_text().count("\n") == 3 * 2 * 4 + 1
    assert "steps written" in capsys.readouterr().out


def test_compare_stamps_names_what_moved(tmp_path: Path, capsys: pytest.CaptureFixture) -> None:
    before = write_run(tmp_path / "a", matches=1)
    after = write_run(tmp_path / "b", matches=1, stamp=stamp_json(contentHash="d" * 64, baseSeed=7))

    assert cli.main(["compare-stamps", str(before / "manifest.json"), str(after / "manifest.json")]) == 1
    lines = capsys.readouterr().out.splitlines()
    assert lines[0].startswith("content: ")
    assert lines[1].startswith("seed: ")

    assert cli.main(["compare-stamps", str(before / "manifest.json"), str(before / "manifest.json")]) == 0
    assert "nothing moved" in capsys.readouterr().out


def test_compare_stamps_reads_policies_and_evaluations(tmp_path: Path, capsys: pytest.CaptureFixture) -> None:
    run = write_run(tmp_path / "run", matches=10)
    cli.main(["train-value", str(run), "-o", str(tmp_path / "model")])
    evaluation = tmp_path / "evaluation.json"
    evaluation.write_text(
        json.dumps(evaluation_json(0.5, 0.5, stamp=stamp_json(engineVersion="fedcba654321")))
    )

    assert cli.main(["compare-stamps", str(tmp_path / "model" / "policy.json"), str(evaluation)]) == 1
    assert capsys.readouterr().out.splitlines()[-1].startswith("engine: ")


def test_an_error_is_printed_and_the_exit_code_says_so(tmp_path: Path, capsys: pytest.CaptureFixture) -> None:
    code = cli.main(["train-clone", str(tmp_path / "missing"), "-o", str(tmp_path / "model")])

    assert code == 1
    assert "error: " in capsys.readouterr().err


def test_the_script_entry_points_prepend_their_command(
    monkeypatch: pytest.MonkeyPatch, tmp_path: Path
) -> None:
    run = write_run(tmp_path / "run", matches=3)
    monkeypatch.setattr("sys.argv", ["export-csv", str(run), "-o", str(tmp_path / "steps.csv")])

    assert cli.export_csv_command() == 0
    assert (tmp_path / "steps.csv").is_file()


def test_evaluate_policy_drives_the_engine_and_updates_the_model(
    tmp_path: Path, fake_engine: list[str], capsys: pytest.CaptureFixture
) -> None:
    run = write_run(tmp_path / "run", matches=10)
    cli.main(["train-value", str(run), "-o", str(tmp_path / "model")])

    code = cli.main(
        [
            "evaluate-policy",
            str(tmp_path / "model"),
            "--opponent",
            "random",
            "--repo",
            str(tmp_path),
            "--engine",
            *fake_engine,
        ]
    )

    assert code == 0
    assert (tmp_path / "model" / "evaluation-vs-random.json").is_file()
    assert "win rate 0.7500" in capsys.readouterr().out
    assert json.loads((tmp_path / "model" / "training.jsonl").read_text())["winRate"] == 0.75
