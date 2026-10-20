import ImageIcon from '@mui/icons-material/ImageOutlined'
import Box from '@mui/material/Box'
import Paper from '@mui/material/Paper'
import Typography from '@mui/material/Typography'
import type { ReactNode } from 'react'

/** Placeholder shown before the user has uploaded an image or started a run. */
export function EmptyState({
  title,
  children,
  action,
}: {
  title: string
  children: ReactNode
  action?: ReactNode
}) {
  return (
    <Paper
      variant="outlined"
      sx={{
        p: 6,
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: 1.5,
        textAlign: 'center',
      }}
    >
      <ImageIcon sx={{ fontSize: 48, color: 'text.secondary' }} />
      <Typography variant="h6" component="h1">
        {title}
      </Typography>
      <Typography color="text.secondary" sx={{ maxWidth: 520 }}>
        {children}
      </Typography>
      {action && <Box sx={{ mt: 1 }}>{action}</Box>}
    </Paper>
  )
}
