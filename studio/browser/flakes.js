// The flake report (ADR 0182): CI gives every journey one retry, and a test that failed and then passed is
// green, so this reads Playwright's JSON report and says which ones did. It prints to the log, because a
// session can read a job's log and cannot read a run's summary, and to the summary for a person.
//
//   node studio/browser/flakes.js <report.json>
import { appendFileSync, existsSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

/** Every test of a Playwright JSON report that passed only on a retry, as `file › title [project]`. */
export function flaked(report) {
  const found = [];
  const walk = (suite, titles) => {
    for (const spec of suite.specs ?? []) {
      for (const test of spec.tests ?? []) {
        if (test.status === 'flaky') found.push(`${spec.file} › ${[...titles, spec.title].join(' › ')} [${test.projectName}]`);
      }
    }
    // A file's suite is titled with its path, which the line already starts with.
    for (const child of suite.suites ?? []) walk(child, child.title === child.file ? titles : [...titles, child.title]);
  };
  for (const suite of report.suites ?? []) walk(suite, []);
  return found.sort();
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const file = process.argv[2];
  if (!file || !existsSync(file)) {
    console.log('No Playwright report to read: no test ran.');
  } else {
    const found = flaked(JSON.parse(readFileSync(file, 'utf8')));
    console.log(found.length === 0 ? 'No test flaked.' : `${found.length} test(s) passed only on the retry:`);
    for (const line of found) console.log(`::warning title=Flaky test::${line}`);
    if (found.length > 0 && process.env.GITHUB_STEP_SUMMARY) {
      appendFileSync(process.env.GITHUB_STEP_SUMMARY, ['### Passed only on the retry', '', ...found.map((line) => `- ${line}`), ''].join('\n'));
    }
  }
}
