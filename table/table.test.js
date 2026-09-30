import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';
import * as transport from './transport.js';
import * as seats from './seats.js';
import * as session from './session.js';
import * as card from './card.js';
import * as board from './board.js';
import * as hand from './hand.js';
import * as feed from './feed.js';
import * as timeline from './timeline.js';
import * as mat from './mat.js';
import * as notes from './notes.js';
import * as ties from './ties.js';
import * as replay from './replay.js';
import * as guidance from './guidance.js';
import * as practice from './practice.js';

// A small DOM double exercises the shipped page without adding a browser dependency to the Node gate.
class Element {
  constructor(tag = 'div') {
    this.tagName = tag;
    this.children = [];
    this.dataset = {};
    this.attributes = {};
    this.events = {};
    this.style = { setProperty(name, value) { this[name] = value; } };
    this.className = '';
    this.scrollLeft = 0;
    this.hidden = false;
    this.classList = { toggle: (name, enabled) => {
      const classes = new Set(this.className.split(' ').filter(Boolean));
      if (enabled) classes.add(name); else classes.delete(name);
      this.className = [...classes].join(' ');
    } };
  }
  set textContent(text) { this.text = String(text); this.children = []; }
  get textContent() { return (this.text ?? '') + this.children.map(child => child.textContent).join(''); }
  append(...children) { this.children.push(...children); }
  replaceChildren(...children) { this.text = ''; this.children = children; }
  setAttribute(key, value) { this.attributes[key] = value; }
  getAttribute(key) { return this.attributes[key]; }
  click() { return this.events.click?.(); }
  addEventListener(key, action) { this.events[key] = action; }
  focus() { this.owner.activeElement = this; }
  scrollIntoView() { this.scrolledIntoView = true; }
  contains(node) { return node === this || this.children.some(child => child.contains(node)); }
  getBoundingClientRect() { return { top: 1200, bottom: 1460, left: 0, right: 600, height: 260 }; }
  querySelectorAll(selector) {
    return this.children.flatMap(child => [
      ...(selector === 'button' ? child.tagName === 'button' : selector === '[data-focus]' ? child.dataset.focus : child.dataset.scroll) ? [child] : [],
      ...child.querySelectorAll(selector),
    ]);
  }
}

function page() {
  const timers = new Map(); const delays = new Map(); let timerId = 0;
  const ids = [...readFileSync(new URL('./index.html', import.meta.url), 'utf8').matchAll(/id="([^"]+)"/g)].map(match => match[1]);
  const nodes = Object.fromEntries(ids.map(id => [id, new Element()]));
  const document = {
    activeElement: null, body: new Element('body'), documentElement: new Element('html'),
    getElementById: id => nodes[id],
    createElement: tag => Object.assign(new Element(tag), { owner: document }),
    querySelectorAll: selector => Object.values(nodes).flatMap(node => node.querySelectorAll(selector)),
  };
  for (const node of Object.values(nodes)) node.owner = document;
  const context = vm.createContext({ ...transport, ...seats, ...session, ...card, ...board, ...hand, ...feed, ...timeline, ...mat, ...notes, ...ties, ...replay, ...guidance, ...practice,
    document, storage: null, practice: null, URLSearchParams, console, innerHeight: 800, location: { search: '' }, setInterval: () => {},
    setTimeout: (action, delay) => { timers.set(++timerId, action); delays.set(timerId, delay); return timerId; }, clearTimeout: id => timers.delete(id),
  });
  const script = readFileSync(new URL('./table.js', import.meta.url), 'utf8').replace(/^import .*;\n/gm, '');
  vm.runInContext(script, context);
  const cards = new Map([['one', { id: 'one', name: 'First card', cost: 2 }], ['two', { id: 'two', name: 'Second card', cost: 3 }]]);
  const packages = new Map([
    ['tier:one:v1', { id: 'tier:one:v1', name: 'First package', level: 1, prerequisites: [], spells: ['one'], initiativeBonus: 1 }],
    ['tier:two:v1', { id: 'tier:two:v1', name: 'Second package', level: 2, prerequisites: ['tier:one:v1'], spells: ['two'], initiativeBonus: 0 }],
  ]);
  const state = { views: [], rendered: null, revision: 0, polling: false, error: '', evolving: null, seats: [], holder: 'player1', shown: null,
    acknowledged: null, announced: null, asked: null, sending: false, picked: [], chosen: null, cards, packages,
    catalogue: { cards: [...cards.values()], packages: [...packages.values()], rules: { roundCap: 16 } }, tab: 'board', feeds: new Map(),
  };
  const view = { waitingFor: 'Intent', waitingCreature: 1, waitingAsked: 1, options: { intent: { creatures: [{ creature: 1, castableSpells: ['one', 'two'] }] } },
    board: { roundNumber: 1, subPhase: 'IntentSelection', allies: [{ id: 1, name: 'First', health: 20, maxHealth: 20, energy: 4, knownSpells: ['one', 'two'] }], enemies: [{ id: 2, health: 10, maxHealth: 20 }], intents: [], timeline: [] }, feed: [],
  };
  const current = { seat: 'player1', view, transport: { seat: async () => ({ ok: true, body: view }), decide: async () => ({ ok: true }) } };
  state.views = [current]; state.seats = [current];
  return { context, document, nodes, state, view, current, timers, delays, draw: () => context.render(state, state.views) };
}

function held(p) { return p.nodes['own-hand'].children[0].children[0].children[1]; }

// The inspector's purchase controls, without the phone's way back to the package list.
function unlocks(p) { return p.nodes.mat.children[2].querySelectorAll('button').filter(node => node.className === 'atlas-unlock'); }

test('an unchanged poll leaves the focused card and scrolled hand in the DOM', () => {
  const p = page(); p.draw();
  const row = held(p); const first = row.children[0];
  row.scrollLeft = 120; first.focus(); p.draw();
  assert.equal(held(p), row);
  assert.equal(p.document.activeElement, first);
  assert.equal(row.scrollLeft, 120);
});

test('a card can be selected with the keyboard without a network poll or losing focus', () => {
  const p = page(); p.draw();
  const first = held(p).children[0]; first.focus(); held(p).scrollLeft = 95;
  let prevented = false;
  first.events.keydown({ key: ' ', preventDefault: () => { prevented = true; } });
  assert.equal(prevented, true);
  assert.equal(p.state.chosen, 'one');
  assert.equal(held(p).scrollLeft, 95);
  assert.equal(p.document.activeElement.dataset.focus, first.dataset.focus);
  assert.equal(p.document.activeElement.attributes['aria-pressed'], 'true');
});

test('the active hand groups the catalogue types without changing spell selection', () => {
  const p = page();
  p.state.cards.get('one').type = 'First type';
  p.state.cards.get('two').type = 'Second type';
  p.draw();
  const groups = held(p).children;
  assert.equal(groups.length, 2);
  assert.equal(groups[0].children[0].textContent, 'First type · 1');
  assert.equal(groups[1].children[0].textContent, 'Second type · 1');
  groups[1].children[1].children[0].click();
  assert.equal(p.state.chosen, 'two');
});

test('a new asking of the same kind clears the old selection', () => {
  const p = page(); p.draw(); p.state.chosen = 'one'; p.draw();
  p.view.waitingAsked += 1; p.draw();
  assert.equal(p.state.chosen, null);
  assert.equal(p.nodes.choices.children.at(-1).disabled, true);
});

test('legal targets are keyboard operable and still obey the host maximum', () => {
  const p = page(); p.view.waitingFor = 'Target';
  p.view.options = { target: { legalTargets: { candidates: [1, 2], minTargets: 1, maxTargets: 1 } } }; p.draw();
  p.nodes.enemies.children[0].events.keydown({ key: 'Enter', preventDefault() {} });
  assert.deepEqual([...p.state.picked], [2]);
  p.nodes.allies.children[0].events.click();
  assert.equal(p.state.picked.length, 1);
});

test('the round bar carries every creature at a glance: number, health, energy and a stun', () => {
  const p = page(); p.view.board.enemies[0].isStunned = true; p.draw();
  const [ally] = p.nodes['mini-allies'].children;
  const [enemy] = p.nodes['mini-enemies'].children;
  assert.match(ally.className, /mini-creature ally/);
  assert.equal(ally.textContent, '120ϟ4');
  assert.equal(enemy.textContent, '210ϟ0⊘');
  assert.equal(enemy.attributes['aria-label'], 'Creature 2, opponent, 10/20 health, 0 energy, 0 defense, stunned');
  assert.doesNotMatch(p.nodes['mini-board'].className, /targeting/);
});

test("a chip shows a creature's defense when it has some", () => {
  const p = page(); p.view.board.allies[0].totalDefense = 3; p.draw();
  const [ally] = p.nodes['mini-allies'].children;
  assert.equal(ally.textContent, '120ϟ4◇3');
  assert.match(ally.attributes['aria-label'], /, 4 energy, 3 defense(,|$)/);
  assert.equal(p.nodes['mini-enemies'].children[0].children.some(child => child.className === 'mini-defense'), false);
});

test('a spell aimed at an ally is aimed from the round bar, and any other chip opens the battlefield', () => {
  const p = page(); p.view.waitingFor = 'Target';
  p.view.options = { target: { legalTargets: { candidates: [1], minTargets: 1, maxTargets: 1 } } }; p.draw();
  assert.match(p.nodes['mini-board'].className, /targeting/);
  const [ally] = p.nodes['mini-allies'].children;
  assert.match(ally.className, /legal/);
  assert.equal(ally.dataset.focus, 'mini-target-1');
  assert.match(ally.attributes['aria-label'], /, legal target$/);
  ally.click();
  assert.deepEqual([...p.state.picked], [1]);
  const picked = p.nodes['mini-allies'].children[0];
  assert.match(picked.className, /picked confirm/);
  assert.match(picked.attributes['aria-label'], /, selected target, tap again to cast$/);
  assert.equal(picked.title, 'Tap again to cast');
  assert.equal(p.nodes.choices.children[0].textContent, 'Tap a selected target again, or Cast, to confirm.');
  assert.ok(!p.state.boardOpen);
  p.nodes['mini-enemies'].children[0].click();
  assert.equal(p.state.boardOpen, true);
  assert.deepEqual([...p.state.picked], [1]);
});

test('the creature the round is on is marked on its chip whoever is being asked', () => {
  const p = page(); p.view.waitingFor = null; p.view.waitingCreature = null;
  p.view.board.subPhase = 'Activation'; p.view.board.activationCursor = 1;
  p.view.board.timeline = [{ creature: 1, speed: 'Standard' }, { creature: 2, speed: 'Standard' }];
  p.draw();
  assert.match(p.nodes['mini-enemies'].children[0].className, /\bturn\b/);
  assert.match(p.nodes['mini-enemies'].children[0].attributes['aria-label'], /, acting now$/);
  assert.doesNotMatch(p.nodes['mini-allies'].children[0].className, /\bturn\b/);
});

test('with the battlefield shut, arrows and numbers pick targets from the chips, and focus stays on them', () => {
  const p = page(); p.view.waitingFor = 'Target';
  p.view.options = { target: { legalTargets: { candidates: [1, 2], minTargets: 1, maxTargets: 1 } } }; p.draw();
  for (const row of [...p.nodes.enemies.children, ...p.nodes.allies.children]) row.checkVisibility = () => false;
  const chip = p.nodes['mini-allies'].children[0];
  chip.closest = selector => (['#phase-dock', '#mini-board'].includes(selector) ? chip : null);
  let prevented = false;
  p.context.keyboardDecision(p.state, { key: 'ArrowRight', target: chip, preventDefault: () => { prevented = true; } });
  assert.equal(prevented, true);
  assert.match(p.document.activeElement.dataset.focus, /^mini-target-/);
  p.context.keyboardDecision(p.state, { key: '2', target: chip, preventDefault() {} });
  assert.deepEqual([...p.state.picked], [2]);
  const again = p.nodes['mini-enemies'].children[0];
  again.focus(); p.state.rendered = null; p.draw();
  assert.equal(p.document.activeElement.dataset.focus, 'mini-target-2');
});

test('closing the battlefield hands focus back to its button, and a chip opening it gives the battlefield focus', () => {
  const p = page(); p.draw();
  p.nodes['mini-enemies'].children[0].click();
  assert.equal(p.document.activeElement, p.nodes.board);
  p.nodes.board.children = [p.nodes.enemies];
  p.nodes.enemies.children[0].focus();
  p.context.showBoard(p.state, false);
  assert.equal(p.document.activeElement, p.nodes['board-toggle']);
});

