"""Playing a trained policy against a baseline with the engine, and keeping the result with the model."""

from __future__ import annotations

from pathlib import Path

from downfall_learning.policy import POLICY_FILE
from downfall_learning.report import TRAINING_FILE, TrainingLog
from downfall_learning.search_weights import CliEvaluator, Score


def evaluation_name(opponent: str) -> str:
    """``evaluation-vs-greedy.json`` for ``greedy``; a path in the spec keeps only safe characters."""
    safe = "".join(character if character.isalnum() else "-" for character in opponent).strip("-")
    return f"evaluation-vs-{safe or 'opponent'}.json"


def evaluate_policy(
    model: Path, evaluator: CliEvaluator, output: Path | None = None, update_log: bool = True
) -> Score:
    """Evaluates ``<model>/policy.json`` as agent A and, unless told otherwise, logs the win rate with it.

    The engine refuses a policy trained under another feature schema, which surfaces as an
    ``EvaluationError``: the model then needs retraining on the current content, or evaluating on the content
    it knows.
    """
    model = Path(model)
    policy = model / POLICY_FILE
    if not policy.is_file():
        raise FileNotFoundError(f"'{model}' holds no {POLICY_FILE}.")
    score = evaluator.evaluate_spec(f"policy:{policy}", output or model / evaluation_name(evaluator.opponent))
    log_path = model / TRAINING_FILE
    if update_log and log_path.is_file():
        TrainingLog.load(log_path).record_evaluation(
            score.win_rate, score.win_rate_low, score.win_rate_high, score.matches
        )
    return score
