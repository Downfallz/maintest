# code-reviewer lessons (branch fix/push-unknown-service-and-unknown-fields)
- A host answer reworded or moved to another status (502 "gone" to 400 `*.UnknownPushService`) silently changes what the pages do when they key on its words or status (`/gone/` in devices.js, table.js, battle-services.js): grep `table/*.js` for the old words and status before approving a host-only diff (#1209, 9164dc7).
- Keeping unknown JSON fields by re-reading the document inside the store's write doubles the storage round trips of every write; ask whether the reader can carry the text instead (#1211, 9164dc7).
