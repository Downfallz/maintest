"""The command line: ``uv run --project learning <command> ...`` (docs/learning/training.md)."""

from __future__ import annotations

import argparse
import sys
from collections.abc import Sequence
from dataclasses import replace
from pathlib import Path

from downfall_learning.artifacts import Dataset, build_dataset, load_evaluation, load_manifest, load_runs
from downfall_learning.evaluate_policy import evaluate_policy
from downfall_learning.export import DEFAULT_WEIGHTS, export_wide_csv, read_weights
from downfall_learning.iteration import (
    build_report,
    compare_reports,
    format_report,
    load_report,
    write_report,
)
from downfall_learning.knobs import KNOBS_FILE, KnobsError, findings, load_content, load_knobs, validate
from downfall_learning.policy import POLICY_FILE, Policy
from downfall_learning.report import TRAINING_FILE, TrainingLog
from downfall_learning.search_weights import (
    CliEvaluator,
    EngineCommand,
    SearchOptions,
    format_search,
    search_weights,
    win_rate_lines,
)
from downfall_learning.stamps import RunStamp
from downfall_learning.train_clone import CloneOptions, train_clone
from downfall_learning.train_value import ValueOptions, train_value
from downfall_learning.tune_content import (
    PAIR_DEPTH,
    ContentEngine,
    EngineContentEvaluator,
    TuneOptions,
    format_result,
    tune_content,
)
from downfall_learning.viewer import RUN_PAGE, write_run_page

RUNS_HELP = "one or more run directories recorded by 'simulate --record'"
REPO_HELP = "the engine repository root (default: cwd)"
OUTPUT_HELP = "the directory the model is written to"


def _add_dataset_arguments(parser: argparse.ArgumentParser) -> None:
    parser.add_argument("runs", nargs="+", type=Path, help=RUNS_HELP)
    parser.add_argument("-o", "--output", type=Path, required=True, help=OUTPUT_HELP)
    parser.add_argument(
        "--kinds", nargs="*", help="keep only these decision kinds (Evolve, Speed, Intent, Targets)"
    )
    parser.add_argument(
        "--validation", type=float, default=0.2, help="share of matches held out (default 0.2)"
    )
    parser.add_argument("--seed", type=int, default=0, help="the seed of the split and the optimizer")
    parser.add_argument("--allow-mixed", action="store_true", help="accept runs whose stamps differ")


def _add_search_weights(commands: argparse._SubParsersAction) -> None:
    search = commands.add_parser(
        "search-weights", help="cross-entropy search of the heuristic agent's weights"
    )
    search.add_argument(
        "-o", "--output", type=Path, required=True, help="the directory the weights are written to"
    )
    search.add_argument(
        "--initial", type=Path, help="a weights file to start from (default: the built-in weights)"
    )
    search.add_argument("--opponent", default="greedy", help="agent B of every evaluation (default greedy)")
    search.add_argument(
        "--seeds", default="benchmarks/benchmark-seeds.json", help="the seed file of every evaluation"
    )
    search.add_argument("--repo", type=Path, default=Path.cwd(), help=REPO_HELP)
    search.add_argument("--iterations", type=int, default=10)
    search.add_argument("--population", type=int, default=16)
    search.add_argument(
        "--elite", type=float, default=0.25, help="share of each population kept as the elite"
    )
    search.add_argument("--sigma", type=float, default=0.5, help="initial spread, relative to each weight")
    search.add_argument("--seed", type=int, default=0)
    search.add_argument(
        "--engine",
        nargs="+",
        default=None,
        help="the engine command prefix (default: dotnet run on the built Release CLI)",
    )
    search.set_defaults(handler=_search_weights)


