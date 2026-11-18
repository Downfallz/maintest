// The anchors GitHub gives a markdown file's headings: lower case, punctuation dropped, a hyphen for a space, and
// a number after a repeated one. The tests that follow a link to a heading read it here (studio/prompts.test.js,
// studio/storyboards.test.js), so the two cannot drift.

export function headingAnchors(markdown) {
  const anchors = new Set();
  let fenced = false;
  for (const line of markdown.split('\n')) {
    if (line.startsWith('```')) fenced = !fenced;
    const heading = fenced ? null : /^#{1,6}\s+(.+?)\s*$/.exec(line);
    if (!heading) continue;
    const slug = heading[1].toLowerCase().replace(/[^\p{L}\p{N}\s_-]/gu, '').replace(/\s/g, '-');
    let anchor = slug;
    for (let n = 1; anchors.has(anchor); n++) anchor = `${slug}-${n}`;
    anchors.add(anchor);
  }
  return anchors;
}
