# host-reviewer lessons, #723/#693/#824 PR

- A listing narrowed to a span (`IFeedbackStore.IdsSentAsync(after, until)`, #693) gains an upper bound the old newest-first walk never had: an id stamped after `now` (clock stepped back, a revision swap's overlap) drops out of the repeat and ceiling guards; check the upper bound whenever a "stop at the oldest" loop becomes a span (#693 PR).
- A browser spec that holds a question and releases it later must assert the question was asked: a release of one never asked is a no-op, and "answered.length equals asked.length" then passes on nothing (#824 PR, feedback.spec.js).
- A refactor that only moves operator use cases out of a bag (OperatorFeedback -> one class each, #723) changes no route: compare the moved bodies line by line against `git show origin/main:<file>`, then questions 1-9 reduce to "same calls, same gate instance shared with SendFeedback" (#723 PR).
