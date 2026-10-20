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
}: {
  title: string
  action?: ReactNode
  children: ReactNode
  gap?: number
}) {
  return (
    <Paper
      variant="outlined"
      component="section"
      sx={{ p: 2, display: 'flex', flexDirection: 'column', gap }}
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
