import { imageContentUrl } from '../../shared/api/client'

/**
 * Creates the second image of a test pair in the browser: the first image rotated counterclockwise (as seen on screen)
 * and scaled around its center, on a black background. The result is a PNG file ready to upload.
 */
export async function createRotatedCopy(
  imageId: string,
  width: number,
  height: number,
  degrees: number,
  scale: number,
): Promise<File> {
  const image = new Image()
  image.src = imageContentUrl(imageId)
  await image.decode()

  const canvas = document.createElement('canvas')
  canvas.width = width
  canvas.height = height
  const context = canvas.getContext('2d')
  if (!context) throw new Error('Canvas is not available.')
  context.fillStyle = '#0b0d11'
  context.fillRect(0, 0, width, height)
  context.translate(width / 2, height / 2)
  // Canvas rotation is clockwise with Y down, so a counterclockwise turn on screen is a negative angle.
  context.rotate((-degrees * Math.PI) / 180)
  context.scale(scale, scale)
  context.drawImage(image, -width / 2, -height / 2, width, height)

  const blob = await new Promise<Blob | null>((resolve) => {
    canvas.toBlob(resolve, 'image/png')
  })
  if (!blob) throw new Error('Could not encode the test image.')
  return new File([blob], `rotated_${degrees}deg_x${scale}.png`, { type: 'image/png' })
}
