# Lessons from claude/avis-issues-1109-1134-1118-fzde68

- A retention sweep that reuses a reader which degrades a bad document to "no entries" (NoticeDocuments.Read: unparseable or unknown kinds read as []) never erases that document: its "kept < read" test is 0 < 0. Probe every sweep with a corrupt and a newer-kind file before trusting a privacy promise built on it (#1118, AccountNoticeLog.EraseExpiredAsync, d6466b2).
- A PR that "closes" an issue must be read against every item of the issue, not only its title: #1118 also asked for a narrower mark-read event, left undone (d6466b2).
