import type Phaser from 'phaser';
import { BattleScene } from './scene';
import { bootScene } from './boot';
import type { SceneFrame } from './types';
import art from '../../data/presentation/spell-art.json';
import spells from 'virtual:bench-spells';
import { benchCreatures, benchDefaultTargets, benchDuration, benchEntries, benchFrame, benchSamples, benchStage, benchTitle, filmInterval,
  filmTitle, seededRandom, benchRound, BENCH_OUTCOMES, BENCH_ROUNDS, FILM_TICK_MS } from '../../table/battle-bench.js';

// The effects bench: every authored spell look, played on chosen creatures of the laboratory's fixture, with no
// host and no rule. Its film clock is what `scripts/battle/filmstrip.mjs` steps to draw a contact sheet.

type Stage = 'decision' | 'windup' | 'impact' | 'resolved';
interface FilmOptions { spell?: string; actor?: string; targets?: string[]; speed?: number; outcome?: string; wound?: boolean; damage?: number | null; defeat?: boolean; round?: number; still?: boolean }

const $ = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;
function make<T extends HTMLElement = HTMLElement>(tag: string, properties: Record<string, unknown> = {}, ...children: (Node | string)[]) {
  const node = Object.assign(document.createElement(tag), properties) as T;
  node.append(...children);
  return node;
}

const lab = $('lab');
const motion = matchMedia('(prefers-reduced-motion: reduce)');
const entries = benchEntries(art, spells);
// The kit's sample stagings, one a primitive: listed after the spells, and never counted among them.
const samples = benchSamples();
const listed = [...entries, ...samples];
const creatures = benchCreatures();
const scene = new BattleScene(new URL('battle-art/', document.baseURI).href);
const reduced = () => motion.matches || $<HTMLInputElement>('reduce-motion').checked;
const speed = () => Number($<HTMLSelectElement>('playback-speed').value);

// One seed for every play: a film of an effect is the same film each time.
const SEED = 0x0b5e55ed;
// How long the scene runs at rest before a film starts, so whatever was in flight has ended.
const SETTLE_TICKS = 25;
const realNow = Date.now, realRandom = Math.random;

let stage: Stage = 'decision';
let elapsed = 0;
let last = 0;
let ready = false;
let game: Phaser.Game | null = null;
// While a film runs the game's loop is stopped: this clock is the only one, and it moves when it is told to.
let film: { clock: number; played: number } | null = null;
let stillFilm = false;
let shownRound = 1;

