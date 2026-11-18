import type { createKit } from './fx';

// The hash of the kit's bundle, written in when this one is built (`build.mjs`).
declare const __KIT_TAG__: string;

// The effects kit is a bundle of its own beside this one (ADR 0196), fetched once the scene has shown its first
// frame. It is named by the hash of what was built, so a cache cannot pair a page with another deploy's kit.
// A kit that does not arrive is no failure: every spell is then drawn as its motif.
export async function loadKit(): Promise<typeof createKit | null> {
  try {
    const module = await import(new URL(`fx.js?v=${__KIT_TAG__}`, import.meta.url).href);
    return typeof module.createKit === 'function' ? module.createKit : null;
  } catch {
    return null;
  }
}
