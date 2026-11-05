import type { SpellVisual } from './types';

type Tone = { start: number; end: number; duration: number; type: OscillatorType };
const TONES: Record<string, Tone> = {
  physical: { start: 130, end: 42, duration: .20, type: 'triangle' },
  biological: { start: 190, end: 72, duration: .17, type: 'triangle' },
  elemental: { start: 420, end: 150, duration: .20, type: 'sine' },
  necromantic: { start: 95, end: 65, duration: .30, type: 'sine' },
  astral: { start: 260, end: 390, duration: .28, type: 'sine' },
  neutral: { start: 180, end: 130, duration: .12, type: 'sine' },
};

// One gesture-enabled context, no downloads or background loop. A cancelled resume cannot re-enable sound.
export class ArenaAudio {
  private context: AudioContext | null = null;
  private enabled = false;
  private generation = 0;
  private readonly voices = new Set<{ oscillator: OscillatorNode; gain: GainNode }>();
  constructor(private readonly createContext = () => new AudioContext()) {}

  async enable(value: boolean) {
    this.enabled = false;
    this.stop();
    const ticket = this.generation;
    if (!value) return false;
    try {
      this.context ??= this.createContext();
      await this.context.resume();
      if (ticket !== this.generation) return false;
      this.enabled = this.context.state === 'running';
      return this.enabled;
    } catch { return false; }
  }

  // A privacy or lifecycle change: release the voices and fence a pending resume, so it cannot turn sound back on.
  stop() {
    this.generation++;
    this.silence();
  }

  // A cancelled or replaced animation: release the voices only. A resume the player just asked for still lands.
  silence() {
    for (const voice of this.voices) {
      voice.oscillator.onended = null;
      try { voice.oscillator.stop(); } catch { /* Already ended. */ }
      voice.oscillator.disconnect(); voice.gain.disconnect();
    }
    this.voices.clear();
  }

  play(visual?: SpellVisual) {
    const context = this.context;
    if (!this.enabled || !context) return;
    // A backgrounded tab or a phone call suspends the context under an opted-in player: ask for it back, and
    // let this one impact pass silently rather than play late.
    if (context.state !== 'running') { context.resume().catch(() => {}); return; }
    this.silence();
    const family = visual?.motif === 'fizzle' ? 'neutral' : visual?.family ?? 'neutral';
    const tone = Object.hasOwn(TONES, family) ? TONES[family] : TONES.neutral;
    const oscillator = context.createOscillator(), gain = context.createGain();
    const voice = { oscillator, gain };
    this.voices.add(voice);
    oscillator.type = tone.type;
    oscillator.frequency.setValueAtTime(tone.start, context.currentTime);
    oscillator.frequency.exponentialRampToValueAtTime(tone.end, context.currentTime + tone.duration);
    gain.gain.setValueAtTime(.0001, context.currentTime);
    gain.gain.exponentialRampToValueAtTime(.055, context.currentTime + .012);
    gain.gain.exponentialRampToValueAtTime(.0001, context.currentTime + tone.duration);
    oscillator.connect(gain); gain.connect(context.destination);
    oscillator.onended = () => { oscillator.disconnect(); gain.disconnect(); this.voices.delete(voice); };
    oscillator.start(); oscillator.stop(context.currentTime + tone.duration + .02);
  }
}

// Sound is deliberately opt-in on every page load, even after browser form restoration.
export function soundControl(audio: ArenaAudio, input: HTMLInputElement) {
  let revision = 0;
  input.checked = false;
  async function toggle() {
    const ticket = ++revision;
    const enabled = await audio.enable(input.checked);
    if (ticket === revision) { input.checked = enabled; }
  }
  input.addEventListener('change', () => { void toggle(); });
}