const spellSelect = make<HTMLSelectElement>('select', { id: 'bench-spell' });
const option = (one: typeof listed[number]) => make('option', { value: one.id }, benchTitle(one));
spellSelect.append(make('optgroup', { label: 'Spells' }, ...entries.map(option)), make('optgroup', { label: 'The effects kit' }, ...samples.map(option)));
const actorSelect = make<HTMLSelectElement>('select', { id: 'bench-actor' });
const targetList = make('div', { id: 'bench-targets', className: 'bench-targets' });
const side = (enemy: boolean) => enemy ? 'opponent' : 'yours';
for (const one of creatures) {
  actorSelect.append(make('option', { value: one.id }, `${one.name} · ${side(one.enemy)}`));
  const box = make('input', { type: 'checkbox', value: one.id });
  box.addEventListener('change', rest);
  targetList.append(make('label', { title: `${one.name} · ${side(one.enemy)}` }, box, make('span', {}, one.name)));
}
// What the play does, each a fixture's choice and no rule: the spell played or fizzled, and its caster paying in
// blood. The live page draws no outcome with a fizzle, so a fizzle takes no wound.
const OUTCOME_WORDS: Record<string, string> = { played: 'Played', fizzled: 'Fizzled' };
const outcomeSelect = make<HTMLSelectElement>('select', { id: 'bench-outcome' }, ...BENCH_OUTCOMES.map(one => make('option', { value: one }, OUTCOME_WORDS[one])));
const woundBox = make<HTMLInputElement>('input', { id: 'bench-wound', type: 'checkbox' });
// The weight of the blow and a death, the fixture's word too: the health each creature struck loses (empty: not
// said, as an action the host served no frames for), and whether the first one struck is defeated by it.
const damageInput = make<HTMLInputElement>('input', { id: 'bench-damage', type: 'number', min: '0', max: '30', step: '1', inputMode: 'numeric', placeholder: 'not said' });
const defeatBox = make<HTMLInputElement>('input', { id: 'bench-defeat', type: 'checkbox' });
// The round the field stands in: the ground gives way as the rounds pass, and the fixture says which one is seen.
const roundInput = make<HTMLInputElement>('input', { id: 'bench-round', type: 'number', min: String(BENCH_ROUNDS.first), max: String(BENCH_ROUNDS.last), step: '1', inputMode: 'numeric', value: '1' });
const playButton = make<HTMLButtonElement>('button', { id: 'bench-play', type: 'button', className: 'primary', disabled: true }, 'Play effect');
const status = make('p', { id: 'bench-status' }, 'Preparing the scene…');
status.setAttribute('role', 'status');
const legend = make('legend', {}, 'Targets · yours above, the opponent below');
const bench = make('section', { id: 'bench' },
  make('label', { className: 'bench-field' }, make('span', {}, 'Effect'), spellSelect),
  make('label', { className: 'bench-field' }, make('span', {}, 'Caster'), actorSelect),
  make('fieldset', { className: 'bench-field' }, legend, targetList),
  make('div', { className: 'bench-outcome' },
    make('label', { className: 'bench-field' }, make('span', {}, 'Outcome'), outcomeSelect),
    make('label', { className: 'bench-wound' }, woundBox, make('span', {}, 'The caster pays in blood'))),
  make('div', { className: 'bench-outcome' },
    make('label', { className: 'bench-field' }, make('span', {}, 'Health lost'), damageInput),
    make('label', { className: 'bench-wound' }, defeatBox, make('span', {}, 'The blow defeats its target'))),
  make('div', { className: 'bench-outcome' },
    make('label', { className: 'bench-field' }, make('span', {}, 'Round'), roundInput),
    make('p', { className: 'bench-note' }, 'The ground as it stands at that round')),
  make('div', { className: 'bench-run' }, playButton, status));
bench.setAttribute('aria-label', 'Effects bench');
lab.dataset.bench = 'true';
lab.dataset.stage = stage;
lab.append(bench);
// The sample action's controls have no part here; sound is the sample's, and no effect plays one (ADR 0134).
$('fullscreen').hidden = true;
$('sound').closest('label')!.hidden = true;
$('reset').title = 'Stop the effect';
$('reset').setAttribute('aria-label', 'Stop the effect');

const boxes = () => [...targetList.querySelectorAll<HTMLInputElement>('input')];
const chosen = () => ({
  entry: listed.find(one => one.id === spellSelect.value) ?? entries[0],
  actor: actorSelect.value,
  targets: boxes().filter(box => box.checked).map(box => box.value),
  outcome: outcomeSelect.value,
  wound: woundBox.checked && !woundBox.disabled,
  damage: damageInput.value === '' || damageInput.disabled ? undefined : Math.max(0, Math.round(Number(damageInput.value)) || 0),
  defeat: defeatBox.checked && !defeatBox.disabled,
  round: benchRound(roundInput.value),
});
// One of the bench's lists set to what was asked, or refused when the bench does not list it.
function pick(select: HTMLSelectElement, asked: string | undefined, known: readonly { id: string }[], what: string) {
  if (asked === undefined) return;
  if (!known.some(one => one.id === asked)) throw new Error(`The bench has no ${what} named ${asked}.`);
  select.value = asked;
}
function choose({ spell, actor, targets, outcome, wound, damage, defeat, round }: FilmOptions) {
  if (round !== undefined) roundInput.value = String(benchRound(round));
  if (outcome !== undefined) {
    if (!(BENCH_OUTCOMES as readonly string[]).includes(outcome)) throw new Error(`The bench has no outcome ${outcome}.`);
    outcomeSelect.value = outcome;
  }
  if (wound !== undefined) woundBox.checked = wound;
  // A film says both each time (null: not said), so a sheet never inherits the last one's.
  if (damage !== undefined) damageInput.value = damage === null ? '' : String(damage);
  if (defeat !== undefined) defeatBox.checked = defeat;
  woundBox.disabled = damageInput.disabled = defeatBox.disabled = outcomeSelect.value === 'fizzled';
  pick(spellSelect, spell, listed, 'effect');
  pick(actorSelect, actor, creatures, 'creature');
  const picked = targets ?? (actor === undefined ? undefined : benchDefaultTargets(actor));
  if (picked) for (const box of boxes()) box.checked = picked.includes(box.value);
}

