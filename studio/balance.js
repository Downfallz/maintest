// The balance knobs, read in the page (`data/balance/README.md`). The file says what a tuning pass may change
// about each spell, between which bounds, and -- the part no number can say -- what the spell is for.
//
// This module is the reading half and nothing else: it takes the knobs the host published and a spell document
// as the editor holds it, and answers what the content carries today, where that value sits inside its own
// band, and everything the two disagree about. It is the same set of disagreements `check-knobs` fails on
// (`learning/src/downfall_learning/knobs.py`, `validate` and `_knob_problems`); this page surfaces them where
// they are caused, it does not invent rules of its own.
//
// It is a separate module for the reason `backend.js` and `github.js` are (ADR 0024): `studio.js` touches the
// DOM at import time, so Node cannot import it, and this is the half worth testing. Nothing here reaches for a
// document, a fetch or the page's state -- everything it reads is an argument -- so a caller that has the
// knobs from anywhere can use it, and a reading is a pure function of (knob, document): after an edit moves a
// number, calling it again is the whole refresh.
//
// Keys are the **unversioned alias** (`spell:pummel`), never the versioned id: cutting a `:v2` in the studio
// repoints the alias, and the entry follows the spell instead of going stale on the version it replaced.

/** The one shape this page knows how to read. A file that says anything else is left unread, and says so. */
export const KNOBS_VERSION = 'knobs:v1';

/** The pointer the critical multiplier hangs off, and the one effect kind it applies to (`ResolutionRules`). */
const CRITICAL_CHANCE = '/criticalChance';
const DAMAGE = 'Damage';

/** 0.667 plus 0.05 does not land on 0.717, and a value authored at a band edge must not read as outside it. */
const EPSILON = 1e-9;

/** Ordinal, like `StringComparer.Ordinal` in the host and `sorted` in check-knobs. Never `localeCompare`. */
const ordinal = (left, right) => (left < right ? -1 : Number(left > right));

const isRecord = value => typeof value === 'object' && value !== null && !Array.isArray(value);
const isNumber = value => typeof value === 'number' && Number.isFinite(value);
const text = value => (typeof value === 'string' ? value : '');
const list = value => (Array.isArray(value) ? value : []);

/** Three decimals is what a move is rounded to, so a band edge and a moved value are spelled the same way. */
export function formatNumber(value) {
  return isNumber(value) ? String(Number(value.toFixed(3))) : '?';
}

/**
 * The knobs this catalogue carries, or the one honest line to show instead.
 *
 * The host does not publish them yet, and an older deployment never will: a page that threw, or drew an empty
 * sheet, would report the host's age as the content's problem. So every refusal names what is missing rather
 * than failing, and the caller has one line to print wherever the knobs would have gone.
 */
export function readBalance(catalogue) {
  const balance = catalogue?.balance;
  if (balance === undefined || balance === null) {
    return {
      ok: false,
      why: 'This host does not publish the balance knobs, so nothing here says what a tuning pass may change'
        + ' about a spell. They are authored in data/balance/knobs.json and checked by check-knobs.',
    };
  }

  if (!isRecord(balance)) {
    return {
      ok: false,
      why: 'What this host publishes as the balance knobs is not shaped like data/balance/knobs.json, so the'
        + ' page leaves it unread rather than guessing at it.',
    };
  }

  // The version is asked before the shape, the order `load_knobs` asks in: a file of a version this page does
  // not know may be shaped like anything, and reporting its shape would blame the file for being newer.
  if (text(balance.version) !== KNOBS_VERSION) {
    return {
      ok: false,
      why: `The balance knobs are version '${text(balance.version) || '(none)'}' and this page reads`
        + ` ${KNOBS_VERSION}. A version it does not know may mean anything, so it reads none of it.`,
    };
  }

  // Absent is not malformed: `load_knobs` defaults `spells` to none at all, which is a file that covers
  // nothing rather than a file that is wrong. Present and not an object is the one this page refuses.
  if (balance.spells !== undefined && !isRecord(balance.spells)) {
    return {
      ok: false,
      why: 'The balance knobs publish a `spells` that is not a map of alias to entry, so the page leaves the'
        + ' file unread rather than guessing at it.',
    };
  }

  return { ok: true, balance };
}