def _add_tune_content(commands: argparse._SubParsersAction) -> None:
    tune = commands.add_parser(
        "tune-content", help="search the balance knobs for a catalogue closer to the objective"
    )
    tune.add_argument("-o", "--output", type=Path, required=True, help="the directory the proposal lands in")
    tune.add_argument("--knobs", type=Path, default=KNOBS_FILE, help=f"the knobs file (default {KNOBS_FILE})")
    tune.add_argument("--data", type=Path, default=Path("data"), help="the authored content directory")
    tune.add_argument("--iterations", type=int, default=8, help="rounds of neighbours (default 8)")
    tune.add_argument("--neighbours", type=int, default=4, help="candidates played per round (default 4)")
    tune.add_argument(
        "--max-changes", type=int, default=12, help="knobs one proposal may move at once (default 12)"
    )
    tune.add_argument("--seed", type=int, default=0)
    tune.add_argument(
        "--no-sweep",
        dest="sweep",
        action="store_false",
        help="skip the opening pass that plays every knob once, and start from random neighbours",
    )
    tune.add_argument(
        "--no-pairs",
        dest="pairs",
        action="store_false",
        help="skip the paired moves the sweep adds for spells no single step could improve the score on",
    )
    tune.add_argument(
        "--pair-depth",
        type=int,
        default=PAIR_DEPTH,
        help="how many steps one knob of a paired move may take once its first step moved a measurement "
        "without improving the score; 1 is the opening move alone",
    )
    tune.add_argument("--repo", type=Path, default=Path.cwd(), help=REPO_HELP)
    tune.add_argument(
        "--engine", nargs="+", help="the engine command prefix (default: dotnet run --project ...)"
    )
    tune.add_argument(
        "--apply",
        action="store_true",
        help="write the winning numbers into the content itself, not only into the output directory",
    )
    tune.set_defaults(handler=_tune_content)


def _add_check_knobs(commands: argparse._SubParsersAction) -> None:
    check = commands.add_parser("check-knobs", help="the balance knobs against the content they describe")
    check.add_argument(
        "--knobs", type=Path, default=KNOBS_FILE, help=f"the knobs file (default {KNOBS_FILE})"
    )
    check.add_argument("--data", type=Path, default=Path("data"), help="the authored content directory")
    check.add_argument(
        "--strict", action="store_true", help="fail on the content findings too, not only on the knobs file"
    )
    check.set_defaults(handler=_check_knobs)


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(prog="downfall-learning", description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)

    clone = commands.add_parser("train-clone", help="behaviour cloning: observation to action key")
    _add_dataset_arguments(clone)
    clone.add_argument("--epochs", type=int, default=20)
    clone.add_argument("--alpha", type=float, default=1e-4, help="L2 regularization strength")
    clone.set_defaults(handler=_train_clone)

    value = commands.add_parser("train-value", help="value regression: (observation, action) to return")
    _add_dataset_arguments(value)
    value.add_argument("--alpha", type=float, default=1.0, help="ridge regularization strength")
    value.add_argument("--min-samples", type=int, default=5, help="steps an action needs to get its own row")
    value.set_defaults(handler=_train_value)

    _add_search_weights(commands)

    evaluate = commands.add_parser(
        "evaluate-policy", help="play a trained policy against a baseline with the engine"
    )
    evaluate.add_argument("model", type=Path, help="the model directory holding policy.json")
    evaluate.add_argument("--opponent", default="greedy", help="agent B of the evaluation (default greedy)")
    evaluate.add_argument("--seeds", default="benchmarks/benchmark-seeds.json", help="the seed file")
    evaluate.add_argument("--repo", type=Path, default=Path.cwd(), help=REPO_HELP)
    evaluate.add_argument("--engine", nargs="+", default=None, help="the engine command prefix")
    evaluate.add_argument(
        "--output", type=Path, help="where to write the evaluation (default: in the model directory)"
    )
    evaluate.add_argument("--no-log", action="store_true", help="leave the model's training.jsonl untouched")
    evaluate.set_defaults(handler=_evaluate_policy)

    report = commands.add_parser("report", help="the report of a run directory, and what moved since another")
    report.add_argument("run", type=Path, help="a run directory holding evaluations/*.json")
    report.add_argument("--against", type=Path, help="the run directory to compare with")
    report.add_argument(
        "--no-html", action="store_true", help=f"do not write {RUN_PAGE}, the viewer page carrying the run"
    )
    report.add_argument(
        "--viewer", type=Path, help="the viewer directory (default: viewer/ at the repository root)"
    )
    report.set_defaults(handler=_report)

    csv = commands.add_parser("export-csv", help="the wide CSV projection of a dataset")
    csv.add_argument("runs", nargs="+", type=Path, help=RUNS_HELP)
    csv.add_argument("-o", "--output", type=Path, required=True, help="the CSV file to write")
    csv.add_argument("--allow-mixed", action="store_true")
    csv.set_defaults(handler=_export_csv)

    _add_check_knobs(commands)
    _add_tune_content(commands)

    stamps = commands.add_parser(
        "compare-stamps",
        help="what differs between the stamps of two artifacts (exit code 1 when something does)",
    )
    stamps.add_argument("before", type=Path, help="a manifest.json, evaluation.json, or policy.json")
    stamps.add_argument("after", type=Path, help="the artifact to compare with")
    stamps.set_defaults(handler=_compare_stamps)
    return parser


