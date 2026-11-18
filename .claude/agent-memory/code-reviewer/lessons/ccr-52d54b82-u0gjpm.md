# code-reviewer lessons (branch ccr-52d54b82-u0gjpm)

- A new `table/*.js` module, even one only bundled into game.js, fails Cli.Tests `TableFilesTests.Every_module_beside_the_page_is_served` until `src/DownfallArena.Cli/Table/TableFiles.cs` lists it: the node and browser suites stay green, only `dotnet test` catches it (BC2 bench, battle-bench.js, f1005e6).
- A page clock driven by Phaser's frame delta (scene `update`) instead of wall-clock timers runs in slow motion on a slow renderer: the bench's 2.5 s play took 6.2 s on desktop at 1 worker and timed a spec out at 4; measure click-to-settled per project (BC2, bench.ts:223, f1005e6).
- A value pushed into a `<select>` that has no such option reads back as '' (Number('') = 0): grep the select's options for every flag that sets it (`--speed 1.5` filmed 1x under a "1.5x" title) (BC2, bench.ts:167, f1005e6).