/**
 * The unversioned alias a spell id answers to, which is how the knobs file names it. The alias map is the
 * authority -- `spell:pummel` is only this spell's alias while it points here -- so the id with its `:vN` cut
 * off is a candidate to confirm, never an answer on its own.
 */
export function aliasOfSpell(spellId, aliases) {
  const map = isRecord(aliases) ? aliases : {};
  const id = text(spellId);
  if (!id) return null;
  const unversioned = id.replace(/:v\d+$/, '');
  if (map[unversioned] === id) return unversioned;
  return Object.keys(map).find(alias => map[alias] === id) ?? null;
}

/** One spell's entry, with every field the renderer reads made safe to read. Null when the file has none. */
export function entryFor(balance, alias) {
  const raw = balance?.spells?.[alias];
  if (!isRecord(raw)) return null;
  return {
    alias,
    name: text(raw.name),
    creatureClass: text(raw.class),
    intent: text(raw.intent).trim(),
    keep: list(raw.keep).map(text).filter(Boolean),
    note: text(raw.note).trim() || null,
    knobs: list(raw.knobs).filter(isRecord).map(knob => ({
      path: text(knob.path),
      minimum: knob.min,
      maximum: knob.max,
      step: knob.step,
    })),
  };
}

/**
 * The number a JSON pointer addresses in a spell document, or why it does not.
 *
 * The pointers in the file are into the spell as authored -- `/energyCost`, `/effects/0/amount` -- and the
 * document here is the draft being edited, so this is read against what is on screen and not against what is
 * on disk. Removing the effect a knob points at is exactly how a pointer stops addressing anything.
 */
export function readPointer(document, pointer) {
  const path = text(pointer);
  if (!path.startsWith('/')) {
    return { ok: false, reason: 'missing', message: `'${path}' is not a JSON pointer into the spell.` };
  }

  // Split and no more. RFC 6901 would unescape `~1` to `/` and `~0` to `~` here, and `_tokens` does not: it
  // splits the raw string. Reading a pointer differently from the checker that fails the build is worse than
  // reading it differently from the RFC, and no field of a spell has a `/` or a `~` in its name to escape.
  let node = document;
  for (const token of path.slice(1).split('/')) {
    node = step(node, token);
    if (node === undefined) {
      return { ok: false, reason: 'missing', message: `'${path}' addresses nothing in this spell.` };
    }
  }

  if (!isNumber(node)) {
    return { ok: false, reason: 'notANumber', message: `'${path}' addresses ${describe(node)}, which is not a number.` };
  }

  return { ok: true, value: node };
}

/** One token of a pointer, which is `_child`: a key of an object, or a digit index into an array. */
function step(node, token) {
  if (Array.isArray(node)) {
    return /^\d+$/.test(token) && Number(token) < node.length ? node[Number(token)] : undefined;
  }

  return isRecord(node) ? node[token] : undefined;
}

/** What a pointer landed on, short enough to sit in a sentence. */
function describe(node) {
  const shown = JSON.stringify(node) ?? String(node);
  return shown.length > 40 ? `${shown.slice(0, 39)}…` : shown;
}

const problem = (code, message) => ({ code, message });

/**
 * A spell deals damage, which is the only thing the critical multiplier reaches.
 *
 * A permanent damage effect does not count, because `_effects` groups it under `Damage:permanent` and
 * `_inert_critical` looks for `Damage` alone -- so check-knobs calls the knob inert there, and a page that
 * disagreed would clear a knob the build is about to refuse.
 */
function damaging(document) {
  return list(document?.effects).some(effect => isRecord(effect) && effect.kind === DAMAGE && !effect.permanent);
}

/**
 * One knob read against the spell as it stands: the value the content carries, where it sits in its band, and
 * everything wrong with the pair. The problems are `_knob_problems`' own, in its order and its words.
 *
 * `tone` is the whole reading in one word, for a page that colours by it: `bad` is a disagreement someone has
 * to fix, `edge` is a value with nowhere left to go on one side -- not wrong, but the thing a tuning pass
 * cannot move -- and `ok` is a number sitting inside its band with room either way.
 */
