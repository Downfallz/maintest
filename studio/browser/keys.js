import { expect } from '@playwright/test';

// Reach a control with real Tab navigation and press Enter: no locator.focus(), synthetic clicks, or pointer
// actions. A control behind the focus in the page's order is reached with Shift+Tab, as a person would, rather than
// by tabbing round the whole page. The control is in view once it has the focus (WCAG 2.4.11).
export async function keyboardActivate(page, selector, key = 'Enter') {
  const target = typeof selector === 'string' ? page.locator(selector) : selector;
  await expect(target).toBeVisible(); await expect(target).toBeEnabled();
  const behind = await target.evaluate(node => {
    const active = document.activeElement;
    return Boolean(active && active !== document.body && node.compareDocumentPosition(active) & Node.DOCUMENT_POSITION_FOLLOWING);
  });
  const step = behind ? 'Shift+Tab' : 'Tab';
  for (let n = 0; n < 100 && !await target.evaluate(node => node === document.activeElement); n++) await page.keyboard.press(step);
  await expect(target).toBeFocused(); await expect(target).toBeInViewport();
  await page.keyboard.press(key);
}
