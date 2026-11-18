# host-reviewer lessons, #814/#815 PR

- `StoredSessions.ReadRecordAsync` (since #814/#815) tells a missing record from an unparseable one; `RecordAsync` still folds both into null. A face-down guard reads `ReadRecordAsync`; check each new guard picks the right one (#814/#815 PR).
- TableServer's `/session/<id>` answers every StillOpenAsync true (open, unparseable, store failure) with the same "still being played" 409 and no console line: a corrupt record is a permanent, mislabelled refusal the operator cannot find; ask for its own text and a line whenever a cause is added there (#815 PR).
- A refusal added to `RedactedAsync` (the scenario's source table) is not predicted by the sheet's `unread`/`underWay` (PlayedAsync reads only the report's table): the sheet offers download and the click answers 409 with the message; acceptable, but the docs line for the code must name both tables (#814 PR).
- The review copy given by /reviewers is not a git repo but builds: run the host tests there (`dotnet test` writes `artifacts/` in the copy), never in the checkout (#814/#815 PR).
