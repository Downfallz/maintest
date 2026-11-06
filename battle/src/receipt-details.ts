// Both upkeep surfaces keep their full accessible receipt behind the same keyboard-operable disclosure.
export function receiptDetails(button: HTMLButtonElement, body: HTMLElement) {
  function expand(value: boolean) {
    body.hidden = !value;
    button.setAttribute('aria-expanded', String(value));
    button.textContent = value ? 'Hide details' : 'Details';
  }
  button.addEventListener('click', () => expand(Boolean(body.hidden)));
  return { expand };
}
