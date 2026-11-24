# code-reviewer lessons (branch claude/fervent-dijkstra-x3kgqa)
- A source check that scans HTML by one attribute order (`<link rel="modulepreload" href=`) misses `href` first or a second spelling of the same module: a duplicate or a stray preload passes; parse the tags, not one spelling (#1207, table/lobby.test.js:428, d7c17e2).
- A reordering done for speed (a read issued first) leaves every suite green when reverted: ask for a request-order assertion in the spec, or say plainly it is held only by the measure (#1207, table/lobby.js:1171, d7c17e2).
