import json
from pathlib import Path

import pytest

from conftest import evaluation_json, stamp_json
from downfall_learning import cli
from downfall_learning.artifacts import ArtifactError
from downfall_learning.iteration import (
    build_report,
    compare_reports,
    format_report,
    load_report,
    write_report,
)


def write_evaluation(run: Path, name: str, score: float, stamp: dict, player1_wins: int = 3) -> Path:
    evaluation = evaluation_json(score, score, matches=6, stamp=stamp)
    evaluation["agentA"]["agent"] = stamp["player1Agent"]
    evaluation["agentB"]["agent"] = stamp["player2Agent"]
    evaluation["draws"] = 1
    evaluation["averageRounds"] = 9.5
    evaluation["pairs"] = [
        {
            "seed": index,
            "aFirst": {"outcome": {"winner": "Player1" if index < player1_wins else "Player2"}},
            "bFirst": {"outcome": {"winner": "Player2"}},
        }
        for index in range(3)
    ]
    directory = run / "evaluations"
    directory.mkdir(parents=True, exist_ok=True)
    path = directory / f"{name}.json"
    path.write_text(json.dumps(evaluation), encoding="utf-8")
    return path


def a_run(tmp_path: Path, name: str, engine: str = "abcdef123456", greedy_score: float = 0.9) -> Path:
    run = tmp_path / name
    write_evaluation(
        run,
        "greedy-vs-random",
        greedy_score,
        stamp_json(engineVersion=engine, player1Agent="Greedy", player2Agent="Random"),
    )
    write_evaluation(
        run,
        "greedy-vs-greedy",
        0.5,
        stamp_json(engineVersion=engine, player1Agent="Greedy", player2Agent="Greedy"),
        player1_wins=2,
    )
    return run


def test_the_report_summarizes_every_evaluation_of_the_run(tmp_path: Path) -> None:
    run = a_run(tmp_path, "one")

    report = build_report(run)
    path = write_report(report, run)

    assert report.run == "one"
    assert [summary.name for summary in report.evaluations] == ["greedy-vs-greedy", "greedy-vs-random"]
    mirrored = report.find("greedy-vs-greedy")
    assert mirrored is not None
    assert mirrored.metrics["player1WinShare"] == pytest.approx(2 / 6)
    assert mirrored.metrics["drawRate"] == pytest.approx(1 / 6)
    assert mirrored.metrics["averageRounds"] == 9.5
    assert mirrored.metrics["winRateA"] == 0.5
    written = json.loads(path.read_text())
    assert written["evaluations"]["greedy-vs-random"]["agentB"] == "Random"
    assert written["stamp"]["engineVersion"] == "abcdef123456"
    assert load_report(run).find("greedy-vs-random") is not None


def test_evaluations_of_two_engines_never_share_a_report(tmp_path: Path) -> None:
    run = a_run(tmp_path, "mixed")
    write_evaluation(run, "other", 0.5, stamp_json(engineVersion="fedcba654321"))

    with pytest.raises(ArtifactError, match="engine"):
        build_report(run)


def test_a_run_without_evaluations_is_refused(tmp_path: Path) -> None:
    with pytest.raises(ArtifactError, match="no evaluation"):
        build_report(tmp_path / "empty")


def test_the_comparison_names_the_axis_that_moved_and_the_deltas(tmp_path: Path) -> None:
    current = build_report(a_run(tmp_path, "after", engine="fedcba654321", greedy_score=0.8))
    previous = build_report(a_run(tmp_path, "before"))
    write_evaluation(
        tmp_path / "before",
        "value-vs-greedy",
        0.4,
        stamp_json(player1Agent="Policy:p.json@1", player2Agent="Greedy"),
    )
    previous = build_report(tmp_path / "before")

    comparison = compare_reports(current, previous)

    assert comparison.previous == "before"
    assert comparison.axes == ("engine: abcdef123456 versus fedcba654321",)
    assert comparison.deltas["greedy-vs-random"]["winRateA"] == pytest.approx(-0.1)
    assert comparison.deltas["greedy-vs-greedy"]["winRateA"] == 0.0
    assert comparison.only_in_previous == ("value-vs-greedy",)
    assert comparison.only_in_current == ()
    assert comparison.to_json()["moved"] == ["engine: abcdef123456 versus fedcba654321"]
    text = format_report(current, comparison)
    assert "Against before: engine" in text
    assert "greedy-vs-random: winRateA -0.100" in text
    assert "only before: value-vs-greedy" in text


def test_the_same_stamp_reads_as_nothing_moved(tmp_path: Path) -> None:
    report = build_report(a_run(tmp_path, "same"))

    text = format_report(report, compare_reports(report, report))

    assert "nothing in the stamp" in text
    assert "Win A" in text
    assert "greedy-vs-random" in text


def test_the_report_command_writes_and_prints(tmp_path: Path, capsys: pytest.CaptureFixture) -> None:
    run = a_run(tmp_path, "cli")
    previous = a_run(tmp_path, "previous", greedy_score=0.7)

    assert cli.main(["report", str(run), "--against", str(previous)]) == 0

    assert (run / "report.json").is_file()
    out = capsys.readouterr().out
    assert "Run cli" in out
    assert "winRateA +0.200" in out
    assert cli.main(["report", str(tmp_path / "missing")]) == 1
