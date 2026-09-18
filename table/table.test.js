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
  getBoundingClientRect() { return { top: 1200, bottom: 1460, left: 0, right: 600, height: 260 }; }
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
    document, URLSearchParams, console, innerHeight: 800, location: { search: '' }, setInterval: () => {},
  });
  const script = readFileSync(new URL('./table.js', import.meta.url), 'utf8').replace(/^import .*;\n/gm, '');
  vm.runInContext(script, context);
  const cards = new Map([['one', { id: 'one', name: 'First card', cost: 2 }], ['two', { id: 'two', name: 'Second card', cost: 3 }]]);
  const state = { views: [], rendered: null, revision: 0, polling: false, error: '', evolving: null, seats: [], holder: 'player1', shown: null,
    acknowledged: null, announced: null, asked: null, sending: false, picked: [], chosen: null, cards,
    catalogue: { cards: [...cards.values()], rules: { roundCap: 16 } }, tab: 'board', feeds: new Map(),
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

test('evolution keeps every creature accessible without mixing their unlock buttons', () => {
  const p = page(); p.view.waitingFor = 'Evolution';
  p.view.options = { evolution: { remainingPicks: 2, creatures: [{ creature: 1, unlockableSpells: ['one'] }, { creature: 3, unlockableSpells: ['two'] }] } }; p.draw();
  assert.match(p.nodes.choices.textContent, /First card/);
  assert.doesNotMatch(p.nodes.choices.textContent, /Second card/);
  p.nodes.choices.children[0].children[1].events.click();
  assert.match(p.nodes.choices.textContent, /Second card/);
  assert.doesNotMatch(p.nodes.choices.textContent, /First card/);
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

test('speed selection opens the acting spellbook as readable reference without offering a cast', () => {
  const p = page(); p.view.waitingFor = 'Speed'; p.view.options = { speed: { missing: [1] } }; p.draw();
  const row = p.nodes['own-hand'].children[0].children[0];
  assert.match(row.className, /active/);
  assert.match(row.textContent, /choose speed/);
  assert.match(held(p).children[0].className, /reference/);
  assert.equal(held(p).children[0].events.click, undefined);
  assert.equal(p.nodes.planning.scrolledIntoView, true);
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

test('tapping a selected card declares that card once with the asking identity', async () => {
  const p = page(); const sent = [];
  p.current.transport.decide = async decision => { sent.push(decision); return { ok: true }; }; p.draw();
  held(p).children[0].events.click(); assert.equal(sent.length, 0);
  await held(p).children[0].events.click();
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
  const pending = held(p).children[0].events.click(); await started;
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
  p.state.cards.get('one').creatureClass = 'North'; p.state.cards.get('one').tier = 1;
  p.state.cards.get('two').creatureClass = 'South'; p.state.cards.get('two').tier = 3;
  p.state.cards.get('two').requires = 'First card';
  p.state.catalogue.trees = [{ name: 'Branch', spells: ['one', 'two'] }];
  p.view.board.allies[0].knownSpells = ['one'];
  p.view.board.allies.push({ id: 3, knownSpells: ['two'] });
}

test('the talent reference follows the inspected creature and the class filter immediately', () => {
  const p = page(); talentFixture(p); p.state.inspectClass = 'South'; p.draw();
  const toolbar = p.nodes.mat.children[0];
  const south = p.nodes.mat.children[2];
  assert.match(south.textContent, /SouthTier 3○ Not learned/);
  assert.match(south.textContent, /Requires: First card/);
  toolbar.children[2].children[1].events.click();
  assert.match(p.nodes.mat.children[2].textContent, /✓ Known/);
  const filter = p.nodes.mat.children[0].children[3];
  filter.value = 'North'; filter.events.change();
  assert.match(p.nodes.mat.children[2].textContent, /North/);
  const again = p.nodes.mat.children[0].children[3]; again.value = 'South'; again.events.change();
  assert.equal(p.nodes.mat.children.length, 3);
  assert.match(p.nodes.mat.children[2].textContent, /South/);
  assert.doesNotMatch(p.nodes.mat.children[2].textContent, /North/);
});

test('the talent inspector uses the selected evolution creature and offers only its legal unlocks', () => {
  const p = page(); talentFixture(p); p.view.waitingFor = 'Evolution';
  p.view.options = { evolution: { creatures: [{ creature: 1, unlockableSpells: ['two'] }, { creature: 3, unlockableSpells: [] }] } };
  p.state.inspectClass = 'South'; p.draw(); assert.match(p.nodes.mat.children[2].textContent, /Unlock now/);
  assert.equal(p.nodes.mat.children[2].querySelectorAll('button').length, 1);
  p.nodes.choices.children[0].children[1].events.click();
  assert.match(p.nodes.mat.children[2].textContent, /✓ Known/);
  assert.doesNotMatch(p.nodes.mat.textContent, /Unlock now/);
  assert.equal(p.nodes.mat.children[2].querySelectorAll('button').length, 0);
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
  p.view.board.subPhase = 'RevealAndTarget'; p.view.board.revealCursor = 1; p.draw();
  assert.match(p.nodes.enemies.textContent, /Turn 1/); assert.match(p.nodes.allies.textContent, /Turn 2/);
  const number = p.nodes.allies.children[0].children[3].children[0].children[0];
  assert.match(number.className, /now/); assert.equal(number.attributes['aria-label'], 'Acts 2 of 2');
  p.view.board.timeline = []; p.draw(); assert.doesNotMatch(p.nodes.allies.textContent, /Turn/);
});

test('the initial talent preview follows the first offered evolution creature even if an earlier ally is absent from options', () => {
  const p = page(); talentFixture(p); p.view.waitingFor = 'Evolution'; p.view.waitingCreature = null;
  p.view.options = { evolution: { creatures: [{ creature: 3, unlockableSpells: ['one'] }] } }; p.draw();
  const filter = p.nodes.mat.children[0].children[3]; filter.value = 'North'; filter.events.change();
  assert.match(p.nodes.mat.children[2].textContent, /Unlock now/);
  p.state.inspectClass = 'South'; p.draw();
  assert.match(p.nodes.mat.children[2].textContent, /✓ Known/);
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
  assert.match(p.nodes.enemies.textContent, /Round 1 · RevealedFirst card→ First #1/);
  p.view.board.roundNumber = 2; p.view.board.revealedActions = []; p.view.board.timeline = [];
  p.view.roundEvents = [{ round: 2, event: { kind: 'CombatActionResolved', roundId: 1, resolution: { action: { actor: 2, spell: 'one', targets: [1] } } } }]; p.draw();
  assert.match(p.nodes.enemies.textContent, /Round 2 · Hidden until revealLast round \(1\): First card/);
  assert.doesNotMatch(p.nodes.enemies.textContent, /Speed · Quick/);
});

test('an atlas unlock uses the guarded current asking and rejects a stale inspector control', async () => {
  const p = page(); talentFixture(p); const sent = []; p.view.waitingFor = 'Evolution';
  p.view.options = { evolution: { creatures: [{ creature: 1, unlockableSpells: ['two'] }] } };
  p.current.transport.decide = async decision => { sent.push(decision); return { ok: true }; };
  p.state.inspectClass = 'South'; p.draw();
  const unlock = p.nodes.mat.children[2].querySelectorAll('button')[0];
  await unlock.events.click(); assert.equal(sent.length, 1); assert.equal(sent[0].creature, 1); assert.equal(sent[0].spell, 'two');
  p.state.views = [{ ...p.current, view: { ...p.view, waitingAsked: 2 } }]; p.draw();
  await unlock.events.click(); assert.equal(sent.length, 1);
});

test('the acting creature appears first in the planning spellbook with its context', () => {
  const p = page(); p.view.board.allies.push({ id: 3, name: 'Third', health: 12, maxHealth: 20, energy: 5, knownSpells: ['two'] });
  p.view.waitingFor = 'Speed'; p.view.waitingCreature = 3; p.view.options = { speed: {} }; p.draw();
  assert.match(p.nodes['own-hand'].children[0].children[0].textContent, /Creature 3/);
  assert.match(p.nodes['decision-context'].textContent, /Third #3 · 12\/20 HP · 5 energy/);
  assert.equal(p.nodes.asking.textContent, 'Choose your speed');
});

test('arrows switch the evolution creature and move into its offered spells without submitting', () => {
  const p = page(); p.view.waitingFor = 'Evolution';
  p.view.options = { evolution: { creatures: [{ creature: 1, unlockableSpells: ['one'] }, { creature: 3, unlockableSpells: ['two'] }] } }; p.draw();
  p.context.keyboardDecision(p.state, keyEvent('ArrowRight'));
  assert.equal(p.state.evolving, 3); assert.equal(p.document.activeElement.dataset.focus, 'evolve-3');
  p.context.keyboardDecision(p.state, keyEvent('ArrowDown'));
  assert.equal(p.document.activeElement.dataset.focus, 'evolve-spell-3-two'); assert.equal(p.state.sending, false);
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
