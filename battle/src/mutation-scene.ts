import { creatureAppearance } from '../../table/battle-art.js';
import { resolveEvolution } from '../../table/battle-evolution.js';
import type { createBattlePlanning } from '../../table/battle-planning.js';
import type { Catalogue } from './planning';
import type { SceneFrame } from './types';

type Planning = ReturnType<ReturnType<typeof createBattlePlanning>['model']>;

// This projection belongs solely to the current seat's visible command. Public inspection,
// history and playback continue to receive the recorded board.
export function mutationScene(plan: Planning, catalogue: Catalogue | null, selected: number | null, live: boolean, frame: SceneFrame,
  purchases: { order: (creature: string) => string[] } | null = null) {
  const available = Boolean(live && plan.available && !plan.latest?.waiting && plan.latest?.waitingFor === 'Evolution');
  const eligible = new Set<string>();
  if (available) for (const offer of plan.latest.options?.evolution?.creatures ?? []) {
    if (offer.availableTiers?.length) eligible.add(String(offer.creature));
  }
  const recipient = available && plan.opened && selected != null ? String(selected) : null;
  const draft = plan.choice;
  const packs = new Map(catalogue?.packages?.map(pack => [pack.id, pack]));
  // The creature drawn as the mutation would make it, the mutation counted as its latest purchase (ADR 0151).
  function wear(id: string, tier: string) {
    const own = plan.latest?.board?.allies?.find((one: {id: number}) => String(one.id) === id);
    if (!own || !packs.get(tier)?.visual) return undefined;
    const acquiredTiers = [...(own.acquiredTiers ?? []), tier];
    const preview = creatureAppearance({ acquiredTiers }, packs);
    const evolution = resolveEvolution({ id, acquiredTiers }, packs, { log: { order: (one: string) => [...(purchases?.order(one) ?? []), tier] } });
    frame = { ...frame, creatures: frame.creatures.map(one => one.id === id ? { ...one, ...preview, evolution } : one) };
    return preview.form as string;
  }
  // A pick the host accepted keeps its creature in the form it leads to until the reveal applies it (UX12b): the
  // seat's own picks, which the host holds privately, never a draft. Read only on the seat's live field.
  const picked = new Map<string, { tier: string; name: string; form?: string }>();
  if (live && plan.latest && !plan.latest.replay && !plan.latest.over) {
    for (const pick of plan.latest.board?.evolutionChoices ?? []) {
      const id = String(pick.creature), own = plan.latest.board.allies?.find((one: {id: number}) => String(one.id) === id);
      if (!own || !pick.tier || (own.acquiredTiers ?? []).includes(pick.tier)) continue;
      picked.set(id, { tier: pick.tier, name: packs.get(pick.tier)?.name ?? pick.tier, form: wear(id, pick.tier) });
    }
  }
  let previewForm: string | undefined;
  if (recipient) {
    frame = { ...frame, mutationFocus: recipient };
    if (draft?.tier && draft.creature === selected && eligible.has(recipient)) previewForm = wear(recipient, draft.tier);
  }
  const prompt = available && !plan.opened && eligible.size ? 'Choose a creature on the battlefield to explore its mutations.' : null;
  function tag(id: string) {
    if (picked.has(id)) return 'PICKED';
    if (!eligible.has(id)) return null;
    if (id !== recipient) return '◇ MUTATE';
    return draft?.tier ? 'PRIVATE PREVIEW' : 'MUTATION TREE';
  }
  // What a screen reader hears in place of PICKED: the tab's own words.
  const spoken = (id: string) => picked.has(id) ? `picked ${picked.get(id)!.name}, hidden until reveal` : null;
  return { frame, eligible, recipient, previewForm, picked, tag, spoken, prompt, available };
}
