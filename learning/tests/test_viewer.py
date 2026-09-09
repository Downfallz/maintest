import json
from pathlib import Path

import pytest

from conftest import REPO_ROOT, evaluation_json, stamp_json, write_run
from downfall_learning import cli
from downfall_learning.artifacts import build_dataset, load_run
from downfall_learning.report import TrainingLog
from downfall_learning.train_value import train_value
from downfall_learning.viewer import (
    EMBEDDED_ID,
    RUN_PAGE,
    carried_files,
    default_viewer_directory,
    render_run_page,
    write_run_page,
)


def a_run_with_everything(tmp_path: Path) -> Path:
    run = tmp_path / "premier"
    evaluations = run / "evaluations"
    evaluations.mkdir(parents=True)
    (evaluations / "greedy-vs-random.json").write_text(
        json.dumps(evaluation_json(0.9, 0.9, stamp=stamp_json(player1Agent="Greedy", player2Agent="Random")))
    )
    dataset = build_dataset([load_run(write_run(run / "dataset", matches=6))], kinds=["Intent"])
    policy = train_value(dataset, log=TrainingLog(dataset.stamp, run / "value" / "training.jsonl"))
    policy.save(run / "value" / "policy.json")
    (run / "value" / "closing.json").write_text('{"note": "</script> inside a value"}')
    assert cli.main(["report", str(run), "--no-html"]) == 0
    return run


def test_the_carried_files_are_the_report_the_evaluations_and_the_training_logs(tmp_path: Path) -> None:
    run = a_run_with_everything(tmp_path)

    names = [path.relative_to(run).as_posix() for path in carried_files(run)]

    assert names == ["report.json", "evaluations/greedy-vs-random.json", "value/training.jsonl"]


def test_the_run_page_is_the_viewer_with_the_stylesheet_inlined_and_the_artifacts_embedded(
    tmp_path: Path,
) -> None:
    run = a_run_with_everything(tmp_path)

    page = render_run_page(run)

    assert '<link rel="stylesheet"' not in page
    assert "<style>" in page
    assert f'id="{EMBEDDED_ID}"' in page
    start = page.index(f'id="{EMBEDDED_ID}"')
    block = page[start : page.index("</script>", start)]
    payload = json.loads(block[block.index(">") + 1 :].replace("<\\/", "</"))
    assert payload["run"] == "premier"
    assert [artifact["path"] for artifact in payload["artifacts"]] == [
        "premier/report.json",
        "premier/evaluations/greedy-vs-random.json",
        "premier/value/training.jsonl",
    ]
    assert json.loads(payload["artifacts"][0]["text"])["run"] == "premier"
    assert page.index(f'id="{EMBEDDED_ID}"') < page.index("'use strict'")


def test_a_closing_tag_inside_an_artifact_cannot_end_the_script_early(tmp_path: Path) -> None:
    run = a_run_with_everything(tmp_path)
    (run / "evaluations" / "odd.json").write_text(
        json.dumps({**evaluation_json(0.5, 0.5), "note": "</script>"})
    )

    page = render_run_page(run)

    start = page.index(f'id="{EMBEDDED_ID}"')
    block = page[start : page.index("</script>", start)]
    assert "<\\/script>" in block
    assert "</script>" not in block


def test_the_page_is_written_next_to_the_report(tmp_path: Path) -> None:
    run = a_run_with_everything(tmp_path)

    path = write_run_page(run)

    assert path == run / RUN_PAGE
    assert path.stat().st_size > 10_000


def test_the_page_is_refused_when_the_run_directory_is_not_one(tmp_path: Path) -> None:
    file_not_directory = tmp_path / "premier.txt"
    file_not_directory.write_text("not a run")

    with pytest.raises(NotADirectoryError, match="not a directory"):
        write_run_page(file_not_directory)
    with pytest.raises(NotADirectoryError, match="not a directory"):
        write_run_page(tmp_path / "nowhere")


def test_a_missing_or_foreign_viewer_is_refused(tmp_path: Path) -> None:
    run = a_run_with_everything(tmp_path)
    foreign = tmp_path / "viewer"
    foreign.mkdir()
    (foreign / "index.html").write_text("<html></html>")
    (foreign / "viewer.css").write_text("")

    with pytest.raises(FileNotFoundError, match="No viewer"):
        render_run_page(run, tmp_path / "nowhere")
    with pytest.raises(ValueError, match="does not look like the viewer"):
        render_run_page(run, foreign)


def test_the_default_viewer_is_the_repository_one() -> None:
    assert default_viewer_directory() == REPO_ROOT / "viewer"
    assert (default_viewer_directory() / "index.html").is_file()


def test_the_report_command_writes_the_page_unless_told_not_to(
    tmp_path: Path, capsys: pytest.CaptureFixture
) -> None:
    run = a_run_with_everything(tmp_path)
    assert not (run / RUN_PAGE).exists()

    assert cli.main(["report", str(run)]) == 0

    assert (run / RUN_PAGE).is_file()
    assert "Open '" in capsys.readouterr().out
