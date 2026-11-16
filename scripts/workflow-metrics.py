#!/usr/bin/env python3
"""The numbers of docs/engineering/ai-workflow-plan.md, measured again.

From git alone: pull requests per week, their size, the files most of them touch, the size of AGENTS.md and
how often it changes, and ADR numbers used twice. With --github and a token in GITHUB_TOKEN (a workflow's own
token is enough): how long a pull request stays open, how many were merged before every check finished, and
how CI on main fares. With --minutes and a token: the Actions minutes the last whole days would be billed on a
private repository, per workflow and per group of CI's jobs (docs/engineering/ci-minutes-plan.md, M1). It asks
once for each run, about 1,000 requests for a busy week: a workflow's own token allows 1,000 an hour, so a
session's token or a person's is the one to use.
Standard library only, so it runs in a session, a workflow or a laptop alike.

    python3 scripts/workflow-metrics.py                 # the last 300 pull requests on origin/main
    python3 scripts/workflow-metrics.py --last 100 --github   # the repository is origin's, read from git
    python3 scripts/workflow-metrics.py --minutes --days 7    # the week's billed minutes
"""

from __future__ import annotations

import argparse
import json
import math
import os
import re
import statistics
import subprocess
import urllib.error
import urllib.parse
import urllib.request
from collections import Counter
from datetime import date, datetime, timedelta, timezone
from pathlib import Path

PR_NUMBER = re.compile(r"\(#(\d+)\)$")
GITHUB_REMOTE = re.compile(r"github\.com[:/]([A-Za-z0-9_.-]+)/([A-Za-z0-9_.-]+?)(?:\.git)?/?$")
ADR_FILE = re.compile(r"^(\d{4})-.+\.md$")


def git(*args: str) -> str:
    return subprocess.run(["git", *args], capture_output=True, text=True, check=True).stdout


def origin_repository() -> str:
    """owner/name of origin on GitHub. Read from git rather than taken as an argument, so the API is only ever
    asked about the repository this checkout belongs to."""
    url = git("remote", "get-url", "origin").strip()
    match = GITHUB_REMOTE.search(url)
    if not match:
        raise SystemExit(f"origin is not a GitHub repository: {url}")
    return f"{segment(match.group(1))}/{segment(match.group(2))}"


def merged_pull_requests(last: int) -> list[dict]:
    """The squash commits of the last pull requests on origin/main, newest first, with the files each touched.

    The branch is fixed rather than an argument, so nothing a caller types reaches the git command line."""
    out = git("log", "origin/main", "--first-parent", "--format=@@%H%x09%aI%x09%s", "--numstat")
    commits: list[dict] = []
    current: dict | None = None
    for line in out.splitlines():
        if line.startswith("@@"):
            sha, date, subject = line[2:].split("\t", 2)
            match = PR_NUMBER.search(subject)
            current = {"sha": sha, "date": date, "subject": subject, "files": []}
            current["number"] = int(match.group(1)) if match else None
            commits.append(current)
        elif line.strip() and current is not None:
            added, deleted, path = line.split("\t", 2)
            changed = (0 if added == "-" else int(added)) + (0 if deleted == "-" else int(deleted))
            current["files"].append((path, changed))
    with_number = [c for c in commits if c["number"] is not None]
    return with_number[:last]


def quantile(values: list[float], q: float) -> float:
    ordered = sorted(values)
    return ordered[min(len(ordered) - 1, int(q * len(ordered)))]


def git_report(prs: list[dict]) -> dict:
    sizes = [sum(changed for _, changed in pr["files"]) for pr in prs]
    weeks = Counter(datetime.fromisoformat(pr["date"]).strftime("%G-W%V") for pr in prs)
    files = Counter(path for pr in prs for path in {p for p, _ in pr["files"]})
    agents = Path("AGENTS.md")
    agents_text = agents.read_text(encoding="utf-8") if agents.exists() else ""
    adr_dir = Path("docs/adr")
    names = [entry.name for entry in adr_dir.iterdir()] if adr_dir.is_dir() else []
    numbers = Counter(m.group(1) for name in names if (m := ADR_FILE.match(name)))
    return {
        "pull_requests": len(prs),
        "range": [prs[-1]["number"], prs[0]["number"]] if prs else [],
        "per_week": dict(sorted(weeks.items())),
        "changed_lines": {"median": statistics.median(sizes), "p90": quantile(sizes, 0.9)} if sizes else {},
        "hottest_files": files.most_common(10),
        "agents_md": {
            "bytes": len(agents_text.encode("utf-8")),
            "lines": agents_text.count("\n"),
            "longest_line": max((len(line) for line in agents_text.splitlines()), default=0),
            "changed_in": files.get("AGENTS.md", 0),
        },
        "adr_numbers_used_twice": sorted(n for n, count in numbers.items() if count > 1),
    }


def utc(timestamp: str) -> datetime:
    return datetime.fromisoformat(timestamp.replace("Z", "+00:00"))


