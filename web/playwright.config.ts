import os from 'node:os'
import path from 'node:path'

import { defineConfig, devices } from '@playwright/test'

// End-to-end tests run the real API (with throwaway storage) and the Vite dev server (ADR-0012).
const storage = path.join(os.tmpdir(), 'salient-e2e')

export default defineConfig({
  testDir: './e2e',
  timeout: 120_000,
  expect: { timeout: 30_000 },
  fullyParallel: false,
  workers: 1,
  reporter: process.env.CI ? 'github' : 'list',
  use: {
    baseURL: 'http://localhost:5173',
    viewport: { width: 1440, height: 900 },
    colorScheme: 'dark',
    trace: 'retain-on-failure',
  },
  projects: [
    {
      name: 'e2e',
      testIgnore: /screenshots\.spec\.ts/,
      use: { ...devices['Desktop Chrome'], viewport: { width: 1440, height: 900 } },
    },
    {
      // README screenshots: npm run screenshots
      name: 'screenshots',
      testMatch: /screenshots\.spec\.ts/,
      use: { ...devices['Desktop Chrome'], viewport: { width: 1440, height: 900 } },
    },
  ],
  webServer: [
    {
      command: 'dotnet run --project ../src/Salient.Api --launch-profile http',
      url: 'http://localhost:5139/health',
      reuseExistingServer: !process.env.CI,
      timeout: 240_000,
      env: { Salient__StorageRoot: storage, Salient__JobsPerMinutePerClient: '1000' },
    },
    {
      command: 'npm run dev -- --port 5173 --strictPort',
      url: 'http://localhost:5173',
      reuseExistingServer: !process.env.CI,
      timeout: 60_000,
    },
  ],
})
