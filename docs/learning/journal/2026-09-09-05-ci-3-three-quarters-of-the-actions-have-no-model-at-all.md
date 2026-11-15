# 2026-09-09. `ci-3`: three quarters of the actions have no model at all

- **What changed**: nothing but the diagnostic. `ci-3` repeats `ci-2` exactly — same explored dataset, same
  alpha 10 and min samples 50 — and returns the same numbers to the digit: loss 0.9607, r² 0.04694, accuracy
  0.3044, 0 of 400 against `Greedy`, 94.75% against `Random`. The loop is deterministic and `fittedActions`
  costs nothing.
- **The number**: **93 fitted actions out of 382**. Two hundred and eighty-nine keys kept the mean of their
  few examples instead of a regression, and a row that is a constant scores the same in every state. So for
  three quarters of the legal moves the policy cannot tell one position from another; it simply prefers
  whichever constant is largest. That is what the spell entropy of 2.70 is made of.
- **What it means, read with `ci-1`**: this is a squeeze, not a mystery. At a threshold of 5 nearly every row
  gets a regression, on far too few examples, and the fit comes out worse than the mean. At 50 the fit turns
  positive and three quarters of the rows lose their model. 62,358 steps over 382 keys is about 163 per key
  on average, skewed enough that only 93 clear fifty in the training split. Exploration multiplied the action
  keys by five and the dataset did not follow.
- **Decision**: raise `--matches` to 1000 before concluding anything about the shape of the model. The
  question "is one independent regression per action the wrong shape" cannot be answered on a dataset where
  most of those regressions were never fitted.
