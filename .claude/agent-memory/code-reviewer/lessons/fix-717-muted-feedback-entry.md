# fix/717-muted-feedback-entry

- A new request a page sends when it loads breaks the specs that assert the page's exact list of `/api/` paths
  (`battle-live.spec.js`, the invitation test; `battle-loop.spec.js`, the session replay test): grep
  `toEqual(['/api/` and `.sort()).toEqual` for any new fetch at load (#717, 47f3dad).
