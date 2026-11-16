"""The billed minutes of scripts/workflow-metrics.py --minutes (docs/engineering/ci-minutes-plan.md, M1).

A job is billed from its start to its end, rounded up to the minute, as GitHub bills a private repository's
hosted runner. The recorded answer is a complete pull-request run of CI that the plan counted at 117 minutes,
so the meter is held to the plan's own reading. The script is outside the package: it is loaded from its path.
"""

from __future__ import annotations

import copy
import importlib.util
import json
import subprocess
import sys
import urllib.error
from collections.abc import Callable
from datetime import date
from types import ModuleType
from typing import Any

import pytest

from conftest import REPO_ROOT

SCRIPT = REPO_ROOT / "scripts" / "workflow-metrics.py"
RECORDED = json.loads((REPO_ROOT / "learning" / "tests" / "fixtures" / "actions-ci-run.json").read_text())
REPO = "owner/name"
TODAY = date(2026, 11, 17)


def load_script() -> ModuleType:
    spec = importlib.util.spec_from_file_location("workflow_metrics", SCRIPT)
    assert spec is not None
    assert spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


@pytest.fixture
def metrics() -> ModuleType:
    return load_script()


def run(run_id: int, *, name: str = "CI", path: str = ".github/workflows/ci.yml", **fields: Any) -> dict:
    base = copy.deepcopy(RECORDED["runs"]["workflow_runs"][0])
    base.update(id=run_id, name=name, path=path, **fields)
    return base


def job(name: str, started: str, completed: str, **fields: Any) -> dict:
    base = {
        "name": name,
        "status": "completed",
        "conclusion": "success",
        "run_attempt": 1,
        "runner_group_name": "GitHub Actions",
        "labels": ["ubuntu-latest"],
        "started_at": started,
        "completed_at": completed,
    }
    base.update(fields)
    return base


class FakeApi:
    """Answers the paths the meter asks for: the runs created on each day, and each run's jobs."""

    def __init__(self, runs_by_day: dict[str, list[dict]], jobs_by_run: dict[int, list[dict]]) -> None:
        self.runs_by_day = runs_by_day
        self.jobs_by_run = jobs_by_run
        self.paths: list[str] = []

    def __call__(self, path: str, token: str) -> dict:
        self.paths.append(path)
        assert token == "token"
        if "/jobs?" in path:
            run_id = int(path.split("/runs/")[1].split("/")[0])
            assert "filter=all" in path, "re-run attempts are billed too"
            jobs = self.jobs_by_run[run_id]
            return {"total_count": len(jobs), "jobs": self.page(jobs, path)}
        day = path.split("created=")[1].split("&")[0]
        runs = self.runs_by_day.get(day, [])
        return {"total_count": len(runs), "workflow_runs": self.page(runs, path)}

    @staticmethod
    def page(items: list[dict], path: str) -> list[dict]:
        page = int(path.split("page=")[-1].split("&")[0]) if "&page=" in path else 1
        return items[(page - 1) * 100 : page * 100]


def report(metrics: ModuleType, api: Callable[[str, str], dict], days: int = 1) -> dict:
    metrics.github = api
    return metrics.minutes_report(REPO, days, "token", TODAY)


def recorded_api() -> FakeApi:
    recorded = RECORDED["runs"]["workflow_runs"][0]
    return FakeApi({"2026-11-16": [recorded]}, {recorded["id"]: RECORDED["jobs"]["jobs"]})


def test_the_recorded_complete_run_bills_the_plans_117_minutes(metrics: ModuleType) -> None:
    result = report(metrics, recorded_api())

    assert result["hosted"] == 117
    assert result["by_workflow"] == {"CI": 117}
    assert result["per_ci_run"] == 117


def test_the_recorded_run_is_split_into_the_plans_job_groups(metrics: ModuleType) -> None:
    result = report(metrics, recorded_api())

    assert result["ci_by_group"] == {
        "journeys": 60,
        "battle-release": 28,
        "real engine": 15,
        "build-and-test": 9,
        "image": 2,
        "changes, gate, main-health": 2,
        "learning": 1,
    }


def test_a_job_is_billed_from_its_start_to_its_end_rounded_up(metrics: ModuleType) -> None:
    assert metrics.billed_minutes(job("x", "2026-11-16T04:00:00Z", "2026-11-16T04:01:00Z")) == 1
    assert metrics.billed_minutes(job("x", "2026-11-16T04:00:00Z", "2026-11-16T04:01:01Z")) == 2
    assert metrics.billed_minutes(job("x", "2026-11-16T04:00:00Z", "2026-11-16T04:00:00Z")) == 0