test('opened while a target is asked for, the battlefield starts at the first creature the spell may take', () => {
  const p = page(); p.view.waitingFor = 'Target';
  p.view.options = { target: { legalTargets: { candidates: [1], minTargets: 1, maxTargets: 1 } } }; p.draw();
  p.nodes.allies.children[0].getBoundingClientRect = () => ({ top: 1700, bottom: 1900, left: 0, right: 600, height: 200 });
  p.nodes.board.scrollTop = 300;
  p.context.showBoard(p.state, true);
  assert.equal(p.nodes.board.scrollTop, 488);
});

test('a chip opens the battlefield at its own creature', () => {
  const p = page(); p.draw();
  p.nodes.enemies.children[0].getBoundingClientRect = () => ({ top: 1500, bottom: 1700, left: 0, right: 600, height: 200 });
  p.nodes['mini-enemies'].children[0].click();
  assert.equal(p.state.boardOpen, true);
  assert.equal(p.nodes.board.scrollTop, 288);
});

test('a replayed action shows what it did to health on the chip once applied', () => {
  const p = page(); p.draw();
  const creature = { id: 1, health: 17, maxHealth: 20, energy: 4 };
  const marks = { picked: [], candidates: [], playbackStage: 'after', playback: { actor: { id: 2 }, targets: [{ id: 1 }], frame: { before: [{ id: 1, health: 20 }] } } };
  const chip = p.context.miniCreature(p.state, creature, 'ally', marks);
  assert.match(chip.className, /replay-target/);
  assert.equal(chip.children[2].children.find(child => child.className === 'mini-delta harm').textContent, '−3');
  const before = p.context.miniCreature(p.state, creature, 'ally', { ...marks, playbackStage: 'before' });
  assert.equal(before.children[2].children.some(child => /mini-delta/.test(child.className)), false);
});

test('the compact battlefield reads current initiative with energy and defense', () => {
  const p = page(); p.view.board.allies[0].currentInitiative = 7; p.view.board.allies[0].totalDefense = 2; p.draw();
  const chip = p.nodes['mini-allies'].children[0];
  assert.match(chip.textContent, /ϟ4◇2↟7/);
  assert.match(chip.attributes['aria-label'], /, 2 defense, 7 initiative/);
});

test('candidates a spell treats alike share one preview line', () => {
  const p = page(); p.view.waitingFor = 'Target';
  p.view.options = { target: { actor: 1, spell: 'one', legalTargets: { candidates: [1, 3], minTargets: 1, maxTargets: 1 } } };
  const heal = { kind: 'HealOutcome', amount: 4 };
  p.view.guidance = [{ spell: 'one', cost: 1, energyAfterCost: 3, targets: [{ target: 1, plain: [heal], critical: [heal] }, { target: 3, plain: [heal], critical: [heal] }] }];
  p.draw();
  assert.match(p.nodes['decision-guide'].textContent, /Creatures 1, 3 · Heal 4/);
  assert.doesNotMatch(p.nodes['decision-guide'].textContent, /Creature 1 · /);
});

test('evolution keeps every creature accessible without mixing their package buttons', () => {
  const p = page(); p.view.waitingFor = 'Evolution';
  p.view.options = { evolution: { remainingPicks: 2, creatures: [{ creature: 1, availableTiers: ['tier:one:v1'] }, { creature: 3, availableTiers: ['tier:two:v1'] }] } }; p.draw();
  assert.match(p.nodes.choices.textContent, /First package/);
  assert.doesNotMatch(p.nodes.choices.textContent, /Second package/);
  p.nodes.choices.children[0].children[1].events.click();
  assert.match(p.nodes.choices.textContent, /Second package/);
  assert.equal(p.nodes.choices.querySelectorAll('[data-focus]').filter(node => node.dataset.focus.startsWith('evolve-package-')).length, 1);
});

// A package is bought whole and the card has to say so: its level, the initiative it is worth for the rest of
// the match, and every spell it teaches. A card that showed only the name would hide two thirds of the choice.
test('a package card names its level, its initiative and every spell it teaches', () => {
  const p = page(); p.view.waitingFor = 'Evolution';
  p.view.options = { evolution: { remainingPicks: 2, creatures: [{ creature: 1, availableTiers: ['tier:one:v1', 'tier:two:v1'] }] } }; p.draw();
  const text = p.nodes.choices.textContent;
  assert.match(text, /First package/);
  assert.match(text, /tier 1/);
  assert.match(text, /\+1 initiative/);
  assert.match(text, /First card/);
  assert.match(text, /no initiative/);
});

test('a seat change hides the table until the next player acknowledges it', () => {
  const p = page(); p.draw(); p.current.seat = 'player2'; p.draw();
  assert.equal(p.nodes.table.hidden, true);
  assert.equal(p.nodes.pass.hidden, false);
  p.state.holder = 'player2'; p.draw();
  assert.equal(p.nodes.table.hidden, false);
  assert.equal(p.nodes.pass.hidden, true);
});

test('a decision refusal remains visible across ordinary polls', async () => {
  const p = page(); p.draw();
  p.current.transport.decide = async () => ({ ok: false, status: 409, body: { message: 'Try again' } });
  await p.context.submit(p.state, p.current, { kind: 'Intent', spell: 'one' }); p.draw();
  assert.equal(p.nodes.problem.hidden, false);
  assert.equal(p.nodes.problem.textContent, 'Try again');
  assert.equal(p.state.sending, false);
});

test('a failed request releases the busy state and shows a recoverable error', async () => {
  const p = page(); p.draw();
  p.current.transport.decide = async () => { throw new Error('Offline'); };
  await p.context.submit(p.state, p.current, { kind: 'Intent', spell: 'one' });
  assert.equal(p.nodes.problem.hidden, false);
  assert.match(p.nodes.problem.textContent, /connection/);
  assert.equal(p.nodes.decision.attributes['aria-busy'], 'false');
});

test('overlapping refreshes make only one seat request', async () => {
  const p = page(); let finish; let calls = 0;
  p.current.transport.seat = () => { calls += 1; return new Promise(resolve => { finish = resolve; }); };
  const first = p.context.refresh(p.state); await p.context.refresh(p.state);
  assert.equal(calls, 1);
  // A handover prevents an acknowledgement from adding a second request.
  p.state.holder = null; finish({ ok: true, body: p.view }); await first;
  assert.equal(p.state.polling, false);
});

test('a response started before a submitted decision cannot redraw the old board', async () => {
  const p = page(); let finish;
  p.current.transport.seat = () => new Promise(resolve => { finish = resolve; });
  const request = p.context.refresh(p.state); p.state.revision += 1;
  finish({ ok: true, body: p.view }); await request;
  assert.equal(p.state.rendered, null);
});

test('a rejected acknowledgement is retried even when the board has not changed', async () => {
  const p = page(); let calls = 0;
  p.current.transport.seat = async () => { calls += 1; return { ok: calls > 1 }; };
  p.draw(); await p.state.announced; p.draw(); await p.state.announced;
  assert.equal(calls, 2);
  assert.equal(p.state.acknowledged, 'player1/1');
});

test('a revoked seat hides the stale board instead of leaving its controls available', async () => {
  const p = page(); p.draw(); await p.state.announced;
  p.current.transport.seat = async () => ({ ok: false, status: 403 });
  await p.context.refresh(p.state);
  assert.equal(p.nodes.table.hidden, true);
  assert.equal(p.state.views.length, 0);
});

test('a new question starts the decision sheet at the top', () => {
  const p = page(); p.draw(); p.nodes.decision.scrollTop = 400;
  p.view.waitingAsked += 1; p.draw();
  assert.equal(p.nodes.decision.scrollTop, 0);
});

test('target selection prominently names the host spell and retains the acting creature', () => {
  const p = page(); p.view.waitingFor = 'Target';
  p.view.options = { target: { actor: 1, spell: 'two', legalTargets: { candidates: [2], minTargets: 1, maxTargets: 1 } } };
  p.draw();
  assert.equal(p.nodes.asking.textContent, 'Creature 1 · Second card');
  assert.equal(p.nodes['decision-phase'].textContent, 'Targeting');
  assert.match(p.nodes.choices.children[0].textContent, /Choose 1 target on the battlefield/);
});

test('a target spell missing from the catalogue still identifies the cast with no legal target', () => {
  const p = page(); p.view.waitingFor = 'Target';
  p.view.options = { target: { actor: 1, spell: 'unknown-card', legalTargets: { candidates: [], minTargets: 1, maxTargets: 1 } } };
  p.draw();
  assert.equal(p.nodes.asking.textContent, 'Creature 1 · unknown-card');
  assert.match(p.nodes.choices.children.at(-1).textContent, /No legal target/);
});


test('a completed round keeps its recap compact without scrolling and preserves a manual opening', () => {
  const p = page();
  p.view.roundEvents = [{ sequence: 1, round: 2, event: { kind: 'RoundEnded', roundId: 1 } }];
  p.view.board.roundNumber = 2; p.draw();
  assert.equal(p.nodes.recap.hidden, false);
  assert.equal(p.nodes.recap.open, false);
  assert.equal(p.nodes.recap.scrolledIntoView, undefined);
  assert.equal(p.nodes['recap-title'].textContent, 'Round 1 recap');
  p.nodes.recap.open = true;
  p.state.chosen = 'one'; p.draw();
  assert.equal(p.nodes.recap.open, true);
  p.view.roundEvents.push({ sequence: 2, round: 3, event: { kind: 'RoundEnded', roundId: 2 } });
  p.view.board.roundNumber = 3; p.draw();
  assert.equal(p.nodes.recap.open, false);
  assert.equal(p.nodes['recap-title'].textContent, 'Round 2 recap');
});

test('a full previous round survives trimming the short activity log on a late first poll', async () => {
  const p = page(); p.state.holder = null;
  p.view.board.roundNumber = 3;
  p.view.feed = Array.from({ length: 70 }, (_, sequence) => ({ sequence, round: 2, event: {
    kind: 'CombatActionResolved', roundId: 2, resolution: { action: { actor: 1, spell: 'one', targets: [2] } }, appliedOutcomes: [],
  } }));
  p.view.feed.push({ sequence: 70, round: 3, event: { kind: 'RoundEnded', roundId: 2 } });
  await p.context.refresh(p.state);
  assert.equal(p.state.feeds.get('player1').entries.length, 60);
  assert.equal(p.state.feeds.get('player1').roundEvents.length, 71);
  p.state.holder = 'player1'; p.context.redraw(p.state);
  assert.equal(p.nodes['recap-actions'].children.length, 71);
  assert.match(p.nodes['recap-actions'].textContent, /First card/);
});

test('speed selection opens the acting spellbook as readable reference without offering a cast', () => {
  const p = page(); p.view.waitingFor = 'Speed'; p.view.options = { speed: { missing: [1] } }; p.draw();
  const row = p.nodes['own-hand'].children[0].children[0];
  assert.match(row.className, /active/);
  assert.match(row.textContent, /choose speed/);
  assert.match(held(p).children[0].className, /speed-spell/);
  assert.equal(held(p).children[0].events.click, undefined);
  assert.equal(p.nodes.planning.scrolledIntoView, true);
});

test('speed reference highlights positive host chances without repeating the spellbook in the guide', () => {
  const p = page();
  p.view.waitingFor = 'Speed'; p.view.options = { speed: { missing: [1] } };
  p.state.cards.get('one').critical = '35%';
  p.state.cards.get('one').effects = ['Damage 7'];
  p.state.cards.get('two').critical = '0%';
  p.view.guidance = [{ spell: 'one', standardCriticalChance: .77 }, { spell: 'two', standardCriticalChance: 0 }];
  p.draw();
  const [first, second] = held(p).children;
  assert.equal(first.tagName, 'details');
  assert.match(first.children[0].textContent, /First card.*2 energy.*77% crit/);
  assert.match(first.children[1].textContent, /Damage 7/);
  assert.doesNotMatch(second.textContent, /crit|0%/);
  assert.doesNotMatch(p.nodes['decision-guide'].textContent, /First card|Second card|77%|0%/);
  assert.equal(first.events.click, undefined);
});

test('speed references respect a zero host override and tolerate missing catalogue cards', () => {
  const p = page(); p.view.waitingFor = 'Speed'; p.view.options = { speed: {} };
  p.state.cards.get('one').critical = '35%';
  p.state.cards.delete('two');
  p.view.guidance = [{ spell: 'one', standardCriticalChance: 0 }]; p.draw();
  assert.doesNotMatch(held(p).children[0].textContent, /crit|35%/);
  assert.equal(held(p).children[1].children[0].textContent, 'two');
});

test('a new intent brings its controls and hand into view together, once', () => {
  const p = page(); p.draw();
  assert.equal(p.nodes.planning.scrolledIntoView, true);
  p.nodes.planning.scrolledIntoView = false;
  held(p).children[0].events.click();
  assert.equal(p.nodes.planning.scrolledIntoView, false);
  p.view.waitingAsked += 1; p.draw();
  assert.equal(p.nodes.planning.scrolledIntoView, true);
});

