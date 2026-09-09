import json
from pathlib import Path

import pytest

from conftest import write_run
from downfall_learning.artifacts import build_dataset, load_run
from downfall_learning.evaluate_policy import evaluate_policy, evaluation_name
from downfall_learning.report import TrainingLog
from downfall_learning.search_weights import CliEvaluator, EngineCommand, EvaluationError
from downfall_learning.train_clone import CloneOptions, train_clone
from downfall_learning.train_value import train_value


def a_model(tmp_path: Path, kind: str = "value") -> Path:
    dataset = build_dataset([load_run(write_run(tmp_path / "run", matches=20))], kinds=["Intent"])
    model = tmp_path / kind
    log = TrainingLog(dataset.stamp, model / "training.jsonl")
    policy = (
        train_value(dataset, log=log)
        if kind == "value"
        else train_clone(dataset, CloneOptions(epochs=3), log)
    )
    policy.save(model / "policy.json")
    return model


def test_the_evaluation_lands_next_to_the_model_and_fills_its_log(
    tmp_path: Path, fake_engine: list[str]
) -> None:
    model = a_model(tmp_path, "clone")
    evaluator = CliEvaluator(
        EngineCommand(root=tmp_path, command=tuple(fake_engine), opponent="random"), tmp_path / "w"
    )

    score = evaluator and evaluate_policy(model, evaluator)

    assert score.win_rate == 0.75
    assert (model / "evaluation-vs-random.json").is_file()
    rows = [json.loads(line) for line in (model / "training.jsonl").read_text().splitlines()]
    best = [row for row in rows if row.get("best")]
    assert len(best) == 1
    assert best[0]["winRate"] == 0.75
    assert best[0]["winRateLow"] == 0.7
    assert best[0]["matches"] == 400
    assert all("winRate" not in row for row in rows if not row.get("best"))
    assert rows[0]["stamp"]["featureSchema"].startswith("features:v1+")


def test_the_output_and_the_log_can_be_left_alone(tmp_path: Path, fake_engine: list[str]) -> None:
    model = a_model(tmp_path)
    before = (model / "training.jsonl").read_text()
    evaluator = CliEvaluator(EngineCommand(root=tmp_path, command=tuple(fake_engine)), tmp_path / "w")

    score = evaluate_policy(model, evaluator, output=tmp_path / "out" / "frozen.json", update_log=False)

    assert score.win_rate == 0.6
    assert (tmp_path / "out" / "frozen.json").is_file()
    assert not (model / "evaluation-vs-greedy.json").exists()
    assert (model / "training.jsonl").read_text() == before


def test_a_policy_of_another_schema_is_refused_by_the_engine(tmp_path: Path, fake_engine: list[str]) -> None:
    model = a_model(tmp_path)
    policy = json.loads((model / "policy.json").read_text())
    policy["schemaId"] = "features:v1"
    (model / "policy.json").write_text(json.dumps(policy))
    evaluator = CliEvaluator(EngineCommand(root=tmp_path, command=tuple(fake_engine)), tmp_path / "w")

    with pytest.raises(EvaluationError, match="another feature schema"):
        evaluate_policy(model, evaluator)


def test_a_directory_without_a_policy_is_refused(tmp_path: Path, fake_engine: list[str]) -> None:
    evaluator = CliEvaluator(EngineCommand(root=tmp_path, command=tuple(fake_engine)), tmp_path / "w")

    with pytest.raises(FileNotFoundError, match=r"policy\.json"):
        evaluate_policy(tmp_path / "nothing", evaluator)


def test_the_evaluation_file_name_follows_the_opponent() -> None:
    assert evaluation_name("greedy") == "evaluation-vs-greedy.json"
    assert (
        evaluation_name("heuristic:runs/search/weights.json")
        == "evaluation-vs-heuristic-runs-search-weights-json.json"
    )
    assert evaluation_name("::") == "evaluation-vs-opponent.json"