def test_a_skipped_job_bills_nothing(metrics: ModuleType) -> None:
    skipped = job("deploy", "2026-11-16T04:00:00Z", "2026-11-16T04:00:09Z", conclusion="skipped")

    assert metrics.billed_minutes(skipped) == 0


def test_a_job_that_never_started_bills_nothing(metrics: ModuleType) -> None:
    queued = job("gate", "2026-11-16T04:00:00Z", "", started_at=None, completed_at=None)

    assert metrics.billed_minutes(queued) == 0


def test_a_job_on_a_runner_of_our_own_is_counted_apart_from_the_hosted_minutes(metrics: ModuleType) -> None:
    jobs = [
        job("gate", "2026-11-16T04:00:00Z", "2026-11-16T04:00:30Z"),
        job(
            "browser / journeys (phone)",
            "2026-11-16T04:00:00Z",
            "2026-11-16T04:10:00Z",
            runner_group_name="Default",
            labels=["self-hosted", "linux", "x64"],
        ),
    ]
    api = FakeApi({"2026-11-16": [run(1)]}, {1: jobs})

    result = report(metrics, api)

    assert result["hosted"] == 1
    assert result["self_hosted"] == 10
    assert result["ci_by_group"] == {"changes, gate, main-health": 1}


def test_a_cancelled_jobs_minutes_are_billed_and_also_counted_as_thrown_away(metrics: ModuleType) -> None:
    runs = [run(1), run(2, conclusion="cancelled")]
    jobs = {
        1: [job("gate", "2026-11-16T04:00:00Z", "2026-11-16T04:00:30Z")],
        2: [
            job(
                "browser / battle-release",
                "2026-11-16T04:00:00Z",
                "2026-11-16T04:07:30Z",
                conclusion="cancelled",
            )
        ],
    }

    result = report(metrics, FakeApi({"2026-11-16": runs}, jobs))

    assert result["hosted"] == 9
    assert result["cancelled_runs"] == 1
    assert result["cancelled"] == 8


def test_every_attempt_of_a_rerun_is_billed_and_a_cancelled_first_attempt_is_counted(
    metrics: ModuleType,
) -> None:
    jobs = [
        job("gate", "2026-11-16T04:00:00Z", "2026-11-16T04:03:00Z", conclusion="cancelled", run_attempt=1),
        job("gate", "2026-11-16T05:00:00Z", "2026-11-16T05:02:00Z", run_attempt=2),
    ]
    api = FakeApi({"2026-11-16": [run(1, run_attempt=2)]}, {1: jobs})

    result = report(metrics, api)

    assert result["hosted"] == 5
    assert result["cancelled"] == 3
    assert result["cancelled_runs"] == 1


def test_a_runner_of_our_own_named_by_a_custom_label_alone_is_not_counted_as_hosted(
    metrics: ModuleType,
) -> None:
    own = job(
        "browser / journeys (phone)",
        "2026-11-16T04:00:00Z",
        "2026-11-16T04:04:00Z",
        runner_group_name="Default",
        labels=["downfall-heavy"],
    )
    api = FakeApi({"2026-11-16": [run(1)]}, {1: [own]})

    result = report(metrics, api)

    assert result["hosted"] == 0
    assert result["self_hosted"] == 4


def test_a_run_listed_twice_while_the_list_moves_is_counted_once(metrics: ModuleType) -> None:
    api = FakeApi(
        {"2026-11-16": [run(1), run(1)]}, {1: [job("gate", "2026-11-16T04:00:00Z", "2026-11-16T04:00:30Z")]}
    )

    result = report(metrics, api)

    assert result["runs"] == 1
    assert result["hosted"] == 1


def test_a_skipped_run_is_not_asked_for_its_jobs(metrics: ModuleType) -> None:
    api = FakeApi({"2026-11-16": [run(1, conclusion="skipped")]}, {})

    result = report(metrics, api)

    assert result["hosted"] == 0
    assert not any("/jobs?" in path for path in api.paths)


def test_the_api_refusing_halfway_stops_with_the_day_it_reached(metrics: ModuleType) -> None:
    def refusing(path: str, token: str) -> dict:
        raise urllib.error.HTTPError(path, 403, "rate limit exceeded", None, None)  # type: ignore[arg-type]

    with pytest.raises(SystemExit, match=r"403.*2026-11-16"):
        report(metrics, refusing)