test('an already visible hand does not move when the question changes', () => {
  const p = page(); p.context.innerHeight = 2000;
  p.nodes.decision.getBoundingClientRect = () => ({ top: 20, bottom: 300, left: 700, right: 1000 }); p.draw();
  assert.equal(p.nodes.planning.scrolledIntoView, undefined);
});

test('repeated card taps only select; the fixed button declares once with the asking identity', async () => {
  const p = page(); const sent = [];
  p.current.transport.decide = async decision => { sent.push(decision); return { ok: true }; }; p.draw();
  const first = held(p).children[0];
  first.events.click(); first.events.click();
  assert.equal(sent.length, 0);
  assert.equal(p.nodes.choices.children[0].textContent, 'Declare First card');
  assert.equal(p.nodes['decision-guide'].children.length, 0);
  assert.match(held(p).children[0].textContent, /Selected/);
  await p.nodes.choices.children[0].click();
  assert.equal(sent.length, 1);
  assert.deepEqual(JSON.parse(JSON.stringify(sent[0])), { kind: 'Intent', creature: 1, spell: 'one', asked: 1 });
});

test('tapping a different card changes the selection instead of declaring', () => {
  const p = page(); let sent = 0;
  p.current.transport.decide = async () => { sent += 1; return { ok: true }; }; p.draw();
  held(p).children[0].events.click(); held(p).children[1].events.click();
  assert.equal(p.state.chosen, 'two'); assert.equal(sent, 0);
});

test('holding a keyboard key does not confirm a selected card', () => {
  const p = page(); p.draw(); held(p).children[0].events.click();
  held(p).children[0].events.keydown({ key: 'Enter', repeat: true, preventDefault() {} });
  assert.equal(p.state.sending, false);
});

test('tapping a selected target confirms it instead of removing it', async () => {
  const p = page(); const sent = []; p.view.waitingFor = 'Target';
  p.view.options = { target: { legalTargets: { candidates: [1, 2], minTargets: 1, maxTargets: 1 } } };
  p.current.transport.decide = async decision => { sent.push(decision); return { ok: true }; }; p.draw();
  p.nodes.enemies.children[0].events.click(); assert.equal(sent.length, 0);
  await p.nodes.enemies.children[0].events.click();
  assert.equal(sent.length, 1); assert.deepEqual([...sent[0].targets], [2]);
  assert.equal(sent[0].asked, 1);
});

test('multi-target confirmation requires the minimum and an explicit removal remains available', async () => {
  const p = page(); const sent = []; p.view.waitingFor = 'Target';
  p.view.options = { target: { legalTargets: { candidates: [1, 2], minTargets: 2, maxTargets: 2 } } };
  p.current.transport.decide = async decision => { sent.push(decision); return { ok: true }; }; p.draw();
  p.nodes.enemies.children[0].events.click(); await p.nodes.enemies.children[0].events.click();
  assert.equal(sent.length, 0); assert.deepEqual([...p.state.picked], [2]);
  p.nodes.allies.children[0].events.click();
  const remove = p.nodes.choices.querySelectorAll('button').find(button => button.textContent.startsWith('Remove'));
  assert.ok(remove); remove.events.click(); assert.equal(p.state.picked.length, 1);
  p.nodes.enemies.children[0].events.click();
  await p.nodes.allies.children[0].events.click();
  assert.equal(sent.length, 1); assert.deepEqual([...sent[0].targets].sort(), [1, 2]);
});

test('a single-target spell allows switching targets before the second tap', () => {
  const p = page(); p.view.waitingFor = 'Target';
  p.view.options = { target: { legalTargets: { candidates: [1, 2], minTargets: 1, maxTargets: 1 } } }; p.draw();
  p.nodes.enemies.children[0].events.click(); p.nodes.allies.children[0].events.click();
  assert.deepEqual([...p.state.picked], [1]);
});

test('in-flight confirmation ignores additional card taps', async () => {
  const p = page(); let release; let entered; let sent = 0;
  const started = new Promise(resolve => { entered = resolve; });
  p.current.transport.decide = () => { sent += 1; entered(); return new Promise(resolve => { release = resolve; }); }; p.draw();
  await p.state.announced; held(p).children[0].events.click();
  const pending = p.nodes.choices.children[0].click(); await started;
  held(p).children[1].events.click(); held(p).children[0].events.click();
  assert.equal(sent, 1); assert.equal(p.state.chosen, 'one');
  release({ ok: true }); await pending;
});

test('a detached card from an earlier asking cannot select or declare on the next one', async () => {
  const p = page(); p.draw(); const stale = held(p).children[0];
  p.state.views = [{ ...p.current, view: { ...p.view, waitingAsked: 2 } }]; p.draw();
  await stale.events.click();
  assert.equal(p.state.chosen, null); assert.equal(p.state.sending, false);
});

function talentFixture(p) {
  p.state.cards.get('one').creatureClass = 'North';
  p.state.cards.get('two').creatureClass = 'South';
  p.state.catalogue.trees = [{ name: 'Branch', spells: ['one', 'two'] }];
  p.view.board.allies[0].knownSpells = ['one'];
  p.view.board.allies.push({ id: 3, knownSpells: ['two'], acquiredTiers: ['tier:two:v1'] });
}

test('the talent reference follows the inspected creature and the class filter immediately', () => {
  const p = page(); talentFixture(p); p.state.inspectClass = 'tier:two:v1'; p.draw();
  const toolbar = p.nodes.mat.children[0];
  const south = p.nodes.mat.children[2];
  assert.match(south.textContent, /Second package · Tier 2/);
  assert.match(south.textContent, /Requires: First package/);
  toolbar.children[2].children[1].events.click();
  assert.match(p.nodes.mat.children[2].textContent, /✓ Package acquired/);
  const filter = p.nodes.mat.children[0].children[3];
  filter.value = 'tier:one:v1'; filter.events.change();
  assert.match(p.nodes.mat.children[2].textContent, /First package/);
  const again = p.nodes.mat.children[0].children[3]; again.value = 'tier:two:v1'; again.events.change();
  assert.equal(p.nodes.mat.children.length, 3);
  assert.match(p.nodes.mat.children[2].textContent, /Second package/);
  assert.doesNotMatch(p.nodes.mat.children[2].textContent, /First card/);
});

test('the talent inspector uses the selected evolution creature and offers only its legal unlocks', () => {
  const p = page(); talentFixture(p); p.view.waitingFor = 'Evolution';
  p.view.options = { evolution: { creatures: [{ creature: 1, availableTiers: ['tier:two:v1'] }, { creature: 3, availableTiers: [] }] } };
  p.state.inspectClass = 'tier:two:v1'; p.draw(); assert.match(p.nodes.mat.children[2].textContent, /Available now/);
  assert.equal(unlocks(p).length, 1);
  p.nodes.choices.children[0].children[1].events.click();
  assert.match(p.nodes.mat.children[2].textContent, /✓ Package acquired/);
  assert.doesNotMatch(p.nodes.mat.textContent, /Available now/);
  assert.equal(unlocks(p).length, 0);
});

test('an opponent card is marked as a reference rather than as a spell nobody may cast', () => {
  const p = page(); p.view.board.enemies[0].knownSpells = ['one']; p.draw();
  const face = p.nodes['enemy-hand'].children[0].children[1].children[0];
  assert.match(face.className, /reference/);
  assert.match(face.textContent, /Spell reference/);
  assert.doesNotMatch(face.textContent, /Not available now/);
});

test('the battlefield opens over the page from the round bar and closes on a second tap or Escape', () => {
  const p = page(); p.draw();
  p.context.showBoard(p.state, true);
  assert.match(p.document.body.className, /board-open/);
  assert.equal(p.nodes['board-toggle'].attributes['aria-pressed'], 'true');
  assert.match(p.nodes['board-toggle'].textContent, /Battlefield ✕/);
  assert.equal(p.document.documentElement.style['--board-top'], '1460px');
  let prevented = false;
  p.context.keyboardDecision(p.state, { key: 'Escape', preventDefault: () => { prevented = true; } });
  assert.equal(prevented, true);
  assert.doesNotMatch(p.document.body.className, /board-open/);
  assert.equal(p.nodes['board-toggle'].attributes['aria-pressed'], 'false');
  assert.equal(p.nodes['board-toggle'].textContent, 'Battlefield');
});

test('a new question waits under an open battlefield and is guided to when it closes', () => {
  const p = page(); p.draw(); p.nodes.planning.scrolledIntoView = false;
  p.context.showBoard(p.state, true);
  p.view.waitingAsked++; p.draw();
  assert.equal(p.nodes.planning.scrolledIntoView, false);
  p.context.showBoard(p.state, false);
  assert.equal(p.nodes.planning.scrolledIntoView, true);
});

test('a handover closes the battlefield so the next seat starts at its own move', () => {
  const p = page(); p.draw(); p.context.showBoard(p.state, true);
  p.state.holder = 'player2'; p.state.rendered = null; p.draw();
  assert.equal(p.nodes.pass.hidden, false);
  assert.equal(p.state.boardOpen, false);
  assert.doesNotMatch(p.document.body.className, /board-open/);
});

test('on a phone, choosing a package in the atlas brings its spells up with a way back to the list', () => {
  const p = page(); talentFixture(p); p.context.innerWidth = 390; p.draw();
  const tree = p.nodes.mat.children[1].children[0].children[0];
  tree.children[0].click();
  const inspector = p.nodes.mat.children[2];
  assert.equal(inspector.scrolledIntoView, true);
  assert.equal(inspector.children[0].className, 'atlas-back reference-link');
  assert.match(inspector.textContent, /First package · Tier 1/);
  p.state.rendered = null; p.draw();
  assert.equal(p.nodes.mat.children[2].scrolledIntoView, undefined);
});

test('opponent spellbooks grow only from public known spells and preserve their expansion', () => {
  const p = page(); p.view.board.enemies[0].knownSpells = ['one']; p.draw();
  let row = p.nodes['enemy-hand'].children[0];
  assert.match(row.textContent, /1 revealed spells/);
  assert.match(row.textContent, /First card/); assert.doesNotMatch(row.textContent, /Second card/);
  assert.equal(row.children[1].children[0].events.click, undefined);
  row.open = true; row.events.toggle();
  p.view.board.enemies[0].knownSpells.push('two'); p.draw();
  row = p.nodes['enemy-hand'].children[0];
  assert.equal(row.open, true); assert.match(row.textContent, /2 revealed spells/);
  assert.match(row.textContent, /Second card/);
  p.view.board.enemies[0].knownSpells = []; p.draw();
  assert.match(p.nodes['enemy-hand'].textContent, /No spells revealed yet/);
  assert.doesNotMatch(p.nodes['enemy-hand'].textContent, /First card/);
});

test('battlefield turn badges appear only after the timeline exists and follow its current cursor', () => {
  const p = page(); p.draw(); assert.doesNotMatch(p.nodes.allies.textContent, /Turn/);
  p.view.board.timeline = [{ creature: 2 }, { creature: 1 }];
  p.view.board.subPhase = 'Activation'; p.view.board.activationCursor = 1; p.draw();
  assert.match(p.nodes.enemies.textContent, /Turn 1/); assert.match(p.nodes.allies.textContent, /Turn 2/);
  const number = p.nodes.allies.children[0].children[3].children[0].children[0];
  assert.match(number.className, /now/); assert.equal(number.attributes['aria-label'], 'Acts 2 of 2');
  p.view.board.timeline = []; p.draw(); assert.doesNotMatch(p.nodes.allies.textContent, /Turn/);
});

test('the initial talent preview follows the first offered evolution creature even if an earlier ally is absent from options', () => {
  const p = page(); talentFixture(p); p.view.waitingFor = 'Evolution'; p.view.waitingCreature = null;
  p.view.options = { evolution: { creatures: [{ creature: 3, availableTiers: ['tier:one:v1'] }] } }; p.draw();
  const filter = p.nodes.mat.children[0].children[3]; filter.value = 'tier:one:v1'; filter.events.change();
  assert.match(p.nodes.mat.children[2].textContent, /Available now/);
  p.state.inspectClass = 'tier:two:v1'; p.draw();
  assert.match(p.nodes.mat.children[2].textContent, /✓ Package acquired/);
});

const keyEvent = (key, extra = {}) => ({ key, preventDefault() {}, ...extra });

test('number shortcuts select without committing and Enter submits the selected card once', async () => {
  const p = page(); const sent = [];
  p.current.transport.decide = async decision => { sent.push(decision); return { ok: true }; }; p.draw();
  await p.context.keyboardDecision(p.state, keyEvent('2'));
  await p.context.keyboardDecision(p.state, keyEvent('2'));
  assert.equal(p.state.chosen, 'two'); assert.equal(sent.length, 0);
  await p.context.keyboardDecision(p.state, keyEvent('Enter'));
  assert.equal(sent.length, 1); assert.equal(sent[0].spell, 'two');
});