def _dataset(arguments: argparse.Namespace) -> Dataset:
    runs = load_runs(arguments.runs, allow_mixed=arguments.allow_mixed)
    return build_dataset(runs, kinds=getattr(arguments, "kinds", None))


def _write_policy(policy: Policy, directory: Path) -> None:
    path = policy.save(directory / POLICY_FILE)
    metrics = ", ".join(f"{name} {value:.4g}" for name, value in policy.metrics.items())
    print(f"Policy ({policy.kind}, {len(policy.action_keys)} actions) written to '{path}'. {metrics}.")


def _train_clone(arguments: argparse.Namespace) -> int:
    dataset = _dataset(arguments)
    log = TrainingLog(dataset.stamp, arguments.output / TRAINING_FILE)
    options = CloneOptions(arguments.epochs, arguments.alpha, arguments.validation, arguments.seed)
    _write_policy(train_clone(dataset, options, log), arguments.output)
    return 0


def _train_value(arguments: argparse.Namespace) -> int:
    dataset = _dataset(arguments)
    log = TrainingLog(dataset.stamp, arguments.output / TRAINING_FILE)
    options = ValueOptions(arguments.alpha, arguments.validation, arguments.seed, arguments.min_samples)
    _write_policy(train_value(dataset, options, log), arguments.output)
    return 0


def _search_weights(arguments: argparse.Namespace) -> int:
    initial = read_weights(arguments.initial) if arguments.initial else DEFAULT_WEIGHTS
    options = SearchOptions(
        arguments.iterations, arguments.population, arguments.elite, arguments.sigma, arguments.seed
    )
    evaluator = CliEvaluator(_engine(arguments), arguments.output / "work")
    log = TrainingLog(path=arguments.output / TRAINING_FILE)
    result = search_weights(evaluator, options, initial, log)
    result.write(arguments.output)
    print(format_search(result, evaluator.opponent, evaluator.calls, arguments.output / "weights.json"))
    return 0


def _engine(arguments: argparse.Namespace) -> EngineCommand:
    engine = EngineCommand(root=arguments.repo, opponent=arguments.opponent, seeds=arguments.seeds)
    return replace(engine, command=tuple(arguments.engine)) if arguments.engine else engine


def _evaluate_policy(arguments: argparse.Namespace) -> int:
    evaluator = CliEvaluator(_engine(arguments), arguments.model / "work")
    score = evaluate_policy(arguments.model, evaluator, arguments.output, update_log=not arguments.no_log)
    lines = [
        f"Policy of '{arguments.model}' played {score.matches} matches against {arguments.opponent}.",
        "",
    ]
    lines.extend(win_rate_lines(score, arguments.opponent))
    lines.extend(
        [
            "",
            f"  score {score.mean:.4f}, and anywhere from {score.low:.4f} to {score.high:.4f}.",
            f"  One half is even against {arguments.opponent}: a win counts one, a draw one half.",
        ]
    )
    print("\n".join(lines))
    return 0


def _report(arguments: argparse.Namespace) -> int:
    report = build_report(arguments.run)
    write_report(report, arguments.run)
    comparison = compare_reports(report, load_report(arguments.against)) if arguments.against else None
    print(format_report(report, comparison))
    if not arguments.no_html:
        page = write_run_page(arguments.run, arguments.viewer)
        print(f"\nOpen '{page}' in a browser: the viewer with this run already loaded.")
    return 0


def _export_csv(arguments: argparse.Namespace) -> int:
    dataset = build_dataset(load_runs(arguments.runs, allow_mixed=arguments.allow_mixed))
    path = export_wide_csv(dataset, arguments.output)
    print(f"{len(dataset)} steps written to '{path}'.")
    return 0


def _repository_of(knobs: Path) -> Path:
    """The repository the objective's agent paths are written against.

    They are the engine's own paths, written from the repository root and not from wherever the CLI was
    launched. The knobs file sits at ``<root>/data/balance/knobs.json``, so its own location is what says
    where that root is -- both `check-knobs` and `tune-content` read it the same way, because a search that
    starts before the check would reach the engine one candidate at a time instead of failing its preflight.
    """
    return knobs.resolve().parents[2]


