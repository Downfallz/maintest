# code-reviewer lessons (branch ccr-528951b6-zvxmy0)

- A hand-rolled PATH lookup with `existsSync(join(folder, 'dotnet'))` takes a directory: PATH=/usr/lib:/usr/bin returns /usr/lib/dotnet (the SDK folder) and spawn fails EACCES; check `statSync().isFile()` and X_OK, as `which` does (#707, studio/browser/dotnet.js:13, staged).
- `realpath` on a binary found on PATH breaks launchers that dispatch on argv0 (snap: /snap/bin/dotnet -> /usr/bin/snap); the dotnet muxer finds its root from /proc/self/exe, so spawn the PATH entry as found (#707, dotnet.js:13, staged).
- A port drawn from a hash over a range (5200-5999) can land on a local service (Postgres 5432, VNC 5900, RabbitMQ 5672): a non-HTTP listener makes fetch throw, read as "free", and Playwright's own "port already used" error names no override (#711, origin.js:27, staged).
- The author edited `lessons.md` directly in a fix PR (#707/#711) instead of leaving the stale line to /retro or `lessons/<branch>.md`: flag it, it is the file every in-flight PR is told not to touch.