test('typing, modifiers, held keys and the handover fence cannot invoke gameplay shortcuts', async () => {
  const p = page(); p.draw();
  for (const extra of [{ repeat: true }, { ctrlKey: true }, { metaKey: true }, { altKey: true }, { isComposing: true },
    { target: { tagName: 'TEXTAREA' } }, { target: { tagName: 'SELECT' } }, { target: { isContentEditable: true } }]) {
    await p.context.keyboardDecision(p.state, keyEvent('1', extra));
    assert.equal(p.state.chosen, null);
  }
  p.state.holder = 'player2'; await p.context.keyboardDecision(p.state, keyEvent('1'));
  assert.equal(p.state.chosen, null);
});

test('target shortcuts respect the minimum and Escape clears the unsubmitted selection', async () => {
  const p = page(); const sent = []; p.view.waitingFor = 'Target';
  p.view.options = { target: { legalTargets: { candidates: [2, 1], minTargets: 2, maxTargets: 2 } } };
  p.current.transport.decide = async decision => { sent.push(decision); return { ok: true }; }; p.draw();
  await p.context.keyboardDecision(p.state, keyEvent('1'));
  await p.context.keyboardDecision(p.state, keyEvent('Enter')); assert.equal(sent.length, 0);
  await p.context.keyboardDecision(p.state, keyEvent('2'));
  await p.context.keyboardDecision(p.state, keyEvent('Enter')); assert.deepEqual([...sent[0].targets], [2, 1]);
  await p.context.keyboardDecision(p.state, keyEvent('Escape')); assert.equal(p.state.picked.length, 0);
});

test('the atlas leaves the battlefield visible and Escape closes it without losing the card selection', async () => {
  const p = page(); p.draw(); p.state.chosen = 'one';
  await p.context.keyboardDecision(p.state, keyEvent('t'));
  assert.equal(p.nodes['talent-window'].hidden, false); assert.equal(p.nodes.board.hidden, false);
  await p.context.keyboardDecision(p.state, keyEvent('Escape'));
  assert.equal(p.nodes['talent-window'].hidden, true); assert.equal(p.state.chosen, 'one');
});

test('Enter on a focused control is left to that control instead of activating twice', async () => {
  const p = page(); p.draw(); p.state.chosen = 'one';
  await p.context.keyboardDecision(p.state, keyEvent('Enter', { target: { closest: () => ({}) } }));
  assert.equal(p.state.sending, false);
});

test('speed shortcuts submit the named speed and atlas numbers only change the inspected creature', async () => {
  const p = page(); const sent = []; p.view.waitingFor = 'Speed'; p.view.options = { speed: {} };
  p.current.transport.decide = async decision => { sent.push(decision); return { ok: true }; }; p.draw();
  await p.context.keyboardDecision(p.state, keyEvent('2')); assert.equal(sent[0].speed, 'Standard');
  await p.context.keyboardDecision(p.state, keyEvent('t'));
  await p.context.keyboardDecision(p.state, keyEvent('1'));
  assert.equal(p.state.inspectCreature, 1); assert.equal(sent.length, 1);
});

test('enemy cards show public speed and the revealed spell while keeping the new round distinct', () => {
  const p = page(); p.view.board.timeline = [{ creature: 2, speed: 'Quick' }]; p.draw();
  assert.match(p.nodes.enemies.textContent, /Speed · Quick/);
  assert.match(p.nodes.enemies.textContent, /Hidden until reveal/);
  assert.doesNotMatch(p.nodes.enemies.textContent, /First card/);
  p.view.board.revealedActions = [{ actor: 2, spell: 'one', targets: [1] }]; p.draw();
  assert.match(p.nodes.enemies.textContent, /Round 1 · RevealedFirst card→ Creature 1/);
  p.view.board.roundNumber = 2; p.view.board.revealedActions = []; p.view.board.timeline = [];
  p.view.roundEvents = [{ round: 2, event: { kind: 'CombatActionResolved', roundId: 1, resolution: { action: { actor: 2, spell: 'one', targets: [1] } } } }]; p.draw();
  assert.match(p.nodes.enemies.textContent, /Round 2 · Hidden until revealLast round \(1\): First card/);
  assert.doesNotMatch(p.nodes.enemies.textContent, /Speed · Quick/);
});

test('an atlas unlock uses the guarded current asking and rejects a stale inspector control', async () => {
  const p = page(); talentFixture(p); const sent = []; p.view.waitingFor = 'Evolution';
  p.view.options = { evolution: { creatures: [{ creature: 1, availableTiers: ['tier:two:v1'] }] } };
  p.current.transport.decide = async decision => { sent.push(decision); return { ok: true }; };
  p.state.inspectClass = 'tier:two:v1'; p.draw();
  const unlock = unlocks(p)[0];
  await unlock.events.click(); assert.equal(sent.length, 1); assert.equal(sent[0].creature, 1); assert.equal(sent[0].tier, 'tier:two:v1'); assert.equal(sent[0].spell, undefined);
  p.state.views = [{ ...p.current, view: { ...p.view, waitingAsked: 2 } }]; p.draw();
  await unlock.events.click(); assert.equal(sent.length, 1);
});

test('targeting exposes only confirmed spells and targets while later choices stay hidden', () => {
  const p = page(); p.view.waitingFor = 'Target'; p.view.board.subPhase = 'Activation';
  p.view.options = { target: { actor: 1, spell: 'one', legalTargets: { candidates: [2], minTargets: 1, maxTargets: 1 } } };
  p.view.board.timeline = [{ creature: 1 }, { creature: 2 }];
  p.view.board.revealedIntents = [{ actor: 1, spell: 'one' }, { actor: 2, spell: 'two' }];
  p.view.board.revealedActions = []; p.draw();
  assert.doesNotMatch(p.nodes.enemies.textContent, /Second card/);
  p.state.picked = [2]; p.draw();
  assert.doesNotMatch(p.nodes.enemies.textContent, /Second card/);
  p.view.board.revealedActions = [{ actor: 1, spell: 'one', targets: [2] }]; p.draw();
  assert.match(p.nodes.allies.textContent, /First card→ Creature 2/);
  assert.doesNotMatch(p.nodes.enemies.textContent, /Second card/);
  p.view.board.revealedActions.push({ actor: 2, spell: 'two', targets: [1] }); p.draw();
  assert.match(p.nodes.enemies.textContent, /Second card→ Creature 1/);
  p.view.board.roundNumber = 2; p.view.board.revealedIntents = []; p.view.board.revealedActions = []; p.draw();
  assert.doesNotMatch(p.nodes.enemies.textContent, /Second card/);
});

test('down reaches the evolution explorer after spell choices and up returns to a spell', () => {
  const p = page(); p.view.waitingFor = 'Evolution';
  p.view.options = { evolution: { remainingPicks: 2, creatures: [{ creature: 1, availableTiers: ['tier:one:v1'] }] } }; p.draw();
  const spell = p.nodes.choices.querySelectorAll('[data-focus]').find(node => node.dataset.focus.startsWith('evolve-package-'));
  spell.focus(); p.context.keyboardDecision(p.state, keyEvent('ArrowDown'));
  assert.equal(p.document.activeElement.dataset.focus, 'evolution-explorer');
  const explorer = p.document.activeElement;
  p.context.keyboardDecision(p.state, keyEvent('ArrowUp'));
  assert.equal(p.document.activeElement, spell);
  explorer.click(); assert.equal(p.state.tab, 'mat');
});

test('down from the last intent row reaches its explorer without declaring a card', () => {
  const p = page(); p.draw(); held(p).children.at(-1).focus();
  p.context.keyboardDecision(p.state, keyEvent('ArrowDown'));
  assert.equal(p.document.activeElement, p.nodes['hand-talents']);
  assert.equal(p.state.chosen, null);
  p.context.keyboardDecision(p.state, keyEvent('ArrowUp'));
  assert.equal(p.document.activeElement, held(p).children.at(-1));
});

test('the atlas identifies the inspected creature and the second evolution pick from the new board', () => {
  const p = page(); p.view.waitingFor = 'Evolution';
  p.view.options = { evolution: { remainingPicks: 2, creatures: [{ creature: 1, availableTiers: ['tier:two:v1'] }] } }; p.draw();
  assert.match(p.nodes.mat.children[0].textContent, /Creature 1 · talentsRound 1 · Evolution/);
  p.view.board.evolutionChoices = [{ creature: 1, tier: 'tier:one:v1' }];
  p.view.options.evolution.remainingPicks = 1; p.view.waitingAsked++; p.draw();
  assert.match(p.nodes.mat.children[0].textContent, /1 \/ 2 team picks remaining/);
  assert.match(p.nodes['evolution-budget'].textContent, /1 \/ 2 team picks remaining/);
  assert.match(p.nodes['evolution-budget'].textContent, /✓ Pick 1 · Creature 1/);
  assert.match(p.nodes.mat.children[0].textContent, /Shared team picks/);
});

test('the evolution budget follows the configured team allowance and does not reset when switching creatures', () => {
  const p = page(); p.view.waitingFor = 'Evolution'; p.state.catalogue.rules.evolutionPicksPerOpportunity = 4;
  p.view.board.evolutionChoices = [{ creature: 1, tier: 'tier:one:v1' }];
  p.view.options = { evolution: { remainingPicks: 3, creatures: [{ creature: 1, availableTiers: ['tier:two:v1'] }, { creature: 2, availableTiers: ['tier:one:v1'] }] } }; p.draw();
  assert.match(p.nodes['evolution-budget'].textContent, /3 \/ 4 team picks remaining/);
  p.nodes.choices.children[0].children[1].click();
  assert.match(p.nodes.asking.textContent, /Creature 2/);
  assert.match(p.nodes['evolution-budget'].textContent, /3 \/ 4 team picks remaining/);
});

test('newly acquired packages are summarized together after evolution and shown on the opponent board', () => {
  const p = page(); p.draw();
  p.view.board.subPhase = 'Evolution'; p.view.waitingFor = 'Evolution';
  p.view.options = { evolution: { remainingPicks: 2, creatures: [{ creature: 1, availableTiers: ['tier:one:v1'] }] } };
  p.draw();
  p.view.board.allies[0].acquiredTiers = ['tier:one:v1']; p.draw();
  p.view.board.enemies[0].acquiredTiers = ['tier:two:v1']; p.draw();
  assert.match(p.nodes.enemies.textContent, /Second package/);
  assert.equal(p.nodes.enemies.children[0].querySelectorAll('[data-focus]').length, 0);
  p.view.board.subPhase = 'Speed'; p.view.waitingFor = 'Speed'; p.draw();
  assert.match(p.nodes['phase-notice-title'].textContent, /Packages unlocked/);
  assert.match(p.nodes['phase-notice-changes'].textContent, /Creature 1 · yoursFirst package/);
  assert.match(p.nodes['phase-notice-changes'].textContent, /Creature 2 · opponentSecond package/);
  assert.equal(p.nodes['phase-notice-changes'].children.length, 2);
  p.draw();
  assert.equal(p.nodes['phase-notice-changes'].children.length, 2);
});

test('package recap waits through an automatic phase and appears with the next decision', () => {
  const p = page(); p.draw();
  p.view.board.subPhase = 'Evolution'; p.view.waitingFor = 'Evolution'; p.draw();
  p.view.board.allies[0].acquiredTiers = ['tier:one:v1']; p.draw();
  p.view.board.subPhase = 'EnergyGain'; p.view.waitingFor = null; p.draw();
  assert.doesNotMatch(p.nodes['phase-notice-title'].textContent, /Packages unlocked/);
  p.view.board.subPhase = 'Speed'; p.view.waitingFor = 'Speed'; p.draw();
  assert.match(p.nodes['phase-notice-title'].textContent, /Packages unlocked/);
  assert.match(p.nodes['phase-notice-changes'].textContent, /First package/);
});

