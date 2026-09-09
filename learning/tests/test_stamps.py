import pytest

from conftest import stamp_json
from downfall_learning.stamps import AXES, RunStamp


def test_a_stamp_round_trips_through_json() -> None:
    stamp = RunStamp.from_json(stamp_json())

    assert stamp.to_json() == stamp_json()
    assert RunStamp.from_json(stamp.to_json()) == stamp


def test_a_stamp_needs_every_field() -> None:
    with pytest.raises(ValueError, match="baseSeed"):
        RunStamp.from_json({key: value for key, value in stamp_json().items() if key != "baseSeed"})


def test_identical_stamps_differ_nowhere() -> None:
    assert RunStamp.from_json(stamp_json()).differences_from(RunStamp.from_json(stamp_json())) == []


@pytest.mark.parametrize(
    ("override", "axis"),
    [
        ({"contentHash": "d" * 64}, "content"),
        ({"engineVersion": "abcdef123456-dirty"}, "engine"),
        ({"ruleSet": {**stamp_json()["ruleSet"], "roundCap": 20}}, "rules"),
        ({"featureSchema": "features:v1+ba9876543210"}, "schema"),
        ({"player2Agent": "Random"}, "agents"),
        ({"baseSeed": 2}, "seed"),
    ],
)
def test_each_axis_is_named_when_it_moves(override: dict, axis: str) -> None:
    before = RunStamp.from_json(stamp_json())
    after = RunStamp.from_json(stamp_json(**override))

    differences = after.differences_from(before)

    assert len(differences) == 1
    assert differences[0].startswith(f"{axis}: ")
    assert axis in AXES
