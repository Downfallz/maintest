import { browserPort, reusable } from './origin.js';
import { defineConfig } from '@playwright/test';

const port = browserPort();

export default defineConfig({
  testDir: '.',
  testMatch: '*.spec.js',
  fullyParallel: true,
  use: { launchOptions: { executablePath: process.env.STUDIO_CHROMIUM }, baseURL: `http://127.0.0.1:${port}`, trace: 'retain-on-failure', screenshot: 'only-on-failure' },
  projects: [
    { name: 'small-phone', use: { viewport: { width: 320, height: 740 }, isMobile: true, hasTouch: true } },
    { name: 'phone', use: { viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true } },
    { name: 'tablet', testMatch: 'table.ux.spec.js', use: { viewport: { width: 900, height: 700 }, isMobile: true, hasTouch: true } },
    { name: 'desktop', use: { viewport: { width: 1440, height: 1000 } } },
  ],
  webServer: { command: 'node server.js', port, reuseExistingServer: !process.env.CI && (await reusable(port)) },
});