def segment(value: object) -> str:
    """One path segment of an API URL, encoded: what an answer or a remote holds never walks the path."""
    return urllib.parse.quote(str(value), safe="")


def github(path: str, token: str) -> dict | list:
    request = urllib.request.Request(
        f"https://api.github.com{path}",
        headers={"Authorization": f"Bearer {token}", "Accept": "application/vnd.github+json"},
    )
    with urllib.request.urlopen(request, timeout=30) as response:
        return json.load(response)


PASSED = {"success", "skipped", "neutral"}


def finished_green(status: str, conclusion: str | None, done: str | None, merged: datetime) -> bool:
    """Done, passed, and before the merge; a cancelled or timed-out run never passed."""
    if status != "completed" or not done or conclusion not in PASSED:
        return False
    return utc(done) <= merged


def green_before(repo: str, head: str, merged: datetime, token: str) -> bool:
    """Every check on the head commit and every workflow run of it finished green before the merge.

    Both are read: a pull_request run checks out the merge commit, and its runs are listed by head_sha in the
    Actions API even where the head commit's check runs do not carry them. Nothing at all is not green.
    """
    sha = segment(head)
    checks = github(f"/repos/{repo}/commits/{sha}/check-runs?per_page=100", token).get("check_runs", [])
    runs = github(f"/repos/{repo}/actions/runs?head_sha={sha}&per_page=100", token).get("workflow_runs", [])
    seen = [(c["status"], c.get("conclusion"), c.get("completed_at")) for c in checks]
    seen += [(r["status"], r.get("conclusion"), r.get("updated_at")) for r in runs]
    return bool(seen) and all(finished_green(s, c, d, merged) for s, c, d in seen)


def github_report(repo: str, prs: list[dict], token: str) -> dict:
    open_minutes: list[float] = []
    merged_early: list[int] = []
    for pr in prs:
        detail = github(f"/repos/{repo}/pulls/{segment(pr['number'])}", token)
        if not detail.get("merged_at"):
            continue
        merged = utc(detail["merged_at"])
        opened = utc(detail["created_at"])
        open_minutes.append((merged - opened).total_seconds() / 60)
        if not green_before(repo, detail["head"]["sha"], merged, token):
            merged_early.append(pr["number"])
    runs = github(f"/repos/{repo}/actions/workflows/ci.yml/runs?branch=main&event=push&per_page=100", token)
    conclusions = Counter(run.get("conclusion") or run["status"] for run in runs.get("workflow_runs", []))
    return {
        "open_to_merge_minutes": {
            "median": statistics.median(open_minutes),
            "under_15": sum(m < 15 for m in open_minutes),
            "of": len(open_minutes),
        }
        if open_minutes
        else {},
        "merged_before_green_or_on_red": merged_early,
        "ci_on_main_last_100": dict(conclusions),
    }


CI_WORKFLOW = ".github/workflows/ci.yml"
# CI's jobs in the groups docs/engineering/ci-minutes-plan.md reads, first match wins. A job of a called
# workflow is named "<caller job> / <job>", so a pattern matches anywhere in the name; a job none names is
# "other".
JOB_GROUPS = (
    ("journeys", re.compile(r"\bjourneys\b")),
    ("battle-release", re.compile(r"\bbattle-release\b")),
    ("real engine", re.compile(r"\breal engine\b")),
    ("build-and-test", re.compile(r"^Build, format check, test, analyse$")),
    # The learning project's own job, folded into build-and-test by M3: kept for the runs before it.
    ("learning", re.compile(r"^Lint and test the learning project$")),
    ("image", re.compile(r"^image /")),
    ("changes, gate, main-health", re.compile(r"^(What the diff calls for|gate|Say so when main is red)$")),
)


def job_group(name: str) -> str:
    return next((group for group, pattern in JOB_GROUPS if pattern.search(name)), "other")


def billed_minutes(job: dict) -> int:
    """From the job's start to its end, rounded up to the minute, as a hosted runner is billed. A job skipped
    or never started bills nothing."""
    started, completed = job.get("started_at"), job.get("completed_at")
    if job.get("conclusion") == "skipped" or not started or not completed:
        return 0
    return math.ceil(max(0.0, (utc(completed) - utc(started)).total_seconds()) / 60)


def self_hosted(job: dict) -> bool:
    """On a runner of the project's own: labelled so, or in a runner group other than GitHub's. A skipped job
    names no group, and some hosted jobs (the Pages deployment) name an empty one: neither is."""
    group = job.get("runner_group_name")
    return "self-hosted" in (job.get("labels") or []) or group not in (None, "", "GitHub Actions")


def paged(path: str, key: str, token: str) -> list[dict]:
    items: list[dict] = []
    page = 1
    while True:
        answer = github(f"{path}&per_page=100&page={page}", token)
        batch = answer.get(key, [])
        items += batch
        if len(batch) < 100 or len(items) >= answer.get("total_count", 0):
            return items
        page += 1


