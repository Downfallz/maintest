# code-reviewer lessons (branch claude/stage-scene-paintings-plan-bebsg7)

- A generator that reads its own output through a guarded dynamic import (`committed().catch(() => ({}))` in scripts/ux/pictures.mjs) loses that guard the moment it statically imports a page module that imports the output (table/picture.js -> page-art.js): a conflict marker in page-art.js kills the script that would rewrite it. Grep the generator's new imports for the generated file (scene paintings S3, staged tree 11e8228).
- Ninth time: the staged tree moved mid-review (1f8e68d -> 11e8228, `:where` and a showcase test added); hash the index with `git write-tree` at start and end of a staged review (scene paintings S3).
