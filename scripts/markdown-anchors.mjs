// The anchors GitHub gives a markdown file's headings: lower case, punctuation dropped, a hyphen for a space, and
// a number after a repeated one. The tests that follow a link to a heading read it here (studio/prompts.test.js,
// studio/storyboards.test.js, studio/journey-briefs.test.js), so they cannot drift.

export function headingAnchors(markdown) {
  const anchors = new Set();
  let fenced = false;
  for (const line of markdown.split('\n')) {
    if (line.startsWith('```')) fenced = !fenced;
    const heading = fenced ? null : /^#{1,6}\s+(\S.*)$/.exec(line);
    if (!heading) continue;
    const slug = heading[1].trimEnd().toLowerCase().replace(/[^\p{L}\p{N}\s_-]/gu, '').replace(/\s/g, '-');
    let anchor = slug;
    let repeat = 0;
    while (anchors.has(anchor)) {
      repeat += 1;
      anchor = `${slug}-${repeat}`;
    }
    anchors.add(anchor);
  }
  return anchors;
}
