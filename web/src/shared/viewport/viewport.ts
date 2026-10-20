/** Maps image pixels to screen (container CSS) pixels: screen = image · scale + offset. */
export interface ViewTransform {
  scale: number
  x: number
  y: number
}

export interface Size {
  width: number
  height: number
}

export interface Point {
  x: number
  y: number
}

/** Zoom limits relative to the fit-to-container scale. */
export const MIN_ZOOM = 1
export const MAX_ZOOM = 32

export function fitScale(container: Size, image: Size): number {
  if (image.width <= 0 || image.height <= 0 || container.width <= 0 || container.height <= 0)
    return 1
  return Math.min(container.width / image.width, container.height / image.height)
}

/** The whole image, centered and scaled to fit. */
export function fitTransform(container: Size, image: Size): ViewTransform {
  const scale = fitScale(container, image)
  return {
    scale,
    x: (container.width - image.width * scale) / 2,
    y: (container.height - image.height * scale) / 2,
  }
}

/**
 * Pans so that no empty space appears inside the container along an axis where the image is larger than it, and centers
 * the image along an axis where it is smaller.
 */
export function clampPan(view: ViewTransform, container: Size, image: Size): ViewTransform {
  const clampAxis = (offset: number, viewport: number, content: number) =>
    content <= viewport
      ? (viewport - content) / 2
      : Math.min(0, Math.max(viewport - content, offset))
  return {
    scale: view.scale,
    x: clampAxis(view.x, container.width, image.width * view.scale),
    y: clampAxis(view.y, container.height, image.height * view.scale),
  }
}

/** Zooms by `factor` keeping the image point under `anchor` (screen coordinates) fixed. */
export function zoomAt(
  view: ViewTransform,
  factor: number,
  anchor: Point,
  container: Size,
  image: Size,
): ViewTransform {
  const fit = fitScale(container, image)
  const scale = Math.min(fit * MAX_ZOOM, Math.max(fit * MIN_ZOOM, view.scale * factor))
  const ratio = scale / view.scale
  return clampPan(
    {
      scale,
      x: anchor.x - (anchor.x - view.x) * ratio,
      y: anchor.y - (anchor.y - view.y) * ratio,
    },
    container,
    image,
  )
}

export function pan(
  view: ViewTransform,
  dx: number,
  dy: number,
  container: Size,
  image: Size,
): ViewTransform {
  return clampPan({ ...view, x: view.x + dx, y: view.y + dy }, container, image)
}

export const toImage = (view: ViewTransform, p: Point): Point => ({
  x: (p.x - view.x) / view.scale,
  y: (p.y - view.y) / view.scale,
})

export const toScreen = (view: ViewTransform, p: Point): Point => ({
  x: p.x * view.scale + view.x,
  y: p.y * view.scale + view.y,
})

/** Zoom relative to the fit scale, for display ("100%" when the image fits). */
export const relativeZoom = (view: ViewTransform, container: Size, image: Size) =>
  view.scale / fitScale(container, image)