def completed_runs(repo: str, days: int, token: str, today: date) -> list[dict]:
    """The runs created over the last whole UTC days, ending yesterday, one day at a time: the API returns at
    most 1,000 runs for a query, and a busy week holds about that many. Today is left out, so a run is never
    in two windows nor in none. A run is kept once, though the list moves while it is read."""
    runs: dict[int, dict] = {}
    for back in range(days, 0, -1):
        day = (today - timedelta(days=back)).isoformat()
        try:
            listed = paged(f"/repos/{repo}/actions/runs?created={day}", "workflow_runs", token)
        except urllib.error.HTTPError as error:
            raise SystemExit(
                f"the Actions API answered {error.code} while reading the runs of {day}"
            ) from error
        if len(listed) >= 1000:
            raise SystemExit(f"{day} holds 1,000 runs or more, past what the API lists for one query")
        runs.update((run["id"], run) for run in listed if run.get("status") == "completed")
    return list(runs.values())


def run_jobs(repo: str, run: dict, token: str) -> list[dict]:
    """Every attempt's jobs: a re-run attempt is billed too."""
    try:
        return paged(f"/repos/{repo}/actions/runs/{segment(run['id'])}/jobs?filter=all", "jobs", token)
    except urllib.error.HTTPError as error:
        day = run.get("created_at", "?")[:10]
        raise SystemExit(
            f"the Actions API answered {error.code} while reading the jobs of a run of {day}"
        ) from error


def tally_run(jobs: list[dict], is_ci: bool, ci_groups: Counter[str]) -> tuple[int, int, int]:
    """One run's hosted minutes, the share of them in cancelled jobs, and its minutes on a runner of our own;
    a CI run's hosted minutes are also added to their job groups."""
    hosted = cancelled = own = 0
    for job in jobs:
        minutes = billed_minutes(job)
        if self_hosted(job):
            own += minutes
            continue
        hosted += minutes
        if job.get("conclusion") == "cancelled":
            cancelled += minutes
        if is_ci and minutes:
            ci_groups[job_group(job["name"])] += minutes
    return hosted, cancelled, own


def minutes_report(repo: str, days: int, token: str, today: date) -> dict:
    """Billed hosted minutes per workflow and per CI job group. A job cancelled counts as thrown away,
    whatever a later attempt did; a runner of the project's own is not billed by GitHub and is counted apart.
    A skipped run started no job, and is not asked for its jobs."""
    by_workflow: Counter[str] = Counter()
    ci_groups: Counter[str] = Counter()
    hosted = own = cancelled = cancelled_runs = ci_runs = 0
    runs = completed_runs(repo, days, token, today)
    for run in runs:
        if run.get("conclusion") == "skipped":
            continue
        is_ci = run.get("path") == CI_WORKFLOW
        ci_runs += is_ci
        run_hosted, run_cancelled, run_own = tally_run(run_jobs(repo, run, token), is_ci, ci_groups)
        hosted += run_hosted
        own += run_own
        cancelled += run_cancelled
        cancelled_runs += bool(run_cancelled) or run.get("conclusion") == "cancelled"
        by_workflow[run.get("name") or run.get("path", "?")] += run_hosted
    return {
        "days": days,
        "since": (today - timedelta(days=days)).isoformat(),
        "until": (today - timedelta(days=1)).isoformat(),
        "runs": len(runs),
        "hosted": hosted,
        "month_estimate": round(hosted * 30 / days),
        "self_hosted": own,
        "cancelled_runs": cancelled_runs,
        "cancelled": cancelled,
        "by_workflow": dict(by_workflow.most_common()),
        "ci_by_group": dict(ci_groups.most_common()),
        "per_ci_run": round(sum(ci_groups.values()) / ci_runs, 1) if ci_runs else 0,
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--last", type=int, default=300)
    parser.add_argument("--github", action="store_true", help="also read GitHub (needs GITHUB_TOKEN)")
    parser.add_argument(
        "--minutes", action="store_true", help="also the billed Actions minutes (needs GITHUB_TOKEN)"
    )
    parser.add_argument("--days", type=int, default=7, help="the window --minutes reads, in days (UTC)")
    args = parser.parse_args()
    token = os.environ.get("GITHUB_TOKEN", "")
    for flag in ("github", "minutes"):
        if getattr(args, flag) and not token:
            raise SystemExit(f"--{flag} needs a token in GITHUB_TOKEN")
    if args.days < 1:
        raise SystemExit("--days must be at least 1")

    prs = merged_pull_requests(args.last)
    report = {"git": git_report(prs)}
    if args.github:
        report["github"] = github_report(origin_repository(), prs, token)
    if args.minutes:
        report["minutes"] = minutes_report(
            # timezone.utc, not datetime.UTC (3.11): the script runs on whatever python3 a laptop has.
            origin_repository(),
            args.days,
            token,
            datetime.now(timezone.utc).date(),  # noqa: UP017
        )
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