function show(next: Stage) {
  stage = next;
  const { entry, actor, targets, outcome, wound, damage, defeat, round } = chosen();
  lab.dataset.stage = stage;
  lab.dataset.spell = entry.id;
  lab.dataset.outcome = outcome;
  lab.dataset.wound = String(wound);
  lab.dataset.motion = reduced() ? 'reduced' : 'full';
  playButton.disabled = !ready;
  if (ready) status.textContent = unknown && stage === 'decision' ? `No effect named ${asked}` : describe(entry.name);
  // A change of round is the fixture's, not a round played: the ground is shown as it stands there, at once. Seen
  // giving way, a film that started one round on would catch the fall wherever the wall clock had left it.
  const moved = round !== shownRound;
  shownRound = round;
  scene.present(benchFrame({ entry, actor, targets, stage, outcome, wound, damage, defeat, round }) as SceneFrame, reduced(), speed(), moved);
}
const playing = () => stage === 'windup' || stage === 'impact';
function describe(name: string) {
  if (stage === 'windup') return `${name}: windup`;
  if (stage === 'impact') return `${name}: impact`;
  if (stage === 'resolved') return reduced() ? `${name}: less motion, settled frame only` : `${name}: ${chosen().outcome}`;
  return 'Choose an effect and its targets';
}

// Phaser's particles and camera shake draw from `Math.random`; pinned for the length of a play, then given back.
function rest() {
  Math.random = realRandom;
  elapsed = 0;
  show('decision');
}
function play() {
  if (!ready) return;
  rest();
  if (reduced()) { show('impact'); show('resolved'); return; }
  Math.random = seededRandom(SEED);
  last = performance.now();
  show('windup');
}
// A film moves the play by the steps it is given. On the page's own loop the play follows the wall clock, as
// Phaser's tweens do: counting frames would play the stages in slow motion on a slow renderer.
function advance(delta: number) {
  if (!playing()) return;
  const now = performance.now();
  elapsed += film ? delta : now - last;
  last = now;
  const next = benchStage(elapsed, speed());
  if (next === stage) return;
  if (next === 'resolved') Math.random = realRandom;
  show(next);
}

// Phaser's tweens keep their own clock, read from `Date.now`, beside the one the game's step hands the scene.
// Whenever `Date.now` changes hands they start again from it, or they would wait for it to catch up.
function restartTweenClock() {
  const tweens = scene.tweens;
  tweens.startTime = Date.now();
  tweens.prevTime = tweens.startTime;
  tweens.nextTime = tweens.gap;
  tweens.time = 0;
}
function tick(delta = FILM_TICK_MS) {
  if (!film || !game) return;
  film.clock += delta;
  film.played += delta;
  game.step(film.clock, delta);
  lab.dataset.film = String(film.played);
}
const filmClock = {
  // Stop the loop, pin the clocks, let the scene come to rest, then show the first frame of the play.
  start(options: FilmOptions = {}) {
    if (!ready || !game) throw new Error('The bench is not ready.');
    filmClock.stop();
    choose(options);
    if (options.speed !== undefined) {
      const select = $<HTMLSelectElement>('playback-speed');
      select.value = String(options.speed);
      // A speed the page does not offer leaves the select empty, and the film would run at 1× under its name.
      if (select.value !== String(options.speed)) { select.value = '1'; throw new Error(`The bench has no speed ${options.speed}.`); }
    }
    game.loop.stop();
    const clock = film = { clock: 0, played: 0 };
    Date.now = () => clock.clock;
    restartTweenClock();
    rest();
    for (let index = 0; index < SETTLE_TICKS; index++) tick();
    // A film of the field at rest plays nothing: the ground alone, on the film's clock.
    stillFilm = options.still === true;
    if (!stillFilm) play();
    clock.played = 0;
    tick(0);
    return { duration: benchDuration(speed()), interval: filmInterval(speed()), tick: FILM_TICK_MS, stage };
  },
  advance(ms: number) {
    if (!film) throw new Error('No film is running.');
    for (let index = 0; index < Math.round(ms / FILM_TICK_MS); index++) tick();
    return { elapsed: film.played, stage };
  },
  stop() {
    if (!film || !game) return;
    film = null;
    stillFilm = false;
    Date.now = realNow;
    restartTweenClock();
    rest();
    delete lab.dataset.film;
    game.loop.start(game.step.bind(game));
  },
};

