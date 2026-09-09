"""The command line: ``uv run --project learning <command> ...`` (docs/learning/training.md)."""

from __future__ import annotations

import argparse
import sys
from collections.abc import Sequence
from dataclasses import replace
from pathlib import Path

from downfall_learning.artifacts import Dataset, build_dataset, load_evaluation, load_manifest, load_runs
from downfall_learning.export import DEFAULT_WEIGHTS, export_wide_csv, read_weights
from downfall_learning.policy import POLICY_FILE, Policy
from downfall_learning.report import TRAINING_FILE, TrainingLog
from downfall_learning.search_weights import CliEvaluator, EngineCommand, SearchOptions, search_weights
from downfall_learning.stamps import RunStamp
from downfall_learning.train_clone import CloneOptions, train_clone
from downfall_learning.train_value import ValueOptions, train_value

RUNS_HELP = "one or more run directories recorded by 'simulate --record'"
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
    search.add_argument(
        "--repo", type=Path, default=Path.cwd(), help="the engine repository root (default: cwd)"
    )
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

    csv = commands.add_parser("export-csv", help="the wide CSV projection of a dataset")
    csv.add_argument("runs", nargs="+", type=Path, help=RUNS_HELP)
    csv.add_argument("-o", "--output", type=Path, required=True, help="the CSV file to write")
    csv.add_argument("--allow-mixed", action="store_true")
    csv.set_defaults(handler=_export_csv)

    stamps = commands.add_parser("compare-stamps", help="what differs between the stamps of two artifacts")
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
    engine = EngineCommand(root=arguments.repo, opponent=arguments.opponent, seeds=arguments.seeds)
    if arguments.engine:
        engine = replace(engine, command=tuple(arguments.engine))
    initial = read_weights(arguments.initial) if arguments.initial else DEFAULT_WEIGHTS
    options = SearchOptions(
        arguments.iterations, arguments.population, arguments.elite, arguments.sigma, arguments.seed
    )
    evaluator = CliEvaluator(engine, arguments.output / "work")
    log = TrainingLog(path=arguments.output / TRAINING_FILE)
    result = search_weights(evaluator, options, initial, log)
    result.write(arguments.output)
    print(
        f"Best weights after {evaluator.calls} evaluations: score {result.best.score.mean:.4f} "
        f"(initial {result.initial.score.mean:.4f}), win rate {result.best.score.win_rate:.4f}, "
        f"written to '{arguments.output / 'weights.json'}'."
    )
    return 0


def _export_csv(arguments: argparse.Namespace) -> int:
    dataset = build_dataset(load_runs(arguments.runs, allow_mixed=arguments.allow_mixed))
    path = export_wide_csv(dataset, arguments.output)
    print(f"{len(dataset)} steps written to '{path}'.")
    return 0


def _stamp_of(path: Path) -> RunStamp:
    if path.name == POLICY_FILE:
        return Policy.load(path).stamp
    if path.name.startswith("evaluation"):
        return load_evaluation(path).stamp
    return load_manifest(path).stamp


def _compare_stamps(arguments: argparse.Namespace) -> int:
    before, after = _stamp_of(arguments.before), _stamp_of(arguments.after)
    differences = after.differences_from(before)
    if not differences:
        print("Same stamp: nothing moved.")
        return 0
    for difference in differences:
        print(difference)
    return 0


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


def compare_stamps_command() -> int:
    return _run("compare-stamps")


if __name__ == "__main__":
    sys.exit(main())
