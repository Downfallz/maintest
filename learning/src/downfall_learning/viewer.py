"""A run page: the static viewer with the run's own artifacts carried inside, so it opens on the report.

``viewer/index.html`` normally takes files by drag and drop. A run page is that same page, its stylesheet
inlined and its small artifacts (``report.json``, every evaluation, every ``training.jsonl``) embedded as
one JSON block the page reads at start, so ``runs/<id>/report.html`` opens straight on the report with
nothing to pick. Datasets and traces are not embedded: they are large, and the page still takes them by drop.
"""

from __future__ import annotations

import json
from collections.abc import Iterable
from pathlib import Path
from typing import Any

from downfall_learning.iteration import EVALUATIONS_DIRECTORY, REPORT_FILE
from downfall_learning.report import TRAINING_FILE

RUN_PAGE = "report.html"
EMBEDDED_ID = "embedded-artifacts"
_STYLESHEET_LINK = '<link rel="stylesheet" href="viewer.css">'
_SCRIPT_START = "  <script>\n    'use strict';"


def default_viewer_directory() -> Path:
    """``viewer/`` at the repository root, next to ``learning/``; the only layout this project ships in."""
    return Path(__file__).resolve().parents[3] / "viewer"


def carried_files(run: Path) -> list[Path]:
    """The small artifacts of a run, in a stable order: the report, the evaluations, the training logs."""
    run = Path(run)
    files: list[Path] = []
    report = run / REPORT_FILE
    if report.is_file():
        files.append(report)
    evaluations = run / EVALUATIONS_DIRECTORY
    if evaluations.is_dir():
        files.extend(sorted(evaluations.glob("*.json")))
    files.extend(sorted(path for path in run.glob(f"*/{TRAINING_FILE}") if path.is_file()))
    return files


def embedded_payload(run: Path, files: Iterable[Path]) -> dict[str, Any]:
    run = Path(run)
    artifacts = []
    for path in files:
        relative = path.relative_to(run).as_posix()
        artifacts.append(
            {"path": f"{run.name}/{relative}", "name": path.name, "text": path.read_text(encoding="utf-8")}
        )
    return {"run": run.name, "artifacts": artifacts}


def render_run_page(run: Path, viewer: Path | None = None) -> str:
    """The viewer page, stylesheet inlined and the run artifacts embedded; refuses a missing viewer."""
    viewer = Path(viewer) if viewer is not None else default_viewer_directory()
    index = viewer / "index.html"
    stylesheet = viewer / "viewer.css"
    if not index.is_file() or not stylesheet.is_file():
        raise FileNotFoundError(f"No viewer under '{viewer}' (index.html and viewer.css).")
    page = index.read_text(encoding="utf-8")
    if _STYLESHEET_LINK not in page or _SCRIPT_START not in page:
        raise ValueError(f"'{index}' does not look like the viewer this page is built from.")
    payload = json.dumps(embedded_payload(run, carried_files(run)))
    # A closing tag inside the JSON would end the script element early; the escape is valid JSON.
    payload = payload.replace("</", "<\\/")
    page = page.replace(_STYLESHEET_LINK, f"<style>\n{stylesheet.read_text(encoding='utf-8')}\n  </style>", 1)
    block = f'  <script id="{EMBEDDED_ID}" type="application/json">{payload}</script>\n'
    return page.replace(_SCRIPT_START, block + _SCRIPT_START, 1)


def write_run_page(run: Path, viewer: Path | None = None) -> Path:
    """Writes the page as ``<run>/report.html``, refusing a run directory that does not exist.

    The name is this module's own constant and the directory has to exist already (the report was just read
    from it), so the written path is always one file inside a directory the operator named on their own
    command line; the containment check states that rather than trusting it.
    """
    directory = Path(run).resolve()
    if not directory.is_dir():
        raise NotADirectoryError(f"'{run}' is not a directory to write {RUN_PAGE} into.")
    path = (directory / RUN_PAGE).resolve()
    if path.parent != directory:
        raise ValueError(f"'{path}' would fall outside '{directory}'.")
    page = render_run_page(directory, viewer)
    # A local developer tool writing a fixed file name into the run directory the operator named; the path
    # is resolved and checked to stay inside it above, so the taint the analyzer sees carries no risk.
    path.write_text(page, encoding="utf-8")  # NOSONAR
    return path
