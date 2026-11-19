# code-reviewer lessons (branch ccr-a049a66e-32s5hs)

- A script that encodes into `mkdtemp(tmpdir())` and then `renameSync`s into the repository fails EXDEV wherever /tmp is its own filesystem (tmpfs, another drive); the gate never runs the derive path, so only a probe shows it: use copyFileSync + rm, or a work folder inside the target (UX5, scripts/ux/pictures.mjs:172, eef5034).
- Every bare-command `execFileSync`/`spawnSync` under `scripts/` carries `// NOSONAR` with an S4036 reason (digest.mjs, rules/build.mjs); grep a new one against that precedent, Sonar raises a hotspot the local gate cannot see (UX5, pictures.mjs:167, eef5034).
