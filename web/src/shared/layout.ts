import type { SxProps, Theme } from '@mui/material/styles'

/** Spacing between the cards of a view, in theme units (12 px). */
export const VIEW_GAP = 1.5

/**
 * Root of every view. The shell is exactly one viewport tall (ADR-0015), so a view fills what is left and its
 * cards shrink to fit instead of making the page scroll.
 */
export const viewSx: SxProps<Theme> = {
  flex: 1,
  minHeight: 0,
  display: 'flex',
  flexDirection: 'column',
  gap: VIEW_GAP,
}

/** A card that takes the remaining space of its row or column and clips its content. */
export const fillCardSx: SxProps<Theme> = {
  flex: '1 1 0',
  minWidth: 0,
  minHeight: 0,
  display: 'flex',
  flexDirection: 'column',
  overflow: 'hidden',
}

/** A fixed-width side column that scrolls only if the window is too short for its cards. */
export const sideColumnSx = (width: number): SxProps<Theme> => ({
  flex: `0 0 ${String(width)}px`,
  minHeight: 0,
  overflowY: 'auto',
  display: 'flex',
  flexDirection: 'column',
  gap: VIEW_GAP,
})
