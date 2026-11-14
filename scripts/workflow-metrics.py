#!/usr/bin/env python3
"""The numbers of docs/engineering/ai-workflow-plan.md, measured again.

From git alone: pull requests per week, their size, the files most of them touch, the size of AGENTS.md and
how often it changes, and ADR numbers used twice. With --github and a token in GITHUB_TOKEN (a workflow's own
token is enough): how long a pull request stays open, how many were merged before every check finished, and
how CI on main fares. Standard library only, so it runs in a session, a workflow or a laptop alike.

    python3 scripts/workflow-metrics.py                 # the last 300 pull requests on origin/main
    python3 scripts/workflow-metrics.py --last 100 --github   # the repository is origin's, read from git
"""

from __future__ import annotations

import argparse
import json
import os
import re
import statistics
import subprocess
import urllib.parse
import urllib.request
from collections import Counter
from datetime import datetime
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


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--last", type=int, default=300)
    parser.add_argument("--github", action="store_true", help="also read GitHub (needs GITHUB_TOKEN)")
    args = parser.parse_args()

    prs = merged_pull_requests(args.last)
    report = {"git": git_report(prs)}
    if args.github:
        token = os.environ.get("GITHUB_TOKEN", "")
        if not token:
            raise SystemExit("--github needs a token in GITHUB_TOKEN")
        report["github"] = github_report(origin_repository(), prs, token)
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
