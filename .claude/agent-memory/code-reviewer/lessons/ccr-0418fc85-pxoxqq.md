# code-reviewer lessons (branch ccr-0418fc85-pxoxqq)

- A "staged diff" review can be committed under you mid-review: snapshot `git write-tree` first and compare it with the new HEAD's tree (promo P4, a9a3a68).
- `new Date(x).toISOString()` throws RangeError on an impossible month or day (2026-13-01): a date check built on it crashes where it should refuse, use `getTime()` first (promo P4 isDay, a9a3a68).
- A magic-bytes `ftyp` check at offset 4 also passes HEIC, AVIF and MOV as "mp4": read the major brand too (promo P4 fileType, a9a3a68).
- A record reader written beside an older one (`readGenerated` vs `readMedia`) dropped the shot/ratio/type checks the older one makes: diff the two validators field by field (promo P4, a9a3a68).