test("in hotseat, each seat's package recap names purchases made before the device was handed over", () => {
  const p = page(); p.draw();
  p.view.board.subPhase = 'Evolution'; p.view.waitingFor = 'Evolution';
  p.view.options = { evolution: { remainingPicks: 2, creatures: [{ creature: 1, availableTiers: ['tier:one:v1'] }] } };
  p.draw();
  p.view.board.allies[0].acquiredTiers = ['tier:one:v1']; p.draw();
  p.current.seat = 'player2'; p.view.waitingAsked++; p.draw();
  p.state.holder = 'player2'; p.draw();
  p.view.board.enemies[0].acquiredTiers = ['tier:two:v1']; p.draw();
  p.view.board.subPhase = 'Speed'; p.view.waitingFor = 'Speed'; p.view.waitingAsked++; p.draw();
  assert.match(p.nodes['phase-notice-title'].textContent, /Packages unlocked/);
  assert.equal(p.nodes['phase-notice-changes'].children.length, 2, 'the purchase made before the handover is included');
  p.current.seat = 'player1'; p.view.waitingAsked++; p.draw();
  p.state.holder = 'player1'; p.draw();
  assert.match(p.nodes['phase-notice-title'].textContent, /Packages unlocked/);
  assert.equal(p.nodes['phase-notice-changes'].children.length, 2);
});

test("switching the creature to evolve starts its package list at the top, and a redraw keeps a creature's own scroll", () => {
  const p = page();
  p.view.waitingFor = 'Evolution'; p.view.board.subPhase = 'Evolution';
  p.view.board.allies.push({ id: 3, health: 20, maxHealth: 20, energy: 2 });
  p.view.options = { evolution: { remainingPicks: 2, creatures: [{ creature: 1, availableTiers: ['tier:one:v1'] }, { creature: 3, availableTiers: ['tier:two:v1'] }] } };
  p.draw();
  const list = () => p.nodes.choices.children.find(child => child.className === 'choice-cards');
  list().scrollTop = 140; p.state.rendered = null; p.draw();
  assert.equal(list().scrollTop, 140);
  p.nodes.choices.children[0].children[1].click();
  assert.equal(list().scrollTop, 0);
});

test('upkeep popup distinguishes applied ticks from conditions still active on the current board', () => {
  const p = page(); p.context.setupPhaseControls(p.state); p.draw();
  p.view.board.roundNumber = 2; p.view.board.subPhase = 'Evolution';
  p.view.board.enemies[0].conditions = [{ effect: { kind: 'DefenseModifier', amount: -2 }, remainingRounds: 2 }];
  p.view.roundEvents = [{ sequence: 42, event: { kind: 'OngoingEffectsApplied', roundId: 2,
    regenerationTicks: [{ creature: 1, healed: 1 }], bleedTicks: [{ creature: 2, damage: 2 }] } }];
  p.draw();
  assert.match(p.nodes['phase-notice-context'].textContent, /Upkeep · 1 \/ 3/);
  assert.match(p.nodes['phase-notice-changes'].textContent, /Creature 1\+1 HP/);
  assert.doesNotMatch(p.nodes['phase-notice-changes'].textContent, /Creature 2/);
  assert.equal(p.timers.has(p.state.phaseTimer), false, 'steps wait for the player');
  p.nodes['phase-notice-next'].click();
  assert.match(p.nodes['phase-notice-changes'].textContent, /Creature 2−2 HP/);
  p.nodes['phase-notice-next'].click();
  assert.match(p.nodes['phase-notice-changes'].textContent, /DefenseModifier -2 · 2 rounds left/);
  assert.match(p.nodes['upkeep-conditions'].textContent, /Still active after upkeepCreature 2 · DefenseModifier -2 · 2 rounds left/);
  p.nodes['phase-notice-next'].click();
  assert.equal(p.nodes['phase-notice'].hidden, true);
  p.view.board.enemies[0].conditions = []; p.view.board.roundNumber = 3; p.draw();
  assert.equal(p.nodes['upkeep-conditions'].hidden, true);
});

test('reopening an upkeep announcement restarts the tick sequence', () => {
  const p = page(); p.context.setupPhaseControls(p.state); p.draw();
  p.view.board.roundNumber = 2;
  p.view.roundEvents = [{ sequence: 9, event: { kind: 'OngoingEffectsApplied', roundId: 2,
    regenerationTicks: [{ creature: 1, healed: 2 }], bleedTicks: [{ creature: 2, damage: 1 }] } }];
  p.draw();
  p.nodes['phase-notice-next'].click();
  assert.match(p.nodes['phase-notice-context'].textContent, /2 \/ 2/);
  p.nodes['announcement-list'].children[1].children[0].click();
  assert.match(p.nodes['phase-notice-context'].textContent, /Earlier announcement · Upkeep · 1 \/ 2/);
  assert.match(p.nodes['phase-notice-changes'].textContent, /Creature 1\+2 HP/);
});

test('a round with conditions but no health ticks shows their actual counters in one notice', () => {
  const p = page(); p.draw();
  p.view.board.roundNumber = 2;
  p.view.board.allies[0].conditions = [
    { effect: { kind: 'Ward', amount: 2 }, remainingRounds: 2, isFresh: true },
    { effect: { kind: 'Aura' }, remainingRounds: null },
  ];
  p.view.roundEvents = [{ sequence: 9, event: { kind: 'OngoingEffectsApplied', roundId: 2 } }];
  p.draw();
  assert.match(p.nodes['phase-notice-changes'].textContent, /Ward 2 · new · 2 rounds left/);
  assert.match(p.nodes['phase-notice-changes'].textContent, /Aura · permanent/);
  assert.equal(p.nodes['phase-notice-next'].hidden, true);
});

test('the persistent phase guide distinguishes simultaneous speeds from sequential spell revelation', () => {
  const p = page(); p.view.board.subPhase = 'Speed'; p.draw();
  assert.match(p.nodes['phase-round'].textContent, /Round 1 \/ 16/);
  assert.match(p.nodes['phase-reminder'].textContent, /All speeds reveal together/);
  assert.equal(p.nodes['phase-steps'].children[2].attributes['aria-current'], 'step');
  p.view.board.subPhase = 'Activation'; p.draw();
  assert.match(p.nodes['phase-reminder'].textContent, /chooses targets, and it resolves at once/);
  assert.equal(p.nodes['phase-steps'].children[5].attributes['aria-current'], 'step');
  p.view.board.phase = 'StartOfRound'; p.draw();
  assert.equal(p.nodes['phase-steps'].children[0].attributes['aria-current'], 'step');
});

test('turn order labels distinguish creature identity, play position and initiative', () => {
  const p = page(); p.view.board.timeline = [{ creature: 2, initiative: 8, speed: 'Quick' }, { creature: 1, initiative: 11, speed: 'Standard' }]; p.draw();
  assert.equal(p.nodes.timeline.children[0].children[1].children[0].attributes['aria-label'], 'Turn 1, Creature 2, Quick, initiative 8');
  assert.equal(p.nodes.timeline.children[1].children[1].children[0].attributes['aria-label'], 'Turn 2, Creature 1, Standard, initiative 11');
  assert.match(p.nodes.allies.textContent, /Creature 1/);
  assert.doesNotMatch(p.nodes.allies.textContent, /First/);
});

test('spell faces display host-provided effect cues and the exact critical reminder without guessing from names', () => {
  const p = page();
  p.state.cards.set('one', { id: 'one', name: 'A neutral title', cues: [{ tone: 'harm', label: 'Impact' }, { tone: 'critical', label: 'Critical' }], criticalNote: 'The host critical rule.' }); p.draw();
  assert.match(held(p).children[0].textContent, /↘ Impact/);
  assert.match(held(p).children[0].textContent, /✦ Critical/);
  assert.match(held(p).children[0].textContent, /The host critical rule/);
  assert.doesNotMatch(held(p).children[1].textContent, /✦/);
});

test('desktop turn guidance keeps the battlefield in view alongside planning', () => {
  const p = page(); p.context.innerWidth = 1280; p.context.innerHeight = 720; p.draw();
  assert.equal(p.nodes.board.scrolledIntoView, true);
  assert.equal(p.nodes.planning.scrolledIntoView, undefined);
});

test('the acting creature appears first in the planning spellbook with its context', () => {
  const p = page(); p.view.board.allies.push({ id: 3, name: 'Third', health: 12, maxHealth: 20, energy: 5, knownSpells: ['two'] });
  p.view.waitingFor = 'Speed'; p.view.waitingCreature = 3; p.view.options = { speed: {} }; p.draw();
  assert.match(p.nodes['own-hand'].children[0].children[0].textContent, /Creature 3/);
  assert.match(p.nodes['decision-context'].textContent, /12\/20 HP · 5 energy/);
  assert.equal(p.nodes.asking.textContent, 'Creature 3 · choose speed');
});

test('arrows switch the evolution creature and move into its offered spells without submitting', () => {
  const p = page(); p.view.waitingFor = 'Evolution';
  p.view.options = { evolution: { creatures: [{ creature: 1, availableTiers: ['tier:one:v1'] }, { creature: 3, availableTiers: ['tier:two:v1'] }] } }; p.draw();
  p.context.keyboardDecision(p.state, keyEvent('ArrowRight'));
  assert.equal(p.state.evolving, 3); assert.equal(p.document.activeElement.dataset.focus, 'evolve-3');
  p.context.keyboardDecision(p.state, keyEvent('ArrowDown'));
  assert.equal(p.document.activeElement.dataset.focus, 'evolve-package-3-tier:two:v1'); assert.equal(p.state.sending, false);
  p.context.keyboardDecision(p.state, keyEvent('ArrowUp'));
  assert.equal(p.document.activeElement.dataset.focus, 'evolve-3');
});

test('arrows focus spell and target controls without selecting or casting them', () => {
  const p = page(); p.draw();
  p.context.keyboardDecision(p.state, keyEvent('ArrowRight'));
  assert.equal(p.document.activeElement.dataset.focus, 'card-1-one');
  p.context.keyboardDecision(p.state, keyEvent('ArrowRight'));
  assert.equal(p.document.activeElement.dataset.focus, 'card-1-two');
  assert.equal(p.state.chosen, null); assert.equal(p.state.sending, false);
  p.view.waitingFor = 'Target'; p.view.waitingAsked++;
  p.view.options = { target: { legalTargets: { candidates: [1, 2], minTargets: 1, maxTargets: 1 } } }; p.draw();
  p.context.keyboardDecision(p.state, keyEvent('ArrowRight'));
  assert.match(p.document.activeElement.dataset.focus, /^target-/); assert.equal(p.state.picked.length, 0);
});

test('vertical arrow navigation chooses the closest card in the next visual row', () => {
  const p = page(); const nodes = [new Element(), new Element(), new Element(), new Element()];
  nodes.forEach((node, index) => { node.getBoundingClientRect = () => ({ left: index % 2 * 200, top: Math.floor(index / 2) * 300, width: 180 }); });
  assert.equal(p.context.arrowNeighbour(nodes, nodes[1], 'ArrowDown'), nodes[3]);
  assert.equal(p.context.arrowNeighbour(nodes, nodes[2], 'ArrowUp'), nodes[0]);
});

test('left and right traverse choices when a narrow layout has only one item per row', () => {
  const p = page(); const nodes = [new Element(), new Element()];
  nodes.forEach((node, index) => { node.getBoundingClientRect = () => ({ left: 0, top: index * 150, width: 300 }); });
  assert.equal(p.context.arrowNeighbour(nodes, nodes[0], 'ArrowRight'), nodes[1]);
  assert.equal(p.context.arrowNeighbour(nodes, nodes[1], 'ArrowLeft'), nodes[0]);
});

test('phase notices survive selection redraws, expire once, and announce only real phase changes', () => {
  const p = page(); p.draw();
  const timer = p.state.phaseTimer;
  assert.match(p.nodes['phase-notice-title'].textContent, /Spells/);
  p.state.chosen = 'one'; p.draw(); p.view.waitingAsked++; p.draw();
  assert.equal(p.state.phaseTimer, timer);
  p.timers.get(timer)();
  assert.equal(p.nodes['phase-notice'].hidden, true);
  p.view.waitingAsked++; p.draw();
  assert.equal(p.nodes['phase-notice'].hidden, true);
  p.view.board.subPhase = 'Activation'; p.draw();
  assert.match(p.nodes['phase-notice-title'].textContent, /Combat/);
  assert.equal(p.nodes['phase-notice'].hidden, false);
  assert.equal(p.timers.has(timer), false);
});