def test_a_workflow_other_than_ci_counts_in_its_own_line_and_not_in_cis_groups(metrics: ModuleType) -> None:
    runs = [run(1), run(2, name="Nightly browser matrix", path=".github/workflows/nightly.yml")]
    jobs = {
        1: [job("gate", "2026-11-16T04:00:00Z", "2026-11-16T04:00:30Z")],
        2: [job("browser / journeys (phone)", "2026-11-16T04:00:00Z", "2026-11-16T04:12:00Z")],
    }

    result = report(metrics, FakeApi({"2026-11-16": runs}, jobs))

    assert result["by_workflow"] == {"Nightly browser matrix": 12, "CI": 1}
    assert result["ci_by_group"] == {"changes, gate, main-health": 1}
    assert result["per_ci_run"] == 1


def test_each_release_profiles_job_counts_in_the_battle_release_group(metrics: ModuleType) -> None:
    # ADR 0190: the release job is one job per profile, named after it; CI calls it as "browser".
    workflow = (REPO_ROOT / ".github" / "workflows" / "studio-browser.yml").read_text()
    job = workflow[workflow.index("\n  battle-release:\n") :]
    name = next(line.split("name: ", 1)[1] for line in job.splitlines() if line.startswith("    name: "))

    for profile in ("release-desktop", "release-android", "release-ios"):
        shown = "browser / " + name.replace("${{ matrix.profile }}", profile)
        assert metrics.job_group(shown) == "battle-release", shown


def test_a_job_no_group_names_is_counted_as_other(metrics: ModuleType) -> None:
    api = FakeApi(
        {"2026-11-16": [run(1)]}, {1: [job("a new job", "2026-11-16T04:00:00Z", "2026-11-16T04:02:00Z")]}
    )

    assert report(metrics, api)["ci_by_group"] == {"other": 2}


def test_a_run_still_playing_is_left_out(metrics: ModuleType) -> None:
    api = FakeApi({"2026-11-16": [run(1, status="in_progress", conclusion=None)]}, {})

    result = report(metrics, api)

    assert result["runs"] == 0
    assert result["hosted"] == 0
    assert not any("/jobs?" in path for path in api.paths)


def test_the_window_is_whole_days_ending_yesterday_read_one_at_a_time(
    metrics: ModuleType,
) -> None:
    api = FakeApi({}, {})

    result = report(metrics, api, days=3)

    asked = [path.split("created=")[1].split("&")[0] for path in api.paths]
    assert asked == ["2026-11-14", "2026-11-15", "2026-11-16"]
    assert result["since"] == "2026-11-14"
    assert result["until"] == "2026-11-16"


def test_a_day_of_more_than_a_hundred_runs_reads_the_next_page(metrics: ModuleType) -> None:
    runs = [run(i) for i in range(1, 151)]
    jobs = {i: [job("gate", "2026-11-16T04:00:00Z", "2026-11-16T04:00:30Z")] for i in range(1, 151)}

    result = report(metrics, FakeApi({"2026-11-16": runs}, jobs))

    assert result["runs"] == 150
    assert result["hosted"] == 150


def test_the_month_is_the_window_scaled_to_thirty_days(metrics: ModuleType) -> None:
    api = FakeApi(
        {"2026-11-16": [run(1)]}, {1: [job("gate", "2026-11-16T04:00:00Z", "2026-11-16T04:06:30Z")]}
    )

    result = report(metrics, api, days=3)

    assert result["hosted"] == 7
    assert result["month_estimate"] == 70


def test_minutes_without_a_token_stops_before_asking_anything() -> None:
    result = subprocess.run(
        [sys.executable, str(SCRIPT), "--minutes"],
        capture_output=True,
        text=True,
        check=False,
        cwd=REPO_ROOT,
        env={"PATH": ""},
    )

    assert result.returncode != 0
    assert "--minutes needs a token in GITHUB_TOKEN" in result.stderr


def test_minutes_over_no_day_is_refused() -> None:
    result = subprocess.run(
        [sys.executable, str(SCRIPT), "--minutes", "--days", "0"],
        capture_output=True,
        text=True,
        check=False,
        cwd=REPO_ROOT,
        env={"PATH": "", "GITHUB_TOKEN": "token"},
    )

    assert result.returncode != 0
    assert "--days must be at least 1" in result.stderr


def test_a_hosted_job_with_an_empty_runner_group_is_still_hosted(metrics: ModuleType) -> None:
    pages = job(
        "Build the site and deploy it", "2026-11-16T04:00:00Z", "2026-11-16T04:00:40Z", runner_group_name=""
    )
    api = FakeApi(
        {"2026-11-16": [run(1, name="Publish the studio", path=".github/workflows/pages.yml")]}, {1: [pages]}
    )

    result = report(metrics, api)

    assert result["hosted"] == 1
    assert result["self_hosted"] == 0
