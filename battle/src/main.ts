// The laboratory's page opens one of two studies: the sample action, or with `?bench` the effects bench
// (docs/art/battle-craft-plan.md). Both stay in this bundle; the live one carries neither.
if (new URLSearchParams(location.search).has('bench')) void import('./bench');
else void import('./lab');
