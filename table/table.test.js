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

// A small DOM double exercises the shipped page without adding a browser dependency to the Node gate.
class Element {
  constructor(tag = 'div') {
    this.tagName = tag;
    this.children = [];
    this.dataset = {};
    this.attributes = {};
    this.events = {};
    this.style = {};
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
  addEventListener(key, action) { this.events[key] = action; }
  focus() { this.owner.activeElement = this; }
  scrollIntoView() { this.scrolledIntoView = true; }
  querySelectorAll(selector) {
    return this.children.flatMap(child => [
      ...(selector === 'button' ? child.tagName === 'button' : selector === '[data-focus]' ? child.dataset.focus : child.dataset.scroll) ? [child] : [],
      ...child.querySelectorAll(selector),
    ]);
  }
}

function page() {
  const ids = [...readFileSync(new URL('./index.html', import.meta.url), 'utf8').matchAll(/id="([^"]+)"/g)].map(match => match[1]);
  const nodes = Object.fromEntries(ids.map(id => [id, new Element()]));
  const document = {
    activeElement: null,
    getElementById: id => nodes[id],
    createElement: tag => Object.assign(new Element(tag), { owner: document }),
    querySelectorAll: selector => Object.values(nodes).flatMap(node => node.querySelectorAll(selector)),
  };
  for (const node of Object.values(nodes)) node.owner = document;
  const context = vm.createContext({ ...transport, ...seats, ...session, ...card, ...board, ...hand, ...feed, ...timeline, ...mat, ...notes,
    document, URLSearchParams, console, location: { search: '' }, setInterval: () => {},
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
  return { context, document, nodes, state, view, current, draw: () => context.render(state, state.views) };
}

function held(p) { return p.nodes['own-hand'].children[0].children[0].children[1]; }

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

test('evolution keeps every creature accessible without mixing their package buttons', () => {
  const p = page(); p.view.waitingFor = 'Evolution';
  p.view.options = { evolution: { remainingPicks: 2, creatures: [{ creature: 1, availableTiers: ['tier:one:v1'] }, { creature: 3, availableTiers: ['tier:two:v1'] }] } }; p.draw();
  assert.match(p.nodes.choices.textContent, /First package/);
  assert.doesNotMatch(p.nodes.choices.textContent, /Second package/);
  p.nodes.choices.children[0].children[1].events.click();
  assert.match(p.nodes.choices.textContent, /Second package/);
  assert.doesNotMatch(p.nodes.choices.textContent, /First package/);
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
  assert.equal(p.nodes.asking.textContent, 'Second card');
  assert.match(p.nodes.choices.children[0].textContent, /Choose targets · Creature 1/);
});

test('a target spell missing from the catalogue still identifies the cast with no legal target', () => {
  const p = page(); p.view.waitingFor = 'Target';
  p.view.options = { target: { actor: 1, spell: 'unknown-card', legalTargets: { candidates: [], minTargets: 1, maxTargets: 1 } } };
  p.draw();
  assert.equal(p.nodes.asking.textContent, 'unknown-card');
  assert.match(p.nodes.choices.children.at(-1).textContent, /No legal target/);
});


test('a completed round opens its recap once, preserves collapse and opens the next recap', () => {
  const p = page();
  p.view.roundEvents = [{ sequence: 1, round: 2, event: { kind: 'RoundEnded', roundId: 1 } }];
  p.view.board.roundNumber = 2; p.draw();
  assert.equal(p.nodes.recap.hidden, false);
  assert.equal(p.nodes.recap.open, true);
  assert.equal(p.nodes.recap.scrolledIntoView, true);
  assert.equal(p.nodes['recap-title'].textContent, 'Round 1 recap');
  p.nodes.recap.open = false;
  p.state.chosen = 'one'; p.draw();
  assert.equal(p.nodes.recap.open, false);
  p.view.roundEvents.push({ sequence: 2, round: 3, event: { kind: 'RoundEnded', roundId: 2 } });
  p.view.board.roundNumber = 3; p.draw();
  assert.equal(p.nodes.recap.open, true);
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
  assert.equal(p.nodes['recap-actions'].children.length, 70);
  assert.match(p.nodes['recap-actions'].textContent, /First card/);
});
