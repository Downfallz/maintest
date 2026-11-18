// The name a player reads for each spell id, read from `data/Spells` when the bundle is built (`build.mjs`).
declare module 'virtual:spell-names' {
  const names: Record<string, string>;
  export default names;
}