test('upkeep remains readable after a skipped automatic phase and after the notice fades', () => {
  const p = page(); p.context.setupPhaseControls(p.state); p.draw();
  p.state.catalogue.rules.energyPerRound = 3;
  p.view.board.roundNumber = 2; p.view.board.subPhase = 'Evolution';
  p.view.roundEvents = [{ sequence: 40, round: 2, event: { kind: 'OngoingEffectsApplied', roundId: 2,
    regenerationTicks: [{ creature: 1, healed: 2 }], bleedTicks: [{ creature: 2, damage: 1 }] } }];
  p.draw();
  assert.match(p.nodes['phase-notice-title'].textContent, /Round 2 begins/);
  assert.equal(p.nodes['phase-notice'].dataset.kind, 'round');
  assert.match(p.nodes['phase-notice-detail'].textContent, /\+3 energy.*Healing over time/);
  assert.match(p.nodes['upkeep-energy'].textContent, /\+3 energy/);
  assert.match(p.nodes['upkeep-effects'].textContent, /Creature 1\+2 HP/);
  assert.match(p.nodes['upkeep-effects'].textContent, /Creature 2−1 HP/);
  p.nodes.upkeep.open = true;
  p.nodes['phase-notice-close'].click(); p.view.waitingAsked++; p.draw();
  assert.equal(p.nodes.upkeep.open, true);
  assert.equal(p.nodes.upkeep.hidden, false);
  p.context.keyboardDecision(p.state, { key: 'Escape', preventDefault() {} });
  assert.equal(p.nodes.upkeep.open, false);
  p.nodes.upkeep.open = true;
  p.view.board.roundNumber = 3; p.draw();
  assert.equal(p.nodes.upkeep.open, false);
  assert.equal(p.nodes.upkeep.hidden, true);
});

test('handover hides and cancels the old notice before the other seat sees the table', () => {
  const p = page(); p.draw(); const timer = p.state.phaseTimer;
  p.current.seat = 'player2'; p.draw();
  assert.equal(p.nodes['phase-notice'].hidden, true);
  assert.equal(p.timers.has(timer), false);
  p.state.holder = 'player2'; p.draw();
  assert.equal(p.nodes['phase-notice'].hidden, false);
});

test('reduced motion keeps the phase announcement without animating it', () => {
  const p = page(); let animated = false;
  p.context.matchMedia = () => ({ matches: true });
  p.nodes['phase-notice'].animate = () => { animated = true; };
  p.draw();
  assert.equal(animated, false);
  assert.equal(p.nodes['phase-notice'].hidden, false);
});

test('spell stats label cast cost and crit without reviving retired spell initiative', () => {
  const p = page(); p.state.cards.set('one', { name: 'Probe', cost: 0, initiative: 2, critical: '35%', criticalThreshold: 14 });
  const parts = p.context.cardParts(p.state, 'one');
  assert.match(parts[0].textContent, /ϟ 0Energy/);
  assert.doesNotMatch(parts[2].textContent, /Initiative|On unlock/);
  assert.match(parts[2].textContent, /Crit chance35%Standard only · d20 14\+/);
  p.state.cards.set('one', { name: 'Unknown stats' });
  const missing = p.context.cardParts(p.state, 'one');
  assert.equal(missing[0].children.length, 1);
  assert.equal(missing[2].children.length, 0);
});

test('targeting names the actual host cursor separately from creature identity in both headers', () => {
  const p = page(); p.view.waitingFor = 'Target'; p.view.board.subPhase = 'Activation';
  p.view.board.timeline = [4, 5, 1, 3, 6, 2].map(creature => ({ creature, speed: 'Standard' }));
  p.view.board.activationCursor = 2;
  p.view.options = { target: { actor: 1, spell: 'two', legalTargets: { candidates: [2], minTargets: 1, maxTargets: 1 } } };
  p.draw();
  assert.equal(p.nodes['decision-phase'].textContent, 'Targeting');
  assert.equal(p.nodes['decision-turn'].textContent, 'Turn 3 of 6 · Standard');
  assert.equal(p.nodes['phase-current'].textContent, 'Combat');
  assert.equal(p.nodes['phase-turn'].textContent, 'Turn 3 of 6 · Creature 1');
  p.view.board.activationCursor = 6; p.draw();
  assert.equal(p.nodes['decision-turn'].hidden, true);
  assert.equal(p.nodes['phase-turn'].textContent, '');
});

test('announcements last longer, pause while reading and can be kept open', () => {
  const p = page(); p.context.setupPhaseControls(p.state); p.draw();
  assert.equal(p.delays.get(p.state.phaseTimer), 6000);
  p.nodes['phase-notice'].events.mouseenter();
  assert.equal(p.timers.has(p.state.phaseTimer), false);
  p.nodes['phase-notice'].events.mouseleave();
  assert.equal(p.timers.has(p.state.phaseTimer), true);
  p.nodes['phase-notice-pin'].click();
  assert.equal(p.state.noticePinned, true);
  assert.equal(p.timers.has(p.state.phaseTimer), false);
  p.nodes['phase-notice'].events.mouseleave();
  assert.equal(p.timers.has(p.state.phaseTimer), false);
  p.nodes['phase-notice-close'].click();
  assert.equal(p.nodes['phase-notice'].hidden, true);
  p.view.board.roundNumber++; p.draw();
  assert.equal(p.delays.get(p.state.phaseTimer), 8000);
});

test('announcement history is bounded, separate per seat, and replays without changing current decisions', () => {
  const p = page(); p.draw(); p.state.chosen = 'one'; p.draw();
  assert.equal(p.state.announcements.get('player1').length, 1);
  p.view.board.subPhase = 'Activation'; p.draw();
  p.nodes['announcement-list'].children[2].children[0].click();
  assert.match(p.nodes['phase-notice-context'].textContent, /Earlier announcement/);
  assert.match(p.nodes['phase-notice-title'].textContent, /Spells/);
  assert.equal(p.nodes['phase-current'].textContent, 'Combat');
  assert.equal(p.state.chosen, 'one');
  assert.equal(p.state.noticePinned, true);
  assert.equal(p.timers.has(p.state.phaseTimer), false);
  for (let round = 2; round <= 16; round++) { p.view.board.roundNumber = round; p.draw(); }
  assert.equal(p.state.announcements.get('player1').length, 12);
  p.current.seat = 'player2'; p.draw();
  assert.equal(p.nodes.table.hidden, true);
  p.state.holder = 'player2'; p.draw();
  assert.equal(p.nodes['announcement-list'].children.length, 2);
  assert.equal(p.state.announcements.get('player1').length, 12);
});

test('own battlefield cards track draft, private declaration, pending targets and public confirmation', () => {
  const p = page(); p.draw(); p.state.chosen = 'one'; p.draw();
  assert.match(p.nodes.allies.textContent, /Not declaredFirst cardNo targets chosen yet/);
  p.view.board.intents = [{ actor: 1, spell: 'one' }, { actor: 2, spell: 'two' }];
  p.view.waitingAsked++; p.view.waitingFor = 'Target'; p.view.board.subPhase = 'Activation';
  p.view.options = { target: { actor: 1, spell: 'one', legalTargets: { candidates: [2], minTargets: 1, maxTargets: 1 } } }; p.draw();
  assert.match(p.nodes.allies.textContent, /Not revealedFirst cardNo targets chosen yet/);
  assert.doesNotMatch(p.nodes.enemies.textContent, /Second card/);
  p.state.picked = [2]; p.draw();
  assert.match(p.nodes.allies.textContent, /Selecting → Creature 2 · not confirmed/);
  p.view.board.revealedActions = [{ actor: 1, spell: 'one', targets: [2] }]; p.draw();
  assert.match(p.nodes.allies.textContent, /RevealedFirst card→ Creature 2/);
  assert.doesNotMatch(p.nodes.allies.textContent, /Not revealed|not confirmed|No targets chosen/);
  p.view.board.roundNumber++; p.view.board.intents = []; p.view.board.revealedActions = []; p.state.chosen = null; p.draw();
  assert.doesNotMatch(p.nodes.allies.textContent, /First card/);
});

function completedRound(round) {
  return [
    { sequence: 10, round, event: { kind: 'CombatActionResolved', roundId: round, resolution: { action: { actor: 1, spell: 'one', targets: [2] }, isCritical: true }, appliedOutcomes: [{ kind: 'DamageOutcome', target: 2, amount: 3 }] } },
    { sequence: 11, round, event: { kind: 'CombatActionResolved', roundId: round, resolution: { action: { actor: 2, spell: 'two', targets: [1] }, fizzled: true, fizzleReason: { message: 'Cannot act.' } }, appliedOutcomes: [] } },
    { sequence: 12, round: round + 1, event: { kind: 'RoundEnded', roundId: round } },
  ];
}

function stepOk(p) {
  p.nodes['combat-step-controls'].children[0].click();
}

function openReplay(p) {
  p.nodes['recap-actions'].children[0].children[0].click();
}

// Each action is shown as it resolves (ADR 0083), so the end of a round no longer plays them back; the recap
// replays them on demand, one at a time, and holds the next question until the player leaves it.
test('a completed round is not replayed on its own, and its recap replays one actual action at a time', async () => {
  const p = page(); const acknowledgements = []; const sent = [];
  p.current.transport.seat = async (_, drawn) => { acknowledgements.push(drawn); return { ok: true }; };
  p.current.transport.decide = async body => { sent.push(body); return { ok: true }; };
  p.draw();
  p.view.board.roundNumber = 2; p.view.waitingAsked = 2; p.view.roundEvents = completedRound(1); p.draw();
  assert.equal(p.state.playback ?? null, null);
  assert.equal(p.nodes.planning.hidden, false);
  // The opponent's last action of the round is read first, and the next question is only acknowledged after it.
  assert.deepEqual(acknowledgements, [1]);
  stepOk(p);
  assert.deepEqual(acknowledgements, [1, 2]);
  openReplay(p);
  assert.equal(p.state.playback.index, 0);
  assert.equal(p.nodes.planning.hidden, true);
  assert.equal(p.nodes.playback.hidden, false);
  assert.equal(p.nodes['phase-current'].textContent, 'Round replay');
  assert.match(p.nodes['playback-action'].textContent, /First cardCritical/);
  assert.match(p.nodes['playback-action'].textContent, /Damage 3 → Creature 2/);
  assert.match(p.nodes.allies.children[0].className, /replay-caster/);
  assert.match(p.nodes.enemies.children[0].className, /replay-target/);
  await p.context.submit(p.state, p.current, { kind: 'Intent', spell: 'one' });
  assert.deepEqual(sent, []);
  p.nodes['playback-controls'].children[1].click();
  assert.match(p.nodes['playback-action'].textContent, /Second cardFizzled/);
  assert.match(p.nodes['playback-action'].textContent, /Cannot act/);
  p.view.feedNext = 100; p.draw();
  assert.equal(p.state.playback.index, 1);
  p.nodes['playback-controls'].children[0].click();
  assert.equal(p.state.playback.index, 0);
  p.nodes['playback-controls'].children[2].click();
  assert.equal(p.state.playback, null);
  assert.equal(p.nodes.planning.hidden, false);
  assert.deepEqual(sent, []);
  p.draw(); assert.equal(p.state.playback, null);
});

test('the action resolved last is shown in the round bar and battlefield until combat moves on', () => {
  const p = page(); p.view.waitingFor = null; p.view.board.subPhase = 'Activation'; p.view.board.activationCursor = 1;
  p.view.board.timeline = [{ creature: 1, speed: 'Standard' }, { creature: 2, speed: 'Standard' }];
  p.view.roundEvents = [completedRound(1)[0]];
  p.view.roundEvents[0].event.frame = { before: [{ ...p.view.board.enemies[0], health: 13 }], after: [], timeline: p.view.board.timeline, rollOffs: [] };
  p.draw();
  assert.equal(p.state.playback ?? null, null);
  assert.match(p.nodes['combat-line'].textContent, /First card/);
  assert.match(p.nodes.allies.children[0].className, /replay-caster/);
  assert.match(p.nodes.enemies.children[0].className, /replay-target/);
  assert.match(p.nodes.enemies.textContent, /HP 13 → 10/);
  p.view.board.subPhase = 'Cleanup'; p.draw();
  assert.equal(p.nodes['combat-line'].hidden, true);
  assert.doesNotMatch(p.nodes.allies.children[0].className, /replay-caster/);
});

test('the next decision keeps the round bar and recap available without another resolved-action box', async () => {
  const p = page(); p.view.waitingFor = 'Intent'; p.view.board.subPhase = 'Activation';
  p.current.transport.decide = async () => ({ ok: true });
  p.view.roundEvents = completedRound(1).slice(0, 2); p.draw();
  stepOk(p);
  assert.match(p.nodes['combat-line'].textContent, /Second card/);
  assert.equal(p.nodes['live-action'], undefined);
  await p.context.submit(p.state, p.current, { kind: 'Intent', spell: 'one' });
  p.draw();
  assert.equal(p.nodes['combat-step'].hidden, true);
});

test('loading an old recap does not auto replay it, but its replay button is available after a skip', () => {
  const p = page(); p.view.roundEvents = completedRound(1); p.view.board.roundNumber = 2; p.draw();
  assert.equal(p.state.playback, undefined);
  p.nodes['recap-actions'].children[0].children[0].click();
  assert.equal(p.state.playback.round, 1);
  assert.match(p.nodes['playback-controls'].textContent, /Skip to round 2/);
  p.context.movePlayback(p.state, 1); p.context.movePlayback(p.state, 1);
  assert.equal(p.state.playback, null);
  assert.equal(p.nodes.playback.hidden, true);
});

