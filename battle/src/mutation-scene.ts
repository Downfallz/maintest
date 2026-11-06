import { creatureAppearance } from '../../table/battle-art.js';
import type { createBattlePlanning } from '../../table/battle-planning.js';
import type { Catalogue } from './planning';
import type { SceneFrame } from './types';

type Planning = ReturnType<ReturnType<typeof createBattlePlanning>['model']>;

// This projection belongs solely to the current seat's visible command. Public inspection,
// history and playback continue to receive the recorded board.
export function mutationScene(plan: Planning, catalogue: Catalogue | null, selected: number | null, live: boolean, frame: SceneFrame) {
  const available = Boolean(live && plan.available && !plan.latest?.waiting && plan.latest?.waitingFor === 'Evolution');
  const eligible = new Set<string>();
  if (available) for (const offer of plan.latest.options?.evolution?.creatures ?? []) {
    if (offer.availableTiers?.length) eligible.add(String(offer.creature));
  }
  const recipient = available && plan.opened && selected != null ? String(selected) : null;
  const draft = plan.choice;
  let previewForm: string | undefined;
  if (recipient) {
    frame = { ...frame, mutationFocus: recipient };
    if (draft?.tier && draft.creature === selected && eligible.has(recipient)) {
      const own = plan.latest.board.allies.find((one: {id: number}) => one.id === selected);
      const packs = new Map(catalogue?.packages?.map(pack => [pack.id, pack]));
      if (own && packs.get(draft.tier)?.visual) {
        const preview = creatureAppearance({ acquiredTiers: [...(own.acquiredTiers ?? []), draft.tier] }, packs);
        previewForm = preview.form;
        frame.creatures = frame.creatures.map(one => one.id === recipient ? { ...one, ...preview } : one);
      }
    }
  }
  const prompt = available && !plan.opened && eligible.size ? 'Choose a creature on the battlefield to explore its mutations.' : null;
  function tag(id: string) {
    if (!eligible.has(id)) return null;
    if (id !== recipient) return '◇ MUTATE';
    return draft?.tier ? 'PRIVATE PREVIEW' : 'MUTATION TREE';
  }
  return { frame, eligible, recipient, previewForm, tag, prompt, available };
}
