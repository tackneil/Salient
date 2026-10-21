import Box from '@mui/material/Box'
import Paper from '@mui/material/Paper'
import Typography from '@mui/material/Typography'
import type { ReactNode } from 'react'

/** A card with an overline title, the building block of the side panels in the design. */
export function Section({
  title,
  action,
  children,
  gap = 1.75,
  grow = false,
}: {
  title: string
  action?: ReactNode
  children: ReactNode
  gap?: number
  /** Takes the remaining height of its column, so content inside may stretch (ADR-0015). */
  grow?: boolean
}) {
  return (
    <Paper
      variant="outlined"
      component="section"
      sx={{
        p: 2,
        display: 'flex',
        flexDirection: 'column',
        gap,
        ...(grow ? { flex: '1 0 auto' } : {}),
      }}
    >
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 1 }}>
        <Typography
          variant="overline"
          component="h2"
          color="text.secondary"
          sx={{ lineHeight: 1.5 }}
        >
          {title}
        </Typography>
        {action}
      </Box>
      {children}
    </Paper>
  )
}