function framedRound(p) {
  const entries = completedRound(1);
  const before = [
    { ...p.view.board.allies[0], health: 17, energy: 4, currentInitiative: 8, totalDefense: 1, conditions: [], isAlive: true },
    { ...p.view.board.enemies[0], health: 3, energy: 2, conditions: [], isAlive: true },
  ];
  const after = [{ ...before[0], health: 20, energy: 2 }, { ...before[1], health: 0, isAlive: false }];
  const timeline = [{ creature: 1, owner: 'Player1', speed: 'Standard', initiative: 8 }, { creature: 2, owner: 'Player2', speed: 'Standard', initiative: 5 }];
  entries[0].event.frame = { before, after, timeline, rollOffs: [{ creature: 1, rolls: [19] }] };
  entries[0].event.appliedOutcomes.push({ kind: 'HealOutcome', target: 1, amount: 3, onCaster: true });
  entries[1].event.frame = { before: after, after, timeline, rollOffs: [] };
  return entries;
}

test('recorded frames rewind stats, death, timeline and energy without replacing live state', async () => {
  const p = page(); const sent = []; p.current.transport.decide = async body => { sent.push(body); return { ok: true }; };
  p.draw(); p.view.roundEvents = framedRound(p); p.view.board.roundNumber = 2; p.view.board.allies[0].energy = 6;
  const live = JSON.stringify(p.view.board); p.draw(); openReplay(p);
  assert.equal(p.state.playback.stage, 'before');
  assert.match(p.nodes.allies.textContent, /17\/20 HP/);
  assert.match(p.nodes.enemies.textContent, /3\/20 HP/);
  assert.doesNotMatch(p.nodes['playback-action'].textContent, /Damage 3|Heal 3|Critical/);
  assert.match(p.nodes.timeline.textContent, /d20 19/);
  assert.match(p.nodes['playback-board-note'].textContent, /Before action 1/);
  assert.equal(p.nodes['playback-controls'].children[0].disabled, true);

  p.nodes['playback-controls'].children[1].click();
  assert.equal(p.state.playback.stage, 'after');
  assert.match(p.nodes.allies.textContent, /20\/20 HP/);
  assert.match(p.nodes.allies.textContent, /HP 17 → 20Energy 4 → 2/);
  assert.match(p.nodes.enemies.textContent, /HP 3 → 0/);
  assert.match(p.nodes.enemies.children[0].className, /dead/);
  assert.match(p.nodes['playback-action'].textContent, /Heal 3 · caster/);
  assert.equal(p.nodes['playback-controls'].children[0].disabled, false);
  p.view.feedNext = 100; p.draw(); assert.equal(p.state.playback.stage, 'after');
  p.context.movePlayback(p.state, -1);
  assert.match(p.nodes.enemies.textContent, /3\/20 HP/);
  assert.doesNotMatch(p.nodes.enemies.children[0].className, /dead/);
  p.context.movePlayback(p.state, 1); p.context.movePlayback(p.state, 1);
  assert.equal(p.state.playback.index, 1); assert.equal(p.state.playback.stage, 'before');
  p.context.movePlayback(p.state, -1);
  assert.equal(p.state.playback.index, 0); assert.equal(p.state.playback.stage, 'after');
  p.context.movePlayback(p.state, 1); p.context.movePlayback(p.state, 1);
  assert.match(p.nodes['playback-action'].textContent, /Fizzled/);
  assert.doesNotMatch(p.nodes.allies.textContent, /HP 17 →/);
  await p.context.submit(p.state, p.current, { kind: 'Intent', spell: 'one' });
  assert.equal(sent.length, 0); assert.equal(JSON.stringify(p.view.board), live);
  p.context.movePlayback(p.state, 1);
  assert.equal(p.state.playback, null);
  stepOk(p);
  assert.equal(p.nodes.allies.children[0].children[2].children[0].children[0].textContent, '6');
  assert.equal(p.nodes.timeline.textContent, '');
});

test('replay shows recorded conditions and spellbooks and skip restores the next round', () => {
  const p = page(); p.draw(); const entries = framedRound(p);
  entries[0].event.frame.after[0] = { ...entries[0].event.frame.after[0], isStunned: true, totalDefense: 3,
    conditions: [{ effect: { kind: 'Stun', duration: { rounds: 1 } }, remainingRounds: 1, isFresh: true }], knownSpells: ['one'] };
  p.view.roundEvents = entries; p.view.board.roundNumber = 2; p.draw(); openReplay(p);
  p.context.movePlayback(p.state, 1);
  assert.match(p.nodes.allies.textContent, /stunned/);
  assert.match(p.nodes.allies.textContent, /Defense 1 → 3/);
  assert.doesNotMatch(p.nodes['own-hand'].textContent, /Second card/);
  p.context.keyboardDecision(p.state, keyEvent('ArrowLeft'));
  assert.doesNotMatch(p.nodes.allies.textContent, /stunned/);
  p.context.keyboardDecision(p.state, keyEvent('Escape'));
  assert.equal(p.state.playback, null);
  assert.match(p.nodes['own-hand'].textContent, /Second card/);
});

test('end-of-match replay uses results controls and hotseat fences keep the review hidden', () => {
  const p = page(); p.draw(); p.view.over = true; p.view.waitingFor = null;
  p.view.roundEvents = completedRound(1); p.draw(); openReplay(p);
  assert.match(p.nodes['playback-controls'].textContent, /Skip to results/);
  p.context.movePlayback(p.state, 1);
  assert.match(p.nodes['playback-controls'].textContent, /Match results/);
  p.view.over = false; p.view.waitingFor = 'Intent'; p.current.seat = 'player2'; p.draw();
  assert.equal(p.nodes.table.hidden, true);
  p.state.holder = 'player2'; p.draw();
  assert.equal(p.state.playback, null);
  assert.equal(p.nodes.playback.hidden, true);
});

// The rule line is how a deck and a screen are checked to be the same game (ADR 0054), so a value that changes
// play has to reach it. Two schedules with the same interval and a different first round are two games.
test('the rule line separates schedules that share an interval but not their first opportunity', () => {
  const p = page();
  const rules = { teamSize: 3, energyPerRound: 2, evolutionPicksPerOpportunity: 2, roundCap: 30, criticalMultiplier: 2 };

  const odd = p.context.ruleLine({ contentHash: 'abcdef012345', rules: { ...rules, firstEvolutionRound: 1, evolutionInterval: 2 } });
  const even = p.context.ruleLine({ contentHash: 'abcdef012345', rules: { ...rules, firstEvolutionRound: 2, evolutionInterval: 2 } });
  const each = p.context.ruleLine({ contentHash: 'abcdef012345', rules: { ...rules, firstEvolutionRound: 3, evolutionInterval: 1 } });

  assert.match(odd, /2 picks every 2 rounds from round 1/);
  assert.match(even, /2 picks every 2 rounds from round 2/);
  assert.match(each, /2 picks every round from round 3/);
  assert.notEqual(odd, even);
});

test('a purchased creature leaves the offer list and the atlas cannot buy again during this opportunity', async () => {
  const p = page(); talentFixture(p); const sent = [];
  p.view.waitingFor = 'Evolution'; p.view.waitingCreature = null;
  p.view.options = { evolution: { remainingPicks: 2, creatures: [{ creature: 1, availableTiers: ['tier:one:v1'] }, { creature: 3, availableTiers: ['tier:one:v1'] }] } };
  p.state.inspectClass = 'tier:one:v1';
  p.current.transport.decide = async decision => { sent.push(decision); return { ok: true }; }; p.draw();
  const old = unlocks(p)[0];
  p.view.board.allies[0].acquiredTiers = ['tier:one:v1'];
  p.view.board.evolutionChoices = [{ creature: 1, tier: 'tier:one:v1' }];
  p.view.options.evolution = { remainingPicks: 1, creatures: [{ creature: 3, availableTiers: ['tier:one:v1'] }] };
  p.view.waitingAsked++; p.draw();
  assert.match(p.nodes.asking.textContent, /Creature 3/);
  assert.match(p.nodes['evolution-budget'].textContent, /1 \/ 2 team picks remaining/);
  p.state.inspectCreature = 1; p.state.inspectClass = 'tier:two:v1'; p.draw();
  assert.equal(unlocks(p).length, 0);
  await old.click(); assert.equal(sent.length, 0);
});

test('a capped opportunity and the next evolution round use the host projections', () => {
  const p = page(); p.view.waitingFor = 'Evolution'; p.state.catalogue.rules.evolutionPicksPerOpportunity = 2;
  p.view.options = { evolution: { remainingPicks: 1, creatures: [{ creature: 1, availableTiers: ['tier:one:v1'] }] } }; p.draw();
  assert.match(p.nodes['evolution-budget'].textContent, /1 \/ 1 team picks remaining/);
  p.view.waitingFor = 'Speed'; p.view.board.subPhase = 'Speed'; p.view.board.roundNumber = 2;
  p.view.board.nextEvolutionRound = 3; p.draw();
  assert.match(p.nodes['phase-reminder'].textContent, /Next evolution: round 3/);
});

test('tie order shows rolls, supports arrow focus and number choices, then submits the order exactly once', async () => {
  const p = page(); const sent = []; p.view.waitingFor = 'TieOrder'; p.view.waitingCreature = null;
  p.view.board.subPhase = 'TieOrder';
  p.view.board.timeline = [{ creature: 1, owner: 'player1', initiative: 8, speed: 'Quick' }, { creature: 2, owner: 'player2', initiative: 8, speed: 'Quick' }, { creature: 3, owner: 'player1', initiative: 8, speed: 'Quick' }];
  p.view.board.rollOffs = [{ creature: 1, rolls: [12, 19] }];
  p.view.options = { tieOrder: { ties: [[1, 3]] } };
  p.current.transport.decide = async decision => { sent.push(decision); return { ok: true }; }; p.draw();
  assert.match(p.nodes.timeline.textContent, /d20 12 → 19/);
  assert.equal(p.nodes['phase-current'].textContent, 'Tie order');
  assert.equal(p.nodes['decision-phase'].textContent, 'Order tied creatures');
  p.context.keyboardDecision(p.state, keyEvent('ArrowRight'));
  assert.equal(p.document.activeElement.dataset.focus, 'tie-1');
  await p.context.keyboardDecision(p.state, keyEvent('2'));
  assert.equal(sent.length, 0); assert.deepEqual([...p.state.ordered], [3]);
  assert.match(p.nodes.choices.textContent, /creature 3 → creature 1/);
  await p.context.keyboardDecision(p.state, keyEvent('Enter'));
  assert.equal(sent.length, 1); assert.deepEqual([...sent[0].order], [3, 1]);
  assert.equal(sent[0].kind, 'TieOrder');
});

test('a stale or handed-over tie control cannot submit or change a later question', async () => {
  const p = page(); const sent = []; p.view.waitingFor = 'TieOrder'; p.view.options = { tieOrder: { ties: [[1, 3]] } };
  p.current.transport.decide = async decision => { sent.push(decision); return { ok: true }; }; p.draw();
  const controls = p.nodes.choices.querySelectorAll('[data-focus]');
  p.state.views = [{ ...p.current, view: { ...p.view, waitingAsked: 2 } }]; p.draw();
  await controls.find(node => node.dataset.focus === 'tie-keep').click();
  controls.find(node => node.dataset.focus === 'tie-3').click();
  assert.equal(sent.length, 0); assert.deepEqual([...p.state.ordered], []);
  p.state.holder = null; p.draw();
  await p.context.keyboardDecision(p.state, keyEvent('1'));
  assert.deepEqual([...p.state.ordered], []);
});

// ADR 0083: an opponent's action resolves the moment its owner confirms targets, so several can land between two
// decisions of a person. Each is read before the seat's next question, with the battlefield marking what it did.
function opponentAction(sequence, amount, round = 1) {
  return { sequence, round, event: { kind: 'CombatActionResolved', roundId: round, resolution: { action: { actor: 2, spell: 'two', targets: [1] } },
    appliedOutcomes: [{ kind: 'DamageOutcome', target: 1, amount }] } };
}

