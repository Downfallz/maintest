# host-reviewer lessons, BC4 effects kit PR

- The Dockerfile's `battle-build` stage copies only battle/, table/*.js, table/battle-art/ and data/presentation/spell-art.json: a battle/build.mjs that reads anything else of the repository (BC4: `../data/Spells` for the bench's names) fails `npm run build` in the image though the local build and every host test pass; reproduce the stage's layout in the scratchpad and build there (BC4 PR).
- The catalogue's ETag is the SHA-256 of its body (TableApi `_served`), not the content hash: a presentation field added to a card's visual reaches pages without a content-hash bump (BC4 PR).
