"""The mean of several policies, as one policy.

A policy is linear: a candidate's score is its row's dot product with the observation plus its bias, and a
value policy adds a baseline that is linear too. So the mean of several policies is exactly a policy, whose
score of any candidate on any board is the mean of their scores. Nothing is approximated and nothing is
chosen: every policy given weighs the same, which is what keeps this off the test set. A turn of the loop fits
one value policy per dataset seed, and those fits have read the same on every held-out number while differing
by 34 points in play from a fifth of their training matches (journal, 2026-09-16). Averaging them is the
cheapest question to ask of that: if the mean plays like the better fits, the spread was variance the mean
removes; if it collapses like the worst, the problem is where the fits disagree, off the recorded
distribution.

A key one policy never saw scores that policy's ``fallback`` and nothing else, so in the mean it contributes a
zero row and its fallback as bias for that key: the mean policy scores the key exactly as the mean of the
policies would have.
"""

from __future__ import annotations

from collections.abc import Sequence

import numpy as np

from downfall_learning.policy import Baseline, Policy
from downfall_learning.training import now_iso


def mean_policy(policies: Sequence[Policy]) -> Policy:
    """One policy scoring every candidate as the mean of the policies given."""
    if len(policies) < 2:
        raise ValueError("A mean needs at least two policies.")
    first = policies[0]
    for policy in policies[1:]:
        if policy.kind != first.kind:
            raise ValueError(f"The policies are not all of one kind ({first.kind} and {policy.kind}).")
        if policy.schema_id != first.schema_id or policy.feature_names != first.feature_names:
            raise ValueError("The policies were not observed under one feature schema.")
        if (policy.baseline is None) != (first.baseline is None):
            raise ValueError("Some of the policies carry a baseline and some do not.")
        # The seed is the one axis on which the fits of one turn differ by design; anything else means the
        # policies were trained under different engines, contents, rules or agents, and their mean is nothing.
        differences = first.stamp.differences_from(policy.stamp)
        differences = [line for line in differences if not line.startswith("seed")]
        if differences:
            raise ValueError("The policies' stamps differ on " + "; ".join(differences) + ".")

    keys = tuple(sorted(set().union(*(policy.action_keys for policy in policies))))
    weights = np.zeros((len(keys), len(first.feature_names)))
    bias = np.zeros(len(keys))
    for policy in policies:
        index = policy.scorer.key_index
        for position, key in enumerate(keys):
            row = index.get(key)
            if row is None:
                bias[position] += policy.fallback
            else:
                weights[position] += policy.weights[row]
                bias[position] += policy.bias[row]
    count = len(policies)
    weights /= count
    bias /= count

    baseline = None
    if first.baseline is not None:
        baselines = [policy.baseline for policy in policies if policy.baseline is not None]
        baseline = Baseline(
            np.mean([one.weights for one in baselines], axis=0),
            float(np.mean([one.bias for one in baselines])),
        )
    return Policy(
        kind=first.kind,
        stamp=first.stamp,
        schema_id=first.schema_id,
        feature_names=first.feature_names,
        action_keys=keys,
        weights=weights,
        bias=bias,
        fallback=float(np.mean([policy.fallback for policy in policies])),
        trained_at=now_iso(),
        baseline=baseline,
        # The fit metrics of the parts describe the parts; the mean has none of its own until it is played.
        metrics={"averaged": float(count), "actions": float(len(keys))},
    )
