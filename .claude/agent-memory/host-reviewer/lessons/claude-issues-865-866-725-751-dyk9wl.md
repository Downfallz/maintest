# host-reviewer lessons, #865/#866/#725/#751 PR

- An index kept beside records (FeedbackIndex, #866) and healed by a listing of ids heals only membership: a row whose note failed after the record's save (index throw, 8 lost tries, a crash) stays wrong for good, a reopened piece left in `Closed` is never listed again; ask for a per-record version (blob ETag/LastModified from the same listing) in the index (#866 PR).
- A write path wrapped so a second store is written after the first (Indexed.SaveAsync) turns a failure of the second into a failed answer for a write that was kept: the player's POST /api/feedback answers 503 "could not be kept" and the retry reads TooSoon (R1 lesson again) (#866 PR).
- Host tests that build the desk over `new FeedbackIndex(store, store, clock)` but send through the raw store never exercise production's send-through-index wiring (TableHost passes `feedbackIndex.Records`): check which store each side of a test is given (#866 PR).
