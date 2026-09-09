"""The learning side of Downfall Arena (learning phase L6, ADR 0013).

The engine records datasets, evaluations, and traces as JSON; this package reads them, trains the first
policies, searches the heuristic agent's weights, and writes back the files the engine and the viewer read:
``policy.json``, a weights file, ``training.jsonl``.
"""

__all__ = ["__version__"]

__version__ = "0.1.0"
