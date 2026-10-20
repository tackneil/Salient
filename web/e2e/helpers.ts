import { expect, type Page } from '@playwright/test'

/** Opens the detector, imports a bundled sample and waits for its detection run to finish. */
export async function loadSample(page: Page, title = 'Synthetic scene') {
  await page.goto('/detector')
  await page.getByRole('button', { name: `Use sample: ${title}` }).click()
  await expect(page.getByLabel('Run progress').getByRole('status')).toContainText('completed in', {
    timeout: 90_000,
  })
}