export function knobReading(knob, document, { duplicate = false } = {}) {
  const path = text(knob?.path);
  const shape = { path, minimum: knob?.minimum, maximum: knob?.maximum, step: knob?.step };
  if (!path.startsWith('/') || !isNumber(shape.minimum) || !isNumber(shape.maximum) || !isNumber(shape.step)) {
    return {
      ...shape,
      value: null,
      position: null,
      at: null,
      room: null,
      problems: [problem('malformed', `'${path || '(no pointer)'}' is not a knob: it needs a pointer and a numeric min, max and step.`)],
      tone: 'bad',
    };
  }

  const problems = [];
  if (duplicate) problems.push(problem('duplicate', `'${path}' is listed twice, so one of the two moves nothing.`));
  if (shape.minimum > shape.maximum) problems.push(problem('bounds', `Its bounds are the wrong way round: [${formatNumber(shape.minimum)}, ${formatNumber(shape.maximum)}].`));
  if (shape.step <= 0) problems.push(problem('step', `A step of ${formatNumber(shape.step)} moves nothing.`));

  const found = readPointer(document, path);
  if (!found.ok) {
    problems.push(problem(found.reason, found.message));
    return { ...shape, value: null, position: null, at: null, room: null, problems, tone: 'bad' };
  }

  const value = found.value;
  const at = placement(value, shape.minimum, shape.maximum);
  if (at === 'below' || at === 'above') {
    problems.push(problem('outside', `The content carries ${formatNumber(value)}, outside [${formatNumber(shape.minimum)}, ${formatNumber(shape.maximum)}]. A tuning pass would pull it back inside.`));
  }

  if (path === CRITICAL_CHANCE && !damaging(document)) {
    problems.push(problem('inert', 'The critical multiplier applies to damage only, and this spell deals none, so this knob cannot move anything.'));
  }

  return {
    ...shape,
    value,
    position: position(value, shape.minimum, shape.maximum),
    at,
    room: room(value, shape),
    problems,
    tone: knobTone(problems, at),
  };
}

/**
 * A knob in one word. `bad` is a disagreement someone has to fix, `edge` a value with nowhere left to go on
 * one side -- not wrong, but what a tuning pass cannot move -- and `ok` a number with room either way.
 */
function knobTone(problems, at) {
  if (problems.length) return 'bad';
  return at === 'min' || at === 'max' ? 'edge' : 'ok';
}

/** Where the value stands against its own bounds: at one of them, inside, or out. */
function placement(value, minimum, maximum) {
  if (value < minimum - EPSILON) return 'below';
  if (value > maximum + EPSILON) return 'above';
  if (value <= minimum + EPSILON) return 'min';
  if (value >= maximum - EPSILON) return 'max';
  return 'inside';
}

/** The value as a fraction of its band, clamped: a value outside it is drawn at the edge it left. */
function position(value, minimum, maximum) {
  const span = maximum - minimum;
  if (span <= 0) return 0.5;
  return Math.min(1, Math.max(0, (value - minimum) / span));
}

/**
 * How many moves are left each way. A move is a step added to the value the content carries rather than to a
 * grid (`data/balance/README.md`), so this counts from where the spell is, which is what the search would do.
 */
function room(value, knob) {
  if (knob.step <= 0 || knob.minimum > knob.maximum) return null;
  const steps = distance => Math.max(0, Math.floor(Number((distance / knob.step).toFixed(6))));
  return { down: steps(value - knob.minimum), up: steps(knob.maximum - value) };
}

/** Every knob of one entry, with the duplicate pointers marked the way `_knob_problems` marks them: the second one. */
export function readings(entry, document) {
  const seen = new Set();
  return list(entry?.knobs).map(knob => {
    const duplicate = seen.has(knob?.path);
    seen.add(knob?.path);
    return knobReading(knob, document, { duplicate });
  });
}

/**
 * One spell's whole balance reading: what it is for, what has to stay true, and what its numbers may do --
 * plus the one line a collapsed strip or a tree node shows instead of all of it.
 */
export function summarise(entry, document) {
  const knobs = readings(entry, document);
  const problems = entry?.intent ? [] : [problem('noIntent', 'This entry has no intent, so nothing says what its numbers are for.')];
  // An entry is only ever `ok` or `bad`: bounds are drawn around what a spell *is*, so a value authored at one
  // of its own bounds is the ordinary case -- 33 of the 36 entries have one -- and escalating that to the whole
  // spell would paint the catalogue amber and teach the reader to ignore the colour. `edge` stays on the knob.
  const tone = problems.length || knobs.some(knob => knob.tone === 'bad') ? 'bad' : 'ok';
  return { ...entry, knobs, problems, tone, headline: headline(entry, knobs) };
}

