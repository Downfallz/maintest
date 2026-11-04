// No context exists until a player enables sound. A failed/late resume never turns it back on.
export function createArenaSound({ createContext, onChange }) {
  let context, enabled = false, generation = 0;
  const voices = new Set();
  function stop() {
    for (const voice of voices) { voice.oscillator.stop(); voice.oscillator.disconnect(); voice.gain.disconnect(); }
    voices.clear();
  }
  function disable() { generation++; enabled = false; stop(); onChange(false); }
  async function enable() {
    const ticket = ++generation;
    try {
      context ??= createContext();
      await context.resume();
      if (ticket !== generation) return;
      enabled = context.state === 'running'; onChange(enabled);
    } catch { if (ticket === generation) { enabled = false; onChange(false); } }
  }
  function play(kind = 'strike') {
    if (!enabled || context?.state !== 'running') return;
    const oscillator = context.createOscillator(), gain = context.createGain();
    const voice = { oscillator, gain }; voices.add(voice);
    const frequency = { strike: 150, recovery: 440, energy: 330, condition: 220, fizzle: 80 }[kind] ?? 220;
    oscillator.type = 'triangle';
    oscillator.frequency.setValueAtTime(frequency, context.currentTime);
    oscillator.frequency.exponentialRampToValueAtTime(frequency / 2, context.currentTime + .12);
    gain.gain.setValueAtTime(.08, context.currentTime);
    gain.gain.exponentialRampToValueAtTime(.001, context.currentTime + .16);
    oscillator.connect(gain); gain.connect(context.destination);
    oscillator.onended = () => { voices.delete(voice); oscillator.disconnect(); gain.disconnect(); };
    oscillator.start(); oscillator.stop(context.currentTime + .17);
  }
  return { enable, disable, stop, play };
}