test("opponent actions hold the next question, one OK each, and the question is acknowledged after the last", () => {
  const p = page(); const acknowledgements = [];
  p.current.transport.seat = async (_, drawn) => { acknowledgements.push(drawn); return { ok: true }; };
  p.view.waitingFor = 'Target'; p.view.board.subPhase = 'Activation';
  p.view.options = { target: { actor: 1, spell: 'one', legalTargets: { candidates: [2], minTargets: 1, maxTargets: 1 } } };
  p.draw();
  p.view.waitingAsked = 2; p.view.roundEvents = [opponentAction(30, 4), opponentAction(31, 2)]; p.draw();
  assert.equal(p.nodes['combat-step'].hidden, false);
  assert.match(p.nodes['combat-step-count'].textContent, /opponent action · 2 to read/);
  assert.match(p.nodes['combat-step-action'].textContent, /Second card.*Damage 4 → Creature 1/);
  assert.equal(p.nodes['combat-line'].textContent, 'Creature 2 (opponent) · Second card → Creature 1 · Damage 4 → Creature 1');
  assert.equal(p.nodes['combat-line'].dataset.pending, 'true');
  assert.equal(p.nodes.choices.children.length, 0);
  assert.match(p.nodes.enemies.children[0].className, /replay-caster/);
  assert.match(p.nodes.allies.children[0].className, /replay-target/);
  assert.doesNotMatch(p.nodes['mini-enemies'].children[0].className, /legal/);
  p.context.pick(p.state, p.current, 2);
  assert.deepEqual([...p.state.picked], []);
  assert.deepEqual(acknowledgements, [1]);

  p.nodes['combat-step-controls'].children[0].click();
  assert.match(p.nodes['combat-step-action'].textContent, /Damage 2/);
  assert.doesNotMatch(p.nodes['combat-step-count'].textContent, /to read/);
  assert.equal(p.nodes['combat-step-controls'].children[0].textContent, 'OK · your move →');
  p.nodes['combat-step-controls'].children[0].click();
  assert.equal(p.nodes['combat-step'].hidden, true);
  assert.deepEqual(acknowledgements, [1, 2]);
  assert.ok(p.nodes.choices.children.length > 0);
  assert.equal(p.nodes['combat-line'].dataset.pending, 'false');
});

test("the seat's own action is shown in the round bar and never held", () => {
  const p = page(); p.view.waitingFor = null; p.view.board.subPhase = 'Activation';
  p.view.roundEvents = [opponentAction(28, 3), { ...completedRound(1)[0], sequence: 29 }]; p.draw();
  assert.equal(p.nodes['combat-step'].hidden, true, 'an opponent action before the seat acted was read then');
  assert.match(p.nodes['combat-line'].textContent, /^Creature 1 \(yours\) · First card/);
  assert.equal(p.nodes['combat-line'].dataset.pending, 'false');
});

test('Skip all reads past every opponent action, and Enter and Escape step from the keyboard', () => {
  const p = page(); p.draw();
  p.view.roundEvents = [opponentAction(30, 4), opponentAction(31, 2), opponentAction(32, 1)]; p.draw();
  p.context.keyboardDecision(p.state, keyEvent('1'));
  assert.equal(p.state.chosen, null, 'a number never picks a card while an action is read');
  p.context.keyboardDecision(p.state, keyEvent('Enter'));
  assert.match(p.nodes['combat-step-action'].textContent, /Damage 2/);
  assert.match(p.nodes['combat-step-controls'].textContent, /Skip all/);
  p.context.keyboardDecision(p.state, keyEvent('Escape'));
  assert.equal(p.nodes['combat-step'].hidden, true);
  p.view.roundEvents = [...p.view.roundEvents, opponentAction(33, 5), opponentAction(34, 6)]; p.draw();
  p.nodes['combat-step-controls'].children[2].click();
  assert.equal(p.nodes['combat-step'].hidden, true);
});

test('auto OKs the opponent actions of its round after a pause, and ends with the round', () => {
  const p = page(); p.draw();
  p.view.roundEvents = [opponentAction(30, 4), opponentAction(31, 2)]; p.draw();
  p.nodes['combat-step-controls'].children[1].click();
  assert.match(p.nodes['combat-step-action'].textContent, /Damage 2/, 'the action on screen is OK-ed at once');
  assert.match(p.nodes['combat-step-count'].textContent, /auto$/);
  assert.equal(p.nodes['combat-step-controls'].children[1].textContent, 'Stop auto');
  assert.equal(p.delays.get(p.state.stepTimer), 1400);
  p.draw();
  assert.equal(p.timers.size, 2, 'a redraw does not start a second pause');
  p.timers.get(p.state.stepTimer)();
  assert.equal(p.nodes['combat-step'].hidden, true);
  p.view.roundEvents = [...p.view.roundEvents, opponentAction(32, 1)]; p.draw();
  assert.equal(p.state.stepTimerFor, 32, 'a later action of the same round is OK-ed too');
  p.timers.get(p.state.stepTimer)();
  p.view.roundEvents = [...p.view.roundEvents, opponentAction(40, 1, 2)]; p.view.board.roundNumber = 2; p.draw();
  assert.equal(p.nodes['combat-step'].hidden, false);
  assert.equal(p.state.stepTimerFor, null, 'the next round is read by hand again');
});

test('muted pop-ups stay listed, hold for the rest of the match across a reload, and can be turned back on', () => {
  const p = page(); const kept = new Map();
  p.context.storage = { getItem: key => kept.get(key) ?? null, setItem: (key, value) => kept.set(key, value) };
  p.state.quietKey = 'token-1';
  p.context.setupPhaseControls(p.state); p.draw();
  assert.equal(p.nodes['phase-notice'].hidden, false);
  p.nodes['phase-notice-mute'].click();
  assert.equal(p.nodes['phase-notice'].hidden, true);
  p.view.board.subPhase = 'Activation'; p.draw();
  assert.equal(p.nodes['phase-notice'].hidden, true);
  assert.equal(p.state.announcements.get('player1').length, 2);
  assert.match(p.nodes['announcements-label'].textContent, /· muted$/);
  assert.deepEqual([...p.context.quietTables()], ['token-1']);
  p.nodes['announcement-list'].children[1].children[0].click();
  assert.equal(p.nodes['phase-notice'].hidden, false, 'an earlier announcement still opens when asked for');
  const toggle = p.nodes['announcement-list'].children[0].children[0];
  assert.equal(toggle.attributes['aria-pressed'], 'true');
  toggle.click();
  assert.equal(p.state.quiet, false);
  assert.deepEqual([...p.context.quietTables()], []);
  p.view.board.roundNumber = 2; p.draw();
  assert.equal(p.nodes['phase-notice'].hidden, false);
});

test('revealed speeds open the turn order once a round, opponent first, even with the phase pop-ups muted', () => {
  const p = page(); p.view.board.slot = 'Player1';
  p.view.board.timeline = [{ creature: 2, owner: 'Player2', speed: 'Quick', initiative: 9 }, { creature: 1, owner: 'Player1', speed: 'Standard', initiative: 7 }];
  p.draw();
  assert.equal(p.nodes.order.hidden, false);
  assert.equal(p.nodes.order.open, true);
  assert.equal(p.nodes['order-theirs'].textContent, 'Opponent: Creature 2 Quick');
  assert.equal(p.nodes['order-mine'].textContent, 'You: Creature 1 Standard');
  assert.equal(p.nodes['order-list'].children[0].textContent, '1Creature 2opponent · Quick · initiative 9');
  p.nodes.order.open = false; p.view.waitingAsked++; p.draw();
  assert.equal(p.nodes.order.open, false);
  p.view.board.roundNumber = 2; p.draw();
  assert.equal(p.nodes.order.open, true);
  p.nodes.order.open = false; p.state.quiet = true; p.view.board.roundNumber = 3; p.draw();
  assert.equal(p.nodes.order.open, true, 'the mute is for phase explanations only');
  p.view.board.timeline = []; p.draw();
  assert.equal(p.nodes.order.hidden, true);
});

test('choosing a spell shows where its creature acts, in the heading and on its spellbook row', () => {
  const p = page(); p.view.board.timeline = [{ creature: 2, speed: 'Quick' }, { creature: 1, speed: 'Standard' }]; p.draw();
  assert.equal(p.nodes['decision-turn'].textContent, 'Acts 2 of 2 · Standard');
  assert.equal(p.nodes['decision-turn'].hidden, false);
  assert.match(p.nodes['own-hand'].textContent, /Creature 1 · acts 2 of 2 · choose a card/);
});

test('stopping auto cancels the pause already running, and auto never carries over to the other seat', () => {
  const p = page(); p.draw();
  p.view.roundEvents = [opponentAction(30, 4), opponentAction(31, 2), opponentAction(32, 1)]; p.draw();
  p.nodes['combat-step-controls'].children[1].click();
  const pending = p.state.stepTimer;
  assert.equal(p.timers.has(pending), true);
  p.nodes['combat-step-controls'].children[1].click();
  assert.equal(p.timers.has(pending), false, 'Stop auto cancels the pending OK');
  assert.match(p.nodes['combat-step-action'].textContent, /Damage 2/);

  p.nodes['combat-step-controls'].children[1].click();
  assert.equal(p.state.stepAuto.seat, 'player1');
  p.current.seat = 'player2'; p.state.holder = 'player2'; p.draw();
  assert.equal(p.nodes['combat-step'].hidden, false);
  assert.equal(p.state.stepTimerFor, null, "the other seat's actions wait for its own OK");
  assert.doesNotMatch(p.nodes['combat-step-count'].textContent, /auto/);
});

test('the turn order closes itself, closes on a tap, and waits out a tie order question', () => {
  const p = page(); p.context.setupPhaseControls(p.state); p.view.board.slot = 'Player1';
  p.view.board.timeline = [{ creature: 2, owner: 'Player2', speed: 'Quick', initiative: 9 }, { creature: 1, owner: 'Player1', speed: 'Quick', initiative: 9 }];
  p.view.waitingFor = 'TieOrder'; p.view.options = { tieOrder: { ties: [[1]] } }; p.draw();
  assert.notEqual(p.nodes.order.open, true, 'the tie order buttons stay free');
  p.view.waitingFor = 'Intent'; p.view.waitingAsked++; p.view.options = { intent: { creatures: [{ creature: 1, castableSpells: ['one'] }] } }; p.draw();
  assert.equal(p.nodes.order.open, true);
  assert.equal(p.delays.get(p.state.orderTimer), 9000);
  p.timers.get(p.state.orderTimer)();
  assert.equal(p.nodes.order.open, false);
  p.nodes.order.open = true;
  p.nodes['order-panel'].click();
  assert.equal(p.nodes.order.open, false);
});

test("an opponent action being read heads the sheet with its own turn, and names the seat's move as next", () => {
  const p = page(); p.view.waitingFor = 'Target'; p.view.board.subPhase = 'Activation';
  p.view.board.timeline = [4, 5, 2, 3, 6, 1].map(creature => ({ creature, speed: 'Standard' }));
  p.view.board.activationCursor = 5;
  p.view.options = { target: { actor: 1, spell: 'one', legalTargets: { candidates: [2], minTargets: 1, maxTargets: 1 } } };
  p.draw();
  p.view.waitingAsked = 2; p.view.roundEvents = [opponentAction(30, 4)]; p.draw();
  assert.equal(p.nodes['decision-phase'].textContent, "Opponent's turn");
  assert.equal(p.nodes['decision-turn'].textContent, 'Turn 3 of 6');
  assert.equal(p.nodes.asking.textContent, 'Creature 2 · Second card');
  assert.equal(p.nodes['decision-context'].textContent, 'Then your move: Creature 1 · First card (turn 6 of 6).');
  assert.equal(p.nodes['phase-turn'].textContent, 'Turn 3 of 6 · Creature 2');
  stepOk(p);
  assert.equal(p.nodes['decision-phase'].textContent, 'Targeting');
  assert.equal(p.nodes['decision-turn'].textContent, 'Turn 6 of 6 · Standard');
});

test("a round's last opponent action read in the next round leaves the round bar on the live round", () => {
  const p = page(); p.view.board.timeline = [{ creature: 2, speed: 'Standard' }, { creature: 1, speed: 'Standard' }]; p.draw();
  p.view.board.roundNumber = 2; p.view.waitingAsked = 2;
  p.view.roundEvents = [opponentAction(30, 4)]; p.draw();
  assert.equal(p.nodes['combat-step'].hidden, false);
  assert.equal(p.nodes['phase-current'].textContent, 'Spells');
  assert.equal(p.nodes['phase-turn'].textContent, '');
  p.view.over = true; p.draw();
  assert.equal(p.nodes['phase-turn'].textContent, '');
});

test("the next creature's speed or spell question starts its spellbook at the top", () => {
  const p = page(); p.draw();
  p.nodes['hand-section'].scrollTop = 320; p.nodes.planning.scrollTop = 40;
  p.state.chosen = 'one'; p.draw();
  assert.equal(p.nodes['hand-section'].scrollTop, 320, 'a redraw of the same question keeps the scroll');
  p.view.waitingCreature = 2; p.view.waitingAsked++; p.draw();
  assert.equal(p.nodes['hand-section'].scrollTop, 0);
  assert.equal(p.nodes.planning.scrollTop, 0);
});
