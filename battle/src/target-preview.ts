import { battleTargetPreview } from '../../table/battle-targets.js';

type Preview = ReturnType<typeof battleTargetPreview>;
const line = (tag: string, value: string, className = '') => {
  const node = document.createElement(tag); node.textContent = value; node.className = className; return node;
};
// The scroll region and its position survive polls. Only changed facts replace the non-interactive text;
// the confirmation buttons stay outside it and never move when another target is selected.
export function targetPreviewControls(id: string) {
  const region = document.getElementById(id)!;
  let key = '';
  function render(model: Preview) {
    region.hidden = !model;
    const nextKey = JSON.stringify(model);
    if (key === nextKey) return;
    key = nextKey;
    const scroll = region.scrollTop;
    if (!model) { region.replaceChildren(); region.scrollTop = 0; return; }
    const nodes = [line('strong', `Estimate · ${model.cost}`, 'target-cost')];
    if (model.reason) nodes.push(line('p', model.reason, 'target-reason'));
    if (model.critical) nodes.push(line('p', model.critical));
    if (model.note) nodes.push(line('p', model.note));
    for (const row of model.rows) {
      const one = line('div', '', 'target-estimate');
      one.append(line('strong', row.name), line('p', `Plain: ${row.plain}`));
      if (row.critical) one.append(line('p', `Critical: ${row.critical}`));
      nodes.push(one);
    }
    if (model.caster) nodes.push(line('p', model.caster, 'target-caster'));
    nodes.push(line('small', model.caveat));
    region.replaceChildren(...nodes); region.scrollTop = scroll;
  }
  return { render };
}
