import { expect, test } from '@playwright/test'

import { expectFitsViewport, loadSample } from './helpers'

test('detect keypoints and inspect one with the keyboard', async ({ page }) => {
  await loadSample(page)

  await page.getByRole('button', { name: 'Show timing breakdown' }).click()
  await expect(page.getByText('Timing')).toBeVisible()
  await page.keyboard.press('Escape')
  await page.getByRole('button', { name: 'Edge ratio' }).click()
  await expect(page.getByText('Edge ratio tr²(H) / det(H)')).toBeVisible()

  const stage = page.getByRole('application', { name: /Image with keypoints/ })
  await stage.focus()
  await page.keyboard.press('ArrowRight')

  const inspector = page
    .getByRole('region', { name: /Keypoint #/ })
    .or(page.getByText(/Keypoint #\d+/))
  await expect(inspector.first()).toBeVisible()
  await expect(page.getByRole('img', { name: 'Orientation histogram' })).toBeVisible()
  await expect(page.getByRole('img', { name: 'Descriptor as 16 orientation roses' })).toBeVisible()
  await expectFitsViewport(page)
})

test('step through the pipeline', async ({ page }) => {
  await loadSample(page)
  await page.getByRole('tab', { name: 'Pipeline' }).click()

  await page.getByRole('button', { name: /Gaussian pyramid/ }).click()
  await expect(page.getByRole('img', { name: /Gaussian layer 0 of octave 0/ })).toBeVisible()

  await page.getByRole('button', { name: /Edge rejection/ }).click()
  await expect(page.getByText(/kept · \d+ rejected at this stage/)).toBeVisible()
})

test('match a rotated test pair and recover the rotation', async ({ page }) => {
  await loadSample(page)
  await page.getByRole('tab', { name: 'Matching' }).click()

  await page.getByRole('button', { name: /Test pair/ }).click()
  // Match becomes available once image B has been uploaded.
  await expect(page.getByRole('button', { name: 'Match', exact: true })).toBeEnabled()
  await page.getByRole('button', { name: 'Match', exact: true }).click()

  const recovered = page.getByText(/Recovered rotation/)
  await expect(recovered).toBeVisible({ timeout: 90_000 })
  const text = (await recovered.textContent()) ?? ''
  const degrees = Number(/rotation\s*(-?[\d.]+)°/.exec(text)?.[1])
  expect(degrees).toBeGreaterThan(9.5)
  expect(degrees).toBeLessThan(10.5)
})

test('run a robustness sweep for one detector', async ({ page }) => {
  await loadSample(page, 'Geometry')
  await page.getByRole('tab', { name: 'Robustness' }).click()

  for (const checkbox of await page.getByRole('checkbox').all()) {
    const label = (await checkbox.locator('xpath=ancestor::label').textContent()) ?? ''
    if (!label.includes('Salient')) await checkbox.uncheck()
  }
  await page.getByRole('button', { name: 'Run sweep' }).click()

  await expect(page.getByRole('table')).toBeVisible({ timeout: 110_000 })
  await expect(page.getByText(/keeps the most correct matches/)).toBeVisible()
})