/** The entry in one line: what is wrong if anything is, and otherwise how much room the numbers have. */
function headline(entry, knobs) {
  const parts = [];
  if (!entry?.intent) parts.push('no intent');
  const bad = knobs.filter(knob => knob.tone === 'bad').length;
  const edge = knobs.filter(knob => knob.tone === 'edge').length;
  const count = `${knobs.length} knob${knobs.length === 1 ? '' : 's'}`;
  if (!knobs.length) parts.push('nothing may move');
  else if (bad) parts.push(`${count}, ${bad} the content disagrees with`);
  else if (edge) parts.push(`${count}, ${edge} at a band edge`);
  else parts.push(`${count}, all inside their bands`);
  return parts.join(' · ');
}

/** What balanced means, as the panel shows it: the evaluations played, and the band each metric should land in. */
export function objectiveOf(balance) {
  const objective = isRecord(balance?.objective) ? balance.objective : {};
  const evaluations = isRecord(objective.evaluations) ? objective.evaluations : {};
  return {
    seeds: text(objective.seeds),
    score: text(objective.score),
    evaluations: Object.entries(evaluations).map(([name, body]) => ({
      name,
      p1: text(body?.p1),
      p2: text(body?.p2),
      reads: text(body?.reads),
    })),
    targets: list(objective.targets).filter(isRecord).map(target => {
      const on = text(target.on);
      const minimum = isNumber(target.min) ? target.min : null;
      const maximum = isNumber(target.max) ? target.max : null;
      return {
        key: `${on}.${text(target.metric)}`,
        metric: text(target.metric),
        on,
        minimum,
        maximum,
        band: band(minimum, maximum),
        scale: isNumber(target.scale) ? target.scale : null,
        weight: isNumber(target.weight) ? target.weight : null,
        why: text(target.why),
        // A target reading an evaluation nobody plays is a term silently missing from every score
        // (`_objective_problems`), which is worth saying here rather than only in `check-knobs`.
        declared: Object.hasOwn(evaluations, on),
      };
    }),
  };
}

function band(minimum, maximum) {
  if (minimum !== null && maximum !== null) return `${formatNumber(minimum)} to ${formatNumber(maximum)}`;
  if (maximum !== null) return `at most ${formatNumber(maximum)}`;
  if (minimum !== null) return `at least ${formatNumber(minimum)}`;
  return 'no band, so nothing is ever outside it';
}

/** The hard rules, in the file's own order. A candidate that breaks one is not scored at all. */
export function constraintsOf(balance) {
  const constraints = isRecord(balance?.constraints) ? balance.constraints : {};
  return Object.entries(constraints).map(([name, body]) => ({
    name,
    enabled: Boolean(body?.enabled),
    what: text(body?.what),
    why: text(body?.why),
    spells: list(body?.spells).map(text).filter(Boolean),
  }));
}

/**
 * The knobs file against the catalogue it describes: the same disagreements `validate` fails on, over the
 * content the page is holding rather than the content on disk.
 *
 * `spells` are the catalogue's own spell rows -- id, name, path, enabled and the document. Two things follow
 * `validate` rather than intuition. A spell that is **off** is judged not at all: `load_content` keys only
 * enabled spells, so `validate` never reads its intent or its knobs, and flagging it here would put the page
 * in disagreement with a build that is green. Its entry still counts as known, and is still the only thing
 * saying what the spell was for. And a spell whose file did not survive parsing is left alone: its numbers
 * are whatever the broken file happened to hold, so every knob would read as addressing nothing.
 *
 * `constraintProblems` is `_constraint_problems`: a constraint naming a spell nothing resolves to checks
 * nothing at all, quietly, which is the kind of hole worth showing where the constraints are read.
 */
