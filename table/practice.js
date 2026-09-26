// A practice capability is separate from seat storage. Normal matches never expose a reset control.
const key = 'downfall.table.practice';

export async function mountPractice(document, location, storage, fetchImpl = (...args) => globalThis.fetch(...args)) {
  const supplied = new URLSearchParams(location.search).get('practice');
  let token = supplied;
  try { token ||= storage?.getItem(key); } catch { /* Storage may be unavailable. */ }
  if (!token) return null;
  const section = document.getElementById('practice');
  const status = document.getElementById('practice-status');
  const request = async body => {
    const response = await fetchImpl('/api/practice', {
      method: body ? 'POST' : 'GET',
      headers: { 'X-Seat-Token': token, ...(body ? { 'Content-Type': 'application/json' } : {}) },
      ...(body ? { body: JSON.stringify(body) } : {}),
    });
    if (!response.ok) throw new Error(await response.text());
    return response.json();
  };
  let state;
  try { state = await request(); }
  catch (error) {
    if (supplied) {
      section.hidden = false;
      status.textContent = `Could not open practice: ${error.message}. Reload to retry.`;
    }
    return supplied ? { seats: [] } : null;
  }
  try { storage?.setItem(key, token); } catch { /* The capability stays in the URL below. */ }
  section.hidden = false;
  const list = document.getElementById('practice-scenario');
  for (const scenario of state.scenarios) {
    const option = document.createElement('option');
    option.value = scenario.id;
    option.textContent = scenario.name;
    list.append(option);
  }
  list.value = state.active ?? state.scenarios[0]?.id;
  const launch = document.getElementById('practice-start');
  const describe = () => {
    const selected = state.scenarios.find(one => one.id === list.value);
    document.getElementById('practice-goal').textContent = selected?.goal ?? '';
    launch.textContent = list.value === state.active ? 'Restart this scenario' : 'Start scenario';
    status.textContent = `Practice · seed ${state.seed} · not recorded`;
  };
  list.addEventListener('change', describe);
  describe();
  launch.addEventListener('click', async () => {
    launch.disabled = true;
    list.disabled = true;
    status.textContent = 'Preparing the scenario…';
    try {
      await request({ scenario: list.value });
      location.replace(`${location.pathname}?practice=${encodeURIComponent(token)}`);
    } catch (error) {
      status.textContent = `Could not start: ${error.message}`;
      launch.disabled = false;
      list.disabled = false;
    }
  });
  return { seats: state.seatToken ? [{ seat: 'player1', token: state.seatToken }] : [] };
}
