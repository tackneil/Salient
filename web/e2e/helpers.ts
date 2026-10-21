import { expect, type Page } from '@playwright/test'

/** Opens the detector, imports a bundled sample and waits for its detection run to finish. */
export async function loadSample(page: Page, title = 'Synthetic scene') {
  await page.goto('/detector')
  await page.getByRole('button', { name: `Use sample: ${title}` }).click()
  await expect(page.getByLabel('Run progress').getByRole('status')).toContainText('completed in', {
    timeout: 90_000,
  })
}

/**
 * Every view must fit one viewport (ADR-0015): the page itself never scrolls and no card clips or scrolls its own
 * content. The image stage, its canvases and anything marked `data-clips-by-design` are exempt, because they clip a zoomable or
 * transformed image on purpose.
 */
export async function expectFitsViewport(page: Page) {
  const problems = await page.evaluate(() => {
    const found: string[] = []
    const root = document.documentElement
    if (root.scrollHeight > window.innerHeight + 1) found.push('page scrolls vertically')
    if (root.scrollWidth > window.innerWidth + 1) found.push('page scrolls horizontally')
    for (const element of document.querySelectorAll<HTMLElement>('main *')) {
      // Form internals that MUI clips on purpose (slider inputs, the label notch of an outlined field).
      if (['INPUT', 'LEGEND', 'FIELDSET'].includes(element.tagName)) continue
      // The chart library clips its own drawing surface.
      const exempt =
        '[role="application"], [data-clips-by-design], canvas, [class*="MuiChartsWrapper"], [class*="MuiLineChart"]'
      if (element.closest(exempt)) continue
      // MUI parks a scrollbar-measuring box far off screen.
      if (element.getBoundingClientRect().bottom < 0) continue
      // Content pushed below the viewport by the shell's own clipping.
      if (element.getBoundingClientRect().bottom > window.innerHeight + 1) {
        let clipped = false
        for (let a = element.parentElement; a && a.tagName !== 'MAIN'; a = a.parentElement) {
          if (getComputedStyle(a).overflowY !== 'visible') clipped = true
        }
        if (!clipped) found.push(`${element.tagName.toLowerCase()} runs below the viewport`)
      }
      const style = getComputedStyle(element)
      const clipsY = ['auto', 'scroll', 'hidden'].includes(style.overflowY)
      if (clipsY && element.clientHeight > 0 && element.scrollHeight > element.clientHeight + 1) {
        const name = element.getAttribute('aria-label') ?? element.tagName.toLowerCase()
        found.push(
          `${name} clips ${String(element.scrollHeight - element.clientHeight)}px vertically`,
        )
      }
    }
    return found
  })
  expect(problems).toEqual([])
}
