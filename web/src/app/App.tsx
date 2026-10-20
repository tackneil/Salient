import AppBar from '@mui/material/AppBar'
import Box from '@mui/material/Box'
import Container from '@mui/material/Container'
import Paper from '@mui/material/Paper'
import Tab from '@mui/material/Tab'
import Tabs from '@mui/material/Tabs'
import Toolbar from '@mui/material/Toolbar'
import Typography from '@mui/material/Typography'
import { useState } from 'react'

import { SalientLogo } from '../shared/components/SalientLogo'

const sections = ['Detector', 'Pipeline', 'Matching', 'Robustness'] as const

// Application shell. Routing and the real views arrive in Phase 7 (see docs/implementation-plan.md).
export function App() {
  const [section, setSection] = useState(0)

  return (
    <Box sx={{ minHeight: '100vh', display: 'flex', flexDirection: 'column' }}>
      <AppBar position="static">
        <Toolbar sx={{ gap: 3, flexWrap: 'wrap' }}>
          <SalientLogo />
          <Tabs
            value={section}
            onChange={(_, value: number) => {
              setSection(value)
            }}
            aria-label="Sections"
          >
            {sections.map((name) => (
              <Tab key={name} label={name} />
            ))}
          </Tabs>
        </Toolbar>
      </AppBar>
      <Container component="main" maxWidth="lg" sx={{ py: 4 }}>
        <Paper variant="outlined" sx={{ p: 4 }}>
          <Typography variant="h5" component="h1" gutterBottom>
            {sections[section]}
          </Typography>
          <Typography color="text.secondary">
            This view is not implemented yet. The frontend scaffold is in place; views arrive in
            Phase 7 of the implementation plan.
          </Typography>
        </Paper>
      </Container>
    </Box>
  )
}
