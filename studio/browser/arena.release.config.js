import { defineConfig, devices } from '@playwright/test';

// Browser engines and touch emulation are repeatable gates, not a claim of testing physical phones.
export default defineConfig({
  testDir: '.', testMatch: ['arena.loop.js', 'arena-release.spec.js', 'arena-live.spec.js'],
  grep: /complete 3v3|two arena seats|lobby rollout|sound requires|failed renderer|keyboard inspection|cold scene/,
  workers: 1, timeout: 45000,
  use: { baseURL: 'http://127.0.0.1:5199', trace: 'retain-on-failure', screenshot: 'only-on-failure' },
  projects: [
    { name: 'release-desktop', use: { browserName: 'chromium', viewport: { width: 1440, height: 1000 }, launchOptions: { executablePath: process.env.STUDIO_CHROMIUM } } },
    { name: 'release-android', use: { ...devices['Pixel 7'], browserName: 'chromium', launchOptions: { executablePath: process.env.STUDIO_CHROMIUM } } },
    { name: 'release-ios', use: { ...devices['iPhone 13'], browserName: 'webkit' } },
  ],
  webServer: { command: 'node server.js', port: 5199, reuseExistingServer: !process.env.CI },
});
