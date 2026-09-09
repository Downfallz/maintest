"""Run stamps: the identity every artifact carries (ADR 0013) and what differs between two of them."""

from __future__ import annotations

from collections.abc import Mapping
from dataclasses import dataclass
from typing import Any

AXES = ("content", "engine", "rules", "schema", "agents", "seed")


@dataclass(frozen=True)
class RunStamp:
    """The engine's ``RunStamp`` as written in every manifest, evaluation, and trace."""

    engine_version: str
    content_hash: str
    rule_set: Mapping[str, Any]
    feature_schema: str
    player1_agent: str
    player2_agent: str
    base_seed: int

    @classmethod
    def from_json(cls, data: Mapping[str, Any]) -> RunStamp:
        try:
            return cls(
                engine_version=str(data["engineVersion"]),
                content_hash=str(data["contentHash"]),
                rule_set=dict(data["ruleSet"]),
                feature_schema=str(data["featureSchema"]),
                player1_agent=str(data["player1Agent"]),
                player2_agent=str(data["player2Agent"]),
                base_seed=int(data["baseSeed"]),
            )
        except KeyError as error:
            raise ValueError(f"A run stamp needs the field {error}.") from None

    def to_json(self) -> dict[str, Any]:
        return {
            "engineVersion": self.engine_version,
            "contentHash": self.content_hash,
            "ruleSet": dict(self.rule_set),
            "featureSchema": self.feature_schema,
            "player1Agent": self.player1_agent,
            "player2Agent": self.player2_agent,
            "baseSeed": self.base_seed,
        }

    def differences_from(self, other: RunStamp) -> list[str]:
        """Names each axis on which ``other`` differs from this stamp, in the order of ``AXES``."""
        checks = (
            ("content", other.content_hash, self.content_hash),
            ("engine", other.engine_version, self.engine_version),
            ("rules", dict(other.rule_set), dict(self.rule_set)),
            ("schema", other.feature_schema, self.feature_schema),
            ("agents", (other.player1_agent, other.player2_agent), (self.player1_agent, self.player2_agent)),
            ("seed", other.base_seed, self.base_seed),
        )
        return [f"{axis}: {before} versus {after}" for axis, before, after in checks if before != after]
