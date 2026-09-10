"""Feature schemas: which observation layouts this package knows how to read (docs/learning/features.md)."""

from __future__ import annotations

from collections.abc import Sequence
from dataclasses import dataclass

SUPPORTED_VERSIONS = frozenset({"features:v1", "features:v2", "features:v3"})


class SchemaError(ValueError):
    """An observation layout this package cannot read, or two layouts that do not match."""


def schema_version(schema_id: str) -> str:
    """The version part of a schema id: ``features:v1+31987e1de3a9`` gives ``features:v1``."""
    version, _, _ = schema_id.partition("+")
    if not version:
        raise SchemaError(f"'{schema_id}' is not a feature schema id.")
    return version


def check_schema(schema_id: str, expected: str | None = None) -> str:
    """Refuses an unsupported version, and a different id than ``expected`` when one is given.

    Returns the version. The fingerprint after ``+`` ties a schema to one content and rule set, so two ids
    of the same version are still two layouts and never pass for each other.
    """
    version = schema_version(schema_id)
    if version not in SUPPORTED_VERSIONS:
        known = ", ".join(sorted(SUPPORTED_VERSIONS))
        raise SchemaError(f"Feature schema '{schema_id}' is not supported; known versions: {known}.")
    if expected is not None and schema_id != expected:
        raise SchemaError(f"Feature schema '{schema_id}' does not match '{expected}'.")
    return version


@dataclass(frozen=True)
class FeatureIndex:
    """Feature names by index, and indexes by name, as the manifest lists them."""

    names: tuple[str, ...]

    def __post_init__(self) -> None:
        if len(set(self.names)) != len(self.names):
            raise SchemaError("Feature names repeat.")

    def __len__(self) -> int:
        return len(self.names)

    def index_of(self, name: str) -> int:
        try:
            return self.names.index(name)
        except ValueError:
            raise SchemaError(f"No feature named '{name}'.") from None

    def with_prefix(self, prefix: str) -> Sequence[int]:
        """The indexes of every feature whose name starts with ``prefix`` (``own0_``, ``enemy1_``)."""
        return [index for index, name in enumerate(self.names) if name.startswith(prefix)]
