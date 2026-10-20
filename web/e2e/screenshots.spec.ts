import path from 'node:path'

import { expect, test } from '@playwright/test'

import { loadSample } from './helpers'

// Captures the README screenshots: npm run screenshots
const output = path.join(import.meta.dirname, '..', '..', 'docs', 'screenshots')

test('capture screenshots of every view', async ({ page }) => {
  test.setTimeout(300_000)
  await loadSample(page)

  await page.getByRole('button', { name: 'Edge ratio' }).click()
  await page.getByRole('application', { name: /Image with keypoints/ }).focus()
  await page.keyboard.press('ArrowRight')
  await page.keyboard.press('ArrowRight')
  await expect(page.getByRole('img', { name: 'Descriptor as 16 orientation roses' })).toBeVisible()
  await page.screenshot({ path: path.join(output, 'detector.png'), fullPage: true })

  await page.getByRole('tab', { name: 'Pipeline' }).click()
  await page.getByRole('button', { name: /Edge rejection/ }).click()
  await expect(page.getByText(/rejected at this stage/)).toBeVisible()
  await page.screenshot({ path: path.join(output, 'pipeline.png'), fullPage: true })

  await page.getByRole('tab', { name: 'Matching' }).click()
  await page.getByRole('button', { name: /Test pair/ }).click()
  await page.getByRole('button', { name: 'Match', exact: true }).click()
  await expect(page.getByText(/Recovered rotation/)).toBeVisible({ timeout: 90_000 })
  await page.screenshot({ path: path.join(output, 'matching.png'), fullPage: true })

  await page.getByRole('tab', { name: 'Robustness' }).click()
  await page.getByRole('button', { name: 'Run sweep' }).click()
  await expect(page.getByRole('table')).toBeVisible({ timeout: 240_000 })
  await page.getByRole('slider', { name: 'Angle' }).focus()
  await page.keyboard.press('ArrowRight')
  await page.keyboard.press('ArrowRight')
  await page.keyboard.press('ArrowRight')
  await page.screenshot({ path: path.join(output, 'robustness.png'), fullPage: true })
})
