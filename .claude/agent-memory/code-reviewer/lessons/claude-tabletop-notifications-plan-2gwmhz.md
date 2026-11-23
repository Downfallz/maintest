# Lessons from claude/tabletop-notifications-plan-2gwmhz (N4)

- A "ready" flag set at the end of an async sync (`state.synced` in devices.js) is skipped by every early `return` above it: the `!state.supported` exit left an iPhone outside the home screen never ready, so the lobby never gave it the install steps the battle view does. Grep each early return in the function that sets the flag, and probe the unsupported browser (N4, table/devices.js:192, 5f38309).
- A fixed card at the top of the battle view covers the header tools (#settings, the menu) until answered; a brief whose Motion row says "leaves on the player's own turn" while its Left says that is not built: read the brief's beats against the code's hide paths (N4, table/push-offer.js, 5f38309).
