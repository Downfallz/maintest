# Lessons from ccr-cf85caec-y25wlk (#1093, #966)

- A picture measure that compares a file with `getBoundingClientRect()` also reads `object-fit`: cover and contain
  draw the file at another width than the box, and the showcase's hero ground (cover) was the case #966 named.
- A regression test for a hang carries its own short timeout (`{ timeout: 5000 }`), or the bug coming back reads as
  a slow suite, not a failure.
