// Without scripting every link jumps to its illustrated entry. With scripting each gallery becomes a tab set.
for (const gallery of document.querySelectorAll('[data-gallery]')) {
  const list = gallery.querySelector('[data-selector]');
  const tabs = [...list.querySelectorAll('a')];
  const panels = tabs.map(tab => document.getElementById(tab.hash.slice(1)));
  function select(index, setFragment = false) {
    tabs.forEach((tab, i) => {
      tab.setAttribute('aria-selected', String(i === index));
      tab.tabIndex = i === index ? 0 : -1;
      panels[i].hidden = i !== index;
    });
    if (setFragment) history.replaceState(null, '', tabs[index].hash);
  }
  list.setAttribute('role', 'tablist');
  tabs.forEach((tab, index) => {
    tab.setAttribute('role', 'tab');
    tab.setAttribute('aria-controls', panels[index].id);
    panels[index].setAttribute('role', 'tabpanel');
    panels[index].setAttribute('aria-labelledby', tab.id);
    panels[index].tabIndex = 0;
    tab.addEventListener('click', event => { event.preventDefault(); select(index, true); });
    tab.addEventListener('keydown', event => {
      const target = { ArrowRight: (index + 1) % tabs.length, ArrowLeft: (index + tabs.length - 1) % tabs.length, Home: 0, End: tabs.length - 1 }[event.key];
      if (target === undefined) return;
      event.preventDefault();
      select(target, true);
      tabs[target].focus();
    });
  });
  const current = () => tabs.findIndex(tab => tab.hash === location.hash);
  select(Math.max(0, current()));
  window.addEventListener('hashchange', () => { const index = current(); if (index >= 0) select(index); });
}
