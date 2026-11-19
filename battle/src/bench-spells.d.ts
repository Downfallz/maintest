// What the content says of each spell id, read from `data/` when the bundle is built (`bench-spells.mjs`).
declare module 'virtual:bench-spells' {
  const spells: Record<string, { name: string; mutations: { name: string; level: number }[]; starting: boolean }>;
  export default spells;
}