function fail(message: string) {
  ready = false;
  lab.dataset.ready = 'false';
  $('loading').hidden = false;
  $('load-detail').textContent = message;
  $('reload').hidden = false;
  playButton.disabled = true;
}

// An address that names an effect the art no longer lists (an older version's) opens on the first one, and says so.
const asked = new URLSearchParams(location.search).get('bench') || undefined;
const unknown = asked !== undefined && !listed.some(one => one.id === asked);
choose({ spell: unknown ? undefined : asked, actor: creatures[0].id });
spellSelect.addEventListener('change', rest);
actorSelect.addEventListener('change', () => { choose({ actor: actorSelect.value }); rest(); });
outcomeSelect.addEventListener('change', () => { choose({}); rest(); });
woundBox.addEventListener('change', rest);
damageInput.addEventListener('change', rest);
defeatBox.addEventListener('change', rest);
roundInput.addEventListener('change', rest);
playButton.addEventListener('click', play);
$('reset').addEventListener('click', rest);
$('reload').addEventListener('click', () => location.reload());
$('settings').addEventListener('click', () => $<HTMLDialogElement>('settings-dialog').showModal());
// A setting that changes mid-play ends the play: the effect is asked for again under the new setting.
$('reduce-motion').addEventListener('change', rest);
$('playback-speed').addEventListener('change', rest);
motion.addEventListener('change', rest);
document.addEventListener('visibilitychange', () => { if (document.hidden && playing() && !film) rest(); });

scene.onResizeDuringPlayback = rest;
// `ready` once the kit's bundle has arrived, `missing` if it did not: a film waits for one or the other.
scene.onKit = equipped => { lab.dataset.kit = equipped ? 'ready' : 'missing'; };
scene.onReady = () => {
  ready = true;
  lab.dataset.ready = 'true'; lab.dataset.renderer = 'phaser';
  lab.dataset.assetFallback = String(scene.assetFailed);
  $('loading').hidden = true;
  scene.events.on('update', (_time: number, delta: number) => advance(delta));
  show('decision');
};
try {
  game = bootScene(scene, $('phaser-mount'), {
    isReady: () => ready,
    onContextLost: () => fail('The graphics context was interrupted. Reload to reopen the scene.'),
    onNeverReady: () => fail('The scene could not finish loading. Check the connection and reload.'),
    onRest: resting => { if (resting) lab.dataset.resting = 'true'; else delete lab.dataset.resting; },
    moving: playing,
  });
} catch { fail('The renderer could not start. Reload the scene or use another browser.'); }

// What the film strip script and the browser specs drive: the bench's own choices, and its film clock.
// `title` names what the bench plays now, as a film strip is titled.
const title = () => { const { entry, outcome, wound, damage, defeat, round } = chosen(); return filmTitle(entry, { speed: speed(), outcome, wound, damage, defeat, round, still: stillFilm }); };
Object.assign(window, { battleBench: { entries, samples, creatures, kit: () => scene.effectsKit, stop: rest, title,
  loading: () => scene.load.isLoading(), shaking: () => scene.motion.view.trauma > 0, motion: () => scene.motion, groundObjects: () => scene.groundObjects, choose: (options: FilmOptions) => { choose(options); rest(); }, play, film: filmClock,
  places: () => scene.effectsPlaces, anchors: () => scene.anchors, halo: () => scene.actingHalo,
  effects: () => scene.children.list.filter(child => child.name === 'spell-vfx').map(child => {
    const { alpha, x, y, visible } = child as Phaser.GameObjects.Graphics;
    return { ...child.data.getAll(), alpha, x, y, visible };
  }) } });
