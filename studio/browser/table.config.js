import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: '.',
  testMatch: 'table.practice.js',
  workers: 1,
  timeout: 45000,
  use: { launchOptions: { executablePath: process.env.STUDIO_CHROMIUM }, trace: 'retain-on-failure', screenshot: 'only-on-failure' },
  projects: [
    { name: 'table-phone', use: { viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true } },
    { name: 'table-desktop', use: { viewport: { width: 1440, height: 1000 } } },
  ],
});
