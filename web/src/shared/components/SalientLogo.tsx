import Box from '@mui/material/Box'
import Typography from '@mui/material/Typography'

import { tokens } from '../../app/theme'

/** The Salient mark: a keypoint with its orientation, surrounded by smaller features. */
export function SalientLogo() {
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.25 }}>
      <svg width="28" height="28" viewBox="0 0 28 28" fill="none" aria-hidden="true">
        <rect
          x="1"
          y="1"
          width="26"
          height="26"
          rx="7"
          fill="#1E2532"
          stroke={tokens.primary}
          strokeWidth="1.5"
        />
        <circle cx="14" cy="14" r="6" stroke={tokens.keypoint} strokeWidth="1.8" />
        <path
          d="M14 14 L19.5 10"
          stroke={tokens.keypoint}
          strokeWidth="1.8"
          strokeLinecap="round"
        />
        <circle cx="7" cy="20" r="1.6" fill={tokens.primary} />
        <circle cx="21" cy="21" r="1.2" fill={tokens.primary} />
        <circle cx="8" cy="8" r="1.2" fill={tokens.primary} />
      </svg>
      <Typography component="span" sx={{ fontSize: 18, fontWeight: 700, letterSpacing: '-0.01em' }}>
        Salient
      </Typography>
    </Box>
  )
}