def _check_knobs(arguments: argparse.Namespace) -> int:
    try:
        knobs = load_knobs(arguments.knobs)
        content = load_content(arguments.data)
    except KnobsError as error:
        print(error, file=sys.stderr)
        return 1

    problems = validate(knobs, content, root=_repository_of(arguments.knobs))
    for problem in problems:
        print(f"problem: {problem}", file=sys.stderr)
    reports = findings(content, knobs)
    for report in reports:
        print(f"finding: {report}")
    print(
        f"{len(knobs)} knobs over {len(knobs.spells)} spells, {len(content)} enabled spells in the content."
    )
    if problems:
        return 1
    return 1 if reports and arguments.strict else 0


def _tune_content(arguments: argparse.Namespace) -> int:
    try:
        knobs = load_knobs(arguments.knobs)
        content = load_content(arguments.data)
    except KnobsError as error:
        print(error, file=sys.stderr)
        return 1

    problems = validate(knobs, content, root=_repository_of(arguments.knobs))
    if problems:
        for problem in problems:
            print(f"problem: {problem}", file=sys.stderr)
        print("The knobs and the content disagree; fix that before searching.", file=sys.stderr)
        return 1

    engine = EngineCommand(root=arguments.repo, seeds=knobs.objective.seeds or EngineCommand().seeds)
    if arguments.engine:
        engine = replace(engine, command=tuple(arguments.engine))
    host = ContentEngine(engine=engine, data=arguments.data, workdir=arguments.output / "work")
    evaluator = EngineContentEvaluator(host, knobs.objective, content)
    options = TuneOptions(
        iterations=arguments.iterations,
        neighbours=arguments.neighbours,
        max_changes=arguments.max_changes,
        seed=arguments.seed,
        sweep=arguments.sweep,
        pairs=arguments.pairs,
        pair_depth=arguments.pair_depth,
    )
    result = tune_content(evaluator, knobs, content, options)
    result.write(arguments.output, arguments.data)
    print(format_result(result, knobs.objective))
    missing = knobs.objective.missing(result.best.metrics)
    if missing:
        print(f"Not measured, so not scored: {', '.join(missing)}.")
    if arguments.apply and result.improved:
        written = result.apply()
        print(f"Applied to {len(written)} spell file(s). Rebuild the content and regenerate the digest.")
    elif arguments.apply:
        print("Nothing improved, so nothing was applied.")
    print(f"{evaluator.calls} evaluation(s) played; the proposal is in '{arguments.output}'.")
    return 0


def _stamp_of(path: Path) -> RunStamp:
    if path.name == POLICY_FILE:
        return Policy.load(path).stamp
    if path.name.startswith("evaluation"):
        return load_evaluation(path).stamp
    return load_manifest(path).stamp


def _compare_stamps(arguments: argparse.Namespace) -> int:
    """Prints what moved and exits with 1 when something did, so a script can branch on it like diff."""
    before, after = _stamp_of(arguments.before), _stamp_of(arguments.after)
    differences = after.differences_from(before)
    if not differences:
        print("Same stamp: nothing moved.")
        return 0
    for difference in differences:
        print(difference)
    return 1


def main(argv: Sequence[str] | None = None) -> int:
    arguments = build_parser().parse_args(argv)
    try:
        return int(arguments.handler(arguments))
    except (ValueError, RuntimeError, OSError) as error:
        print(f"error: {error}", file=sys.stderr)
        return 1


def _run(command: str) -> int:
    return main([command, *sys.argv[1:]])


def train_clone_command() -> int:
    return _run("train-clone")


def train_value_command() -> int:
    return _run("train-value")


def search_weights_command() -> int:
    return _run("search-weights")


def export_csv_command() -> int:
    return _run("export-csv")


def evaluate_policy_command() -> int:
    return _run("evaluate-policy")


def report_command() -> int:
    return _run("report")


def compare_stamps_command() -> int:
    return _run("compare-stamps")


def check_knobs_command() -> int:
    return _run("check-knobs")


def tune_content_command() -> int:
    return _run("tune-content")


if __name__ == "__main__":
    sys.exit(main())
