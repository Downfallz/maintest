import pytest

from downfall_learning.features import FeatureIndex, SchemaError, check_schema, schema_version


def test_the_version_is_the_part_before_the_fingerprint() -> None:
    assert schema_version("features:v1+31987e1de3a9") == "features:v1"
    assert schema_version("features:v1") == "features:v1"


def test_a_supported_id_passes_and_returns_its_version() -> None:
    assert check_schema("features:v1+31987e1de3a9") == "features:v1"


def test_every_published_version_is_readable() -> None:
    """A run recorded under an older layout stays analysable; only the engine refuses to play its policy."""
    assert check_schema("features:v2+31987e1de3a9") == "features:v2"


def test_an_unknown_version_is_refused() -> None:
    with pytest.raises(SchemaError, match="features:v9"):
        check_schema("features:v9+31987e1de3a9")


def test_a_different_fingerprint_of_the_same_version_is_another_layout() -> None:
    with pytest.raises(SchemaError, match="does not match"):
        check_schema("features:v1+31987e1de3a9", expected="features:v1+ba9876543210")


def test_an_empty_id_is_refused() -> None:
    with pytest.raises(SchemaError):
        schema_version("+abc")


def test_the_index_finds_features_by_name_and_prefix() -> None:
    index = FeatureIndex(("round_fraction", "own0_alive", "own0_energy", "enemy0_alive"))

    assert len(index) == 4
    assert index.index_of("own0_energy") == 2
    assert list(index.with_prefix("own0_")) == [1, 2]
    with pytest.raises(SchemaError, match="own1_alive"):
        index.index_of("own1_alive")


def test_repeated_names_are_refused() -> None:
    with pytest.raises(SchemaError, match="repeat"):
        FeatureIndex(("a", "a"))