export function survey(balance, spells, aliases) {
  const map = isRecord(aliases) ? aliases : {};
  // Only the versions an alias reaches. `load_content` walks the alias map, not the folder, so a spell on disk
  // that nothing points at is not in the content at all: after **Save as next version** the `:v1` file is
  // still there and still enabled, and `validate` asks nothing of it. Counting it would turn every version cut
  // into a spell with no entry that check-knobs has never heard of.
  const addressable = list(spells)
    .map(spell => ({ spell, alias: aliasOfSpell(spell?.id, map) }))
    .filter(({ alias }) => alias !== null);

  const covered = new Set();
  const resting = new Set();
  const uncovered = [];
  const flagged = [];

  for (const { spell, alias } of addressable) {
    const entry = entryFor(balance, alias);
    if (!entry) {
      // An entry is owed for what the build carries; a spell that is off is not in it.
      if (spell.enabled !== false && !spell.problem) {
        uncovered.push({ id: text(spell.id), name: text(spell.name), path: text(spell.path), alias });
      }

      continue;
    }

    // Counted apart, because most of this catalogue is off: 27 of the 36 entries describe a spell that left
    // the build, and folding those into the coverage would read as more covered than the build is.
    if (spell.enabled === false) {
      resting.add(alias);
      continue;
    }

    covered.add(alias);
    const finding = spell.problem ? null : flag(entry, spell, alias);
    if (finding) flagged.push(finding);
  }

  // Every alias the content answers to, not only the one alias each spell was reached by: two aliases may
  // point at the same spell, and `content.spells` is keyed by alias, so `validate` knows both. Reading this
  // from one alias per spell would report the other as an entry for a spell nothing resolves to.
  const onDisk = new Set(list(spells).map(spell => text(spell?.id)).filter(Boolean));
  const known = new Set(Object.keys(map).filter(alias => onDisk.has(map[alias])));
  const entries = Object.keys(balance?.spells ?? {});
  return {
    entries: entries.length,
    knobs: entries.reduce((total, alias) => total + (entryFor(balance, alias)?.knobs.length ?? 0), 0),
    enabled: addressable.filter(({ spell }) => spell.enabled !== false).length,
    covered: covered.size,
    resting: resting.size,
    uncovered,
    // An entry for a spell no alias resolves to describes nothing: the spell was cut, or the alias was.
    unresolved: entries.filter(alias => !known.has(alias)).sort(ordinal),
    flagged,
    constraintProblems: constraintProblems(balance, known),
  };
}

/** One spell's disagreements, flattened so a finding names the pointer it is about. Null when there are none. */
function flag(entry, spell, alias) {
  const summary = summarise(entry, spell.document);
  if (summary.tone === 'ok') return null;
  return {
    alias,
    name: summary.name || text(spell.name),
    path: text(spell.path),
    tone: summary.tone,
    problems: [
      ...summary.problems,
      ...summary.knobs.flatMap(knob => knob.problems.map(carried => ({ ...carried, path: knob.path }))),
    ],
  };
}

/** A constraint naming a spell no alias resolves to is a constraint that quietly checks nothing. */
function constraintProblems(balance, known) {
  return constraintsOf(balance).flatMap(constraint => constraint.spells
    .filter(alias => !known.has(alias))
    .map(alias => ({ constraint: constraint.name, alias, message: `${constraint.name}: '${alias}' is not a spell any alias resolves to.` })));
}

// ---------- writing ----------
//
// The reading half above answers what the file and the content disagree about. This half is what a change
// carries back: one entry replaced, seeded or pruned, in a document written whole (ADR 0025). Nothing here
// touches the page or the network -- a writer takes the knobs it was given and returns new knobs, so the page
// can show what it is about to commit before it commits it.

/**
 * The knobs with one spell's entry replaced by `entry`, or removed when `entry` is null.
 *
 * Written whole and key by key rather than by spreading, so the order of `spells` survives a change: the file
 * is reviewed as a diff, and an edit that reorders 36 entries to change one is a diff nobody reads. A new
 * entry lands at the end, which is where a new spell belongs in a file read top to bottom.
 */
export function withEntry(balance, alias, entry) {
  const key = text(alias);
  if (!key) return balance;
  const spells = {};
  for (const [name, body] of Object.entries(balance?.spells ?? {})) {
    if (name !== key) {
      spells[name] = body;
    } else if (entry) {
      spells[name] = entry;
    }
  }

  if (entry && !Object.hasOwn(spells, key)) spells[key] = entry;
  return { ...balance, spells };
}

