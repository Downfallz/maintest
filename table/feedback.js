// Opt-in interaction feedback. No remote assets, no background audio or
// dependency on game mechanics. Audio is created only from a user gesture.
const KEY = 'downfall-feedback-v1';
export function feedbackSettings(store) {
  try { return { sound: false, haptics: false, ...JSON.parse(store.getItem(KEY) || '{}') }; }
  catch { return { sound: false, haptics: false }; }
}
export function saveFeedbackSettings(store, value) {
  try { store.setItem(KEY, JSON.stringify({ sound: !!value.sound, haptics: !!value.haptics })); } catch {}
}
export function createFeedback(globalScope, store) {
  let settings = feedbackSettings(store);
  let audio = null;
  return {
    settings: () => ({ ...settings }),
    update(next) { settings = { ...settings, ...next }; saveFeedbackSettings(store, settings); },
    cue(kind) {
      if (settings.haptics) {
        try { globalScope.navigator?.vibrate?.(kind === 'confirm' ? 18 : 8); } catch {}
      }
      if (!settings.sound) return;
      const Audio = globalScope.AudioContext || globalScope.webkitAudioContext;
      if (!Audio) return;
      try {
        audio ??= new Audio();
        if (audio.state === 'suspended') void audio.resume();
        const start = audio.currentTime;
        const oscillator = audio.createOscillator();
        const gain = audio.createGain();
        oscillator.type = 'sine';
        oscillator.frequency.value = kind === 'confirm' ? 660 : 440;
        gain.gain.setValueAtTime(.0001, start);
        gain.gain.exponentialRampToValueAtTime(.035, start + .008);
        gain.gain.exponentialRampToValueAtTime(.0001, start + .085);
        oscillator.connect(gain);
        gain.connect(audio.destination);
        oscillator.start(start);
        oscillator.stop(start + .09);
      } catch { /* Audio is an optional enhancement. */ }
    },
  };
}
