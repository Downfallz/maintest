import { mountPractice } from './practice.js';

// Complete the optional practice handshake before the table reads any ordinary seat tokens.
function kept() {
  try {
    return globalThis.localStorage ?? null;
  } catch {
    return null;
  }
}

export const storage = kept();
export const practice = await mountPractice(document, globalThis.location, storage);