/**
 * The entry a spell is owed when it is first created: what the content already says about it, and nothing
 * invented. `intent` is the author's to write -- ADR 0021 is the argument that it cannot be derived, and
 * `check-knobs` fails on an empty one, which is the page's cue to ask rather than to guess.
 */
export function seedEntry(document, intent = '') {
  const entry = { name: text(document?.name), class: text(document?.creatureClass), intent: text(intent).trim() };
  // No knobs: every number of a new spell is its identity until someone says which of them may move.
  return { ...entry, keep: [], knobs: [] };
}

/** An entry as the file holds it, from the shape `entryFor` reads it into. The round trip has to be lossless. */
export function entryDocument(entry) {
  const written = {
    name: text(entry?.name),
    class: text(entry?.creatureClass),
    intent: text(entry?.intent).trim(),
    keep: list(entry?.keep).map(text).filter(Boolean),
  };
  if (entry?.note) written.note = text(entry.note).trim();
  written.knobs = list(entry?.knobs).map(knob => ({
    path: text(knob?.path),
    min: knob?.minimum,
    max: knob?.maximum,
    step: knob?.step,
  }));
  return written;
}

/**
 * Everything wrong with an entry that a browser can tell, which is the half of `validate` that needs only this
 * spell: an intent that says nothing, and each knob against the document it governs. The other half -- new
 * dominance, indistinguishable spells, the tiers, the objective's score -- needs the whole catalogue and the
 * engine, and stays with `check-knobs` in CI (ADR 0023, ADR 0025). A save is never blocked by what the page
 * cannot check; it is blocked by what it can.
 */
export function entryProblems(entry, document) {
  const summary = summarise(entry, document);
  return [
    ...summary.problems.map(problem => ({ ...problem, path: null, line: problem.message })),
    ...summary.knobs.flatMap(knob => knob.problems.map(problem => ({
      ...problem,
      path: knob.path || null,
      line: `${knob.path || '(no pointer)'}: ${problem.message}`,
    }))),
  ];
}

/** The codes that mean a pointer stopped addressing a number, which is what a new version of a spell breaks. */
export const STALE_POINTER = Object.freeze(['missing', 'notANumber']);

/** The starting-kit aliases a constraint names, which a deletion has to be checked against (ADR 0025). */
export function kitAliases(balance) {
  return constraintsOf(balance).flatMap(constraint => constraint.spells);
}

/**
 * Every pointer into a document that addresses a number, in the document's own order.
 *
 * It is the set a knob may hold, so it is built here rather than in the page and against the same idea of
 * "addresses a number" that `readPointer` reads by: a pointer this offers is a pointer that reads, and the
 * tests hold the two to each other. A pointer that reads can still be refused for what it *means* -- a
 * critical chance on a spell that deals no damage is the one the file documents -- and that is
 * `entryProblems`' to say, not this one's.
 *
 * Read from the draft rather than from the file, so adding an effect offers its numbers as soon as they are
 * typed.
 */
export function pointersOf(node, prefix = '') {
  if (Array.isArray(node)) return node.flatMap((item, index) => pointersOf(item, `${prefix}/${index}`));
  if (isRecord(node)) return Object.entries(node).flatMap(([key, value]) => pointersOf(value, `${prefix}/${key}`));
  return isNumber(node) && prefix ? [prefix] : [];
}

/** The first number of a document no knob of this entry claims yet, or nothing when every one is taken. */
export function unclaimedPointer(entry, document) {
  const taken = new Set(list(entry?.knobs).map(knob => text(knob?.path)));
  return pointersOf(document).find(candidate => !taken.has(candidate)) ?? null;
}

/**
 * A knob on the first number no other knob claims, pinned where the content already sits. The band is the
 * author's to widen: a bound this page invented would be a decision nobody made, sitting in the file as though
 * someone had (ADR 0021), and a band of no width says plainly that nothing may move yet.
 */
export function newKnob(entry, document) {
  const path = unclaimedPointer(entry, document) ?? '';
  const found = readPointer(document, path);
  const value = found.ok ? found.value : 0;
  return { path, minimum: value, maximum: value, step: Number.isInteger(value) ? 1 : 0.01 };
}
